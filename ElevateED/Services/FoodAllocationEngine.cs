using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using ElevateED.Models;

namespace ElevateED.Services
{
    /// <summary>
    /// Bulk food allocation engine.
    ///
    /// Two modes:
    ///   1. ComputePlan() — pure, read-only. Returns what the engine WOULD do.
    ///   2. Preview and commit are handled by the controller.
    ///
    /// Design principles (see conversation):
    ///   • Students don't choose food type or quantity.
    ///   • Allergen conflicts are hard-blocked.
    ///   • Urgent / High / Medium / Low priority raises a student's position
    ///     in the queue, but doesn't guarantee a fixed amount — supply is
    ///     irregular in a donation system.
    ///   • A rolling 7-day window tracks how much each student received, so
    ///     fairness is achieved over time rather than per-day.
    ///   • Per-donation caps by food type prevent one student from taking
    ///     everything from a single large donation.
    ///   • Per-7-day caps prevent hoarding.
    /// </summary>
    public class FoodAllocationEngine
    {
        // ── Configurable constants ─────────────────────────────────

        /// <summary>Rolling window used to measure recent supply.</summary>
        public const int RollingWindowDays = 7;

        /// <summary>Maximum units a single student may receive in the window.</summary>
        public const int MaxPerStudentInWindow = 15;

        /// <summary>Priority multipliers for the need score.</summary>
        private static readonly Dictionary<RequestPriority, double> PriorityWeights =
            new Dictionary<RequestPriority, double>
        {
            { RequestPriority.Urgent, 3.0 },
            { RequestPriority.High,   2.0 },
            { RequestPriority.Medium, 1.0 },
            { RequestPriority.Low,    0.5 }
        };

        /// <summary>Per-donation cap by food type.</summary>
        private static readonly Dictionary<string, int> CapPerDonationByFoodType =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Canned Food",   4 },
            { "Dry Goods",     4 },
            { "Fresh Produce", 5 },
            { "Bread & Bakery",2 },
            { "Snacks",        3 },
            { "Beverages",     4 },
            { "Ready Meals",   2 },
            { "Other",         3 }
        };

        private const int DefaultCapPerDonation = 3;

        // ────────────────────────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the pool of eligible students from the database.
        /// One candidate per open food request item.
        /// </summary>
        public async Task<List<FoodRequestCandidate>> BuildCandidatesAsync(
            ElevateEDContext context)
        {
            var cutoff = DateTime.Now.AddDays(-RollingWindowDays);

            // All active food request items that haven't been fulfilled yet.
            var pendingItems = await context.DonationRequestItems
                .Include("DonationRequest")
                .Include("DonationRequest.Student")
                .Include("DonationRequest.Student.User")
                .Where(i => i.Category == DonationCategory.Food
                            && i.DonationRequest.IsActive
                            && !i.IsFulfilled
                            && i.Status != RequestItemStatus.Declined)
                .ToListAsync();

            if (!pendingItems.Any())
                return new List<FoodRequestCandidate>();

            // Pre-load per-student recent history so we can compute need scores.
            var studentIds = pendingItems
                .Select(i => i.DonationRequest.StudentId)
                .Distinct()
                .ToList();

            var recentAllocations = await context.DonationAllocations
                .Where(a => studentIds.Contains(a.StudentId)
                            && a.IsActive
                            && a.AllocationDate >= cutoff)
                .ToListAsync();

            var lastEverAllocations = await context.DonationAllocations
                .Where(a => studentIds.Contains(a.StudentId) && a.IsActive)
                .GroupBy(a => a.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    LastDate = g.Max(a => a.AllocationDate)
                })
                .ToListAsync();

            var candidates = new List<FoodRequestCandidate>();

            foreach (var item in pendingItems)
            {
                var student = item.DonationRequest?.Student;
                if (student == null) continue;

                var recentForStudent = recentAllocations
                    .Where(a => a.StudentId == student.Id)
                    .ToList();

                int unitsInWindow = recentForStudent.Sum(a => a.QuantityAllocated);
                int allocationsInWindow = recentForStudent.Count;

                DateTime? lastServed = lastEverAllocations
                    .Where(x => x.StudentId == student.Id)
                    .Select(x => (DateTime?)x.LastDate)
                    .FirstOrDefault();

                candidates.Add(new FoodRequestCandidate
                {
                    StudentId = student.Id,
                    StudentName = student.FullName ?? "Unknown",
                    StudentNumber = student.User?.StudentNumber,
                    RequestId = item.DonationRequestId,
                    RequestItemId = item.Id,
                    RequestDate = item.DonationRequest.RequestDate,
                    Priority = item.Priority,
                    DietaryRequirements = item.DietaryRequirements ?? "",
                    UnitsInWindow = unitsInWindow,
                    AllocationsInWindow = allocationsInWindow,
                    LastServedDate = lastServed
                });
            }

            // Compute need scores now (they don't depend on the donation).
            foreach (var c in candidates)
                c.NeedScore = ComputeNeedScore(c);

            return candidates;
        }

        /// <summary>
        /// Pure computation. Given a donation and the candidate pool,
        /// returns the plan for that donation.
        /// </summary>
        public FoodAllocationPlan ComputePlanForDonation(
            DonationItem donation,
            FoodDonationCheck foodCheck,
            IList<FoodRequestCandidate> candidates)
        {
            var plan = new FoodAllocationPlan
            {
                DonationItemId = donation.Id,
                ItemName = donation.ItemName,
                FoodType = foodCheck?.FoodType ?? "Other",
                Allergens = foodCheck?.Allergens ?? FoodAllergen.None,
                TotalAvailable = donation.QuantityRemaining,
                RemainingUnallocated = donation.QuantityRemaining
            };

            if (donation.QuantityRemaining <= 0 || !candidates.Any())
                return plan;

            // ── Step 1 — Filter by allergen compatibility ───────────
            var eligible = new List<FoodRequestCandidate>();
            foreach (var c in candidates)
            {
                var reason = GetIneligibilityReason(c, plan.Allergens);
                if (reason != null)
                {
                    plan.Excluded.Add(new ExcludedStudent
                    {
                        StudentId = c.StudentId,
                        StudentName = c.StudentName,
                        Reason = reason
                    });
                }
                else
                {
                    eligible.Add(c);
                }
            }

            // ── Step 2 — Skip students already at their 7-day cap ──
            eligible = eligible
                .Where(c => c.UnitsInWindow < MaxPerStudentInWindow)
                .ToList();

            plan.EligibleStudentCount = eligible.Count;
            plan.ExcludedStudentCount = plan.Excluded.Count;

            if (!eligible.Any())
                return plan;

            // ── Step 3 — Sort by need score, then by wait time ──────
            eligible = eligible
                .OrderByDescending(c => c.NeedScore)
                .ThenBy(c => c.RequestDate)
                .ToList();

            // ── Step 4 — Per-donation cap for this food type ────────
            int cap = CapPerDonationByFoodType.ContainsKey(plan.FoodType)
                ? CapPerDonationByFoodType[plan.FoodType]
                : DefaultCapPerDonation;

            // ── Step 5 — Water-fill allocation ──────────────────────
            int remaining = donation.QuantityRemaining;
            var pool = eligible.ToList();
            int safety = 0;

            while (remaining > 0 && pool.Any() && safety < 10)
            {
                safety++;
                int fairShare = remaining / pool.Count;

                if (fairShare <= 0)
                {
                    // Not enough for everyone to get 1 → top N get 1 each.
                    int n = Math.Min(remaining, pool.Count);
                    for (int i = 0; i < n; i++)
                    {
                        AddOrUpdateLine(plan, pool[i], 1, cap);
                        remaining--;
                    }
                    break;
                }

                int movedThisPass = 0;
                var stillWanting = new List<FoodRequestCandidate>();

                foreach (var c in pool)
                {
                    var line = plan.Lines.FirstOrDefault(l => l.StudentId == c.StudentId);
                    int already = line?.AllocatedQuantity ?? 0;

                    int roomInCap = cap - already;
                    int roomInWeek = MaxPerStudentInWindow - c.UnitsInWindow - already;
                    int room = Math.Min(roomInCap, roomInWeek);

                    if (room <= 0) continue;

                    int give = Math.Min(fairShare, room);
                    if (give <= 0) continue;

                    AddOrUpdateLine(plan, c, give, cap);
                    remaining -= give;
                    movedThisPass += give;

                    if (already + give < cap) stillWanting.Add(c);
                }

                if (movedThisPass == 0) break;
                pool = stillWanting;
            }

            plan.TotalAllocated = donation.QuantityRemaining - remaining;
            plan.RemainingUnallocated = remaining;
            return plan;
        }

        // ────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────

        private static double ComputeNeedScore(FoodRequestCandidate c)
        {
            double priorityWeight = PriorityWeights.ContainsKey(c.Priority)
                ? PriorityWeights[c.Priority]
                : 1.0;

            int daysSinceServed = c.LastServedDate.HasValue
                ? Math.Min(30, (int)(DateTime.Now - c.LastServedDate.Value).TotalDays)
                : 15; // never served → treat as 15 days waiting

            // More waiting → higher score, weighted by priority.
            double waitScore = priorityWeight * daysSinceServed;

            // Recent allocations reduce the score slightly so that the same
            // student doesn't get served twice in a row.
            double recencyPenalty = 0.5 * c.AllocationsInWindow;

            // If the student already received a lot this window, penalise.
            double volumePenalty = 0.3 * c.UnitsInWindow;

            return Math.Max(0, waitScore - recencyPenalty - volumePenalty);
        }

        private static void AddOrUpdateLine(
            FoodAllocationPlan plan,
            FoodRequestCandidate c,
            int give,
            int cap)
        {
            var line = plan.Lines.FirstOrDefault(l => l.StudentId == c.StudentId);
            if (line == null)
            {
                line = new AllocationLine
                {
                    StudentId = c.StudentId,
                    StudentName = c.StudentName,
                    StudentNumber = c.StudentNumber,
                    RequestId = c.RequestId,
                    RequestItemId = c.RequestItemId,
                    RequestDate = c.RequestDate,
                    Priority = c.Priority,
                    PriorityLabel = c.Priority.ToString(),
                    NeedScore = c.NeedScore,
                    UnitsInWindow = c.UnitsInWindow,
                    AllocatedQuantity = 0
                };
                plan.Lines.Add(line);
            }

            line.AllocatedQuantity += give;
            if (line.AllocatedQuantity > cap)
                line.AllocatedQuantity = cap;
        }

        /// <summary>
        /// Returns a reason string if the student can't eat this food, or
        /// null if they can.
        /// </summary>
        private static string GetIneligibilityReason(
            FoodRequestCandidate c,
            FoodAllergen allergen)
        {
            if (allergen == FoodAllergen.None) return null;

            var reqs = (c.DietaryRequirements ?? "").ToLowerInvariant();

            if (reqs.Contains("gluten-free") && allergen == FoodAllergen.Gluten)
                return "Gluten-Free (food contains Gluten)";

            if (reqs.Contains("nut-free")
                && (allergen == FoodAllergen.Nuts || allergen == FoodAllergen.Peanuts))
                return "Nut-Free (food contains nuts)";

            if (reqs.Contains("dairy-free") && allergen == FoodAllergen.Dairy)
                return "Dairy-Free (food contains Dairy)";

            if (reqs.Contains("vegan")
                && (allergen == FoodAllergen.Dairy
                    || allergen == FoodAllergen.Eggs
                    || allergen == FoodAllergen.Shellfish))
                return "Vegan (food contains animal products)";

            if ((reqs.Contains("vegetarian")
                 || reqs.Contains("halal")
                 || reqs.Contains("kosher"))
                && allergen == FoodAllergen.Shellfish)
                return "Dietary restriction (food contains Shellfish)";

            return null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Supporting types
    // ────────────────────────────────────────────────────────────────

    public class FoodRequestCandidate
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }

        public int RequestId { get; set; }
        public int RequestItemId { get; set; }
        public DateTime RequestDate { get; set; }

        public RequestPriority Priority { get; set; }
        public string DietaryRequirements { get; set; }

        // Rolling window stats
        public int UnitsInWindow { get; set; }
        public int AllocationsInWindow { get; set; }
        public DateTime? LastServedDate { get; set; }

        // Computed by the engine
        public double NeedScore { get; set; }
    }

    public class FoodAllocationPlan
    {
        public int DonationItemId { get; set; }
        public string ItemName { get; set; }
        public string FoodType { get; set; }
        public FoodAllergen Allergens { get; set; }

        public int TotalAvailable { get; set; }
        public int TotalAllocated { get; set; }
        public int RemainingUnallocated { get; set; }

        public int EligibleStudentCount { get; set; }
        public int ExcludedStudentCount { get; set; }

        public List<AllocationLine> Lines { get; set; }
        public List<ExcludedStudent> Excluded { get; set; }

        public FoodAllocationPlan()
        {
            Lines = new List<AllocationLine>();
            Excluded = new List<ExcludedStudent>();
        }
    }

    public class AllocationLine
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }

        public int RequestId { get; set; }
        public int RequestItemId { get; set; }
        public DateTime RequestDate { get; set; }

        public RequestPriority Priority { get; set; }
        public string PriorityLabel { get; set; }

        public double NeedScore { get; set; }
        public int UnitsInWindow { get; set; }
        public int AllocatedQuantity { get; set; }
    }

    public class ExcludedStudent
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string Reason { get; set; }
    }
}