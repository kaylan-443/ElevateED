using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    /// <summary>
    /// The automated half of the redesigned donation system (UC05 and UC08
    /// from the design doc). There is no job scheduler anywhere in this app
    /// (no Hangfire/Quartz — confirmed), so <c>Global.asax.cs</c> runs this
    /// on a plain <see cref="System.Threading.Timer"/>. It's equally safe to
    /// call directly from a controller action (the admin's "Run Matching
    /// Now" button, or right after a new request/available item appears),
    /// since every call opens and disposes its own short-lived context —
    /// nothing here depends on a particular caller's DbContext instance.
    ///
    /// Food donations are explicitly excluded everywhere in this class —
    /// they have their own allergen-aware <c>FoodAllocationEngine</c> and
    /// are untouched by this redesign.
    /// </summary>
    public static class DonationAutomationEngine
    {
        // How long a confirmed-but-uncollected allocation stays reserved
        // before the item is automatically released back to the pool (UC08).
        private const int CollectionGraceDays = 5;

        // ───────────────────────── UC05 — Auto-Match ─────────────────────────

        /// <summary>
        /// For every non-Food category, pairs the highest-priority waiting
        /// request items with the best-fit Available donation items and
        /// creates "PendingReview" allocation proposals for the admin Match
        /// Review queue. Never decrements stock and never notifies anyone —
        /// that only happens once an admin confirms the match (UC06).
        /// Returns how many new proposals were created.
        /// </summary>
        public static int RunMatchingSweep()
        {
            int proposalsCreated = 0;

            using (var db = new ElevateEDContext())
            {
                var categories = Enum.GetValues(typeof(DonationCategory))
                    .Cast<DonationCategory>()
                    .Where(c => c != DonationCategory.Food);

                foreach (var category in categories)
                {
                    proposalsCreated += MatchCategory(db, category);
                }
            }

            return proposalsCreated;
        }

        private static int MatchCategory(ElevateEDContext db, DonationCategory category)
        {
            // Waitlisted (unmatched) request items in this category, highest
            // priority first. DonationRequestItem.CalculateItemScore() already
            // blends urgency, grade level, and specificity — reused as-is.
            var waitingItems = db.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Where(i => i.Category == category
                            && i.DonationRequest.IsActive
                            && !i.IsFulfilled
                            && i.Status == RequestItemStatus.Pending)
                .ToList()
                .OrderByDescending(i => i.CalculateItemScore())
                .ThenBy(i => i.DonationRequest.RequestDate)
                .ToList();

            if (!waitingItems.Any()) return 0;

            var availableItems = db.DonationItems
                .Include(d => d.Allocations)
                .Where(d => d.Category == category
                            && d.IsActive
                            && d.Status == DonationStatus.Available)
                .ToList()
                .OrderBy(d => d.DonationDate) // oldest stock first — reduces shelf life / storage pressure
                .ToList();

            if (!availableItems.Any()) return 0;

            var created = 0;

            foreach (var item in waitingItems)
            {
                var remaining = item.QuantityRequested - item.QuantityReceived;
                if (remaining <= 0) continue;

                var best = FindBestFit(item, availableItems);
                if (best == null) continue;

                var reservedQty = ReservedQuantity(best);
                var availableQty = best.QuantityRemaining - reservedQty;
                if (availableQty <= 0) continue;

                var qty = Math.Min(remaining, availableQty);
                var fitScore = ScoreFit(item, best);

                var allocation = new DonationAllocation
                {
                    DonationItemId = best.Id,
                    StudentId = item.DonationRequest.StudentId,
                    RequestId = item.DonationRequestId,
                    QuantityAllocated = qty,
                    Status = "PendingReview",
                    AllocationDate = DateTime.Now,
                    IsActive = true,
                    Notes = $"Auto-matched by the system — {fitScore}% fit on {category}."
                };

                db.DonationAllocations.Add(allocation);
                item.Status = RequestItemStatus.Reserved;

                db.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = best.Id,
                    Action = "MatchProposed",
                    Description = $"System proposed matching request #{item.DonationRequestId} ({qty} unit(s), {fitScore}% fit) — pending admin review.",
                    ActorType = "System",
                    ActionDate = DateTime.Now
                });

                created++;

                // Keep the in-memory reservation total current so the next
                // waiting item in this same sweep doesn't over-commit stock
                // that was just reserved a few lines above.
                if (best.Allocations == null) best.Allocations = new HashSet<DonationAllocation>();
                best.Allocations.Add(allocation);
            }

            if (created > 0) db.SaveChanges();
            return created;
        }

        private static int ReservedQuantity(DonationItem item)
        {
            return item.Allocations?
                .Where(a => a.IsActive && (a.Status == "PendingReview" || a.Status == "AwaitingCollection"))
                .Sum(a => (int?)a.QuantityAllocated) ?? 0;
        }

        private static DonationItem FindBestFit(DonationRequestItem item, List<DonationItem> candidates)
        {
            DonationItem best = null;
            var bestScore = -1;

            foreach (var candidate in candidates)
            {
                if (candidate.QuantityRemaining - ReservedQuantity(candidate) <= 0) continue;

                var score = ScoreFit(item, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        // Category is already guaranteed equal by the caller's query (worth
        // 40 of the 100 points on its own); grade/size/subject/type bonuses
        // on top follow the same spirit as CalculateItemScore()'s weighting.
        private static int ScoreFit(DonationRequestItem item, DonationItem candidate)
        {
            var score = 40;

            if (!string.IsNullOrEmpty(item.GradeLevel) && !string.IsNullOrEmpty(candidate.GradeLevel)
                && item.GradeLevel.Trim().Equals(candidate.GradeLevel.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (!string.IsNullOrEmpty(item.ClothingSize) && !string.IsNullOrEmpty(candidate.ClothingSize)
                && item.ClothingSize.Trim().Equals(candidate.ClothingSize.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 15;

            if (!string.IsNullOrEmpty(item.Subject) && !string.IsNullOrEmpty(candidate.Subject)
                && item.Subject.Trim().Equals(candidate.Subject.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 15;

            if (item.ItemType == candidate.ItemType)
                score += 10;

            return Math.Min(score, 100);
        }

        // ───────────────────────── UC08 — Reassign Unclaimed ─────────────────────────

        /// <summary>
        /// Finds confirmed allocations whose collection window has lapsed
        /// (scheduled date + grace period, with no pickup), releases the
        /// item back to the pool, frees the request back onto the waitlist,
        /// emails the learner, and immediately re-runs matching so the freed
        /// stock is reconsidered straight away. Returns how many were reassigned.
        /// </summary>
        public static int ReassignUnclaimedSweep()
        {
            var reassigned = 0;
            var emailService = new EmailService();

            using (var db = new ElevateEDContext())
            {
                var cutoff = DateTime.Now.AddDays(-CollectionGraceDays);

                var lapsed = db.DonationAllocations
                    .Include(a => a.Student)
                    .Include(a => a.Student.User)
                    .Include(a => a.DonationItem)
                    .Where(a => a.IsActive
                                && a.Status == "AwaitingCollection"
                                && a.ScheduledCollectionDate != null
                                && a.ScheduledCollectionDate < cutoff)
                    .ToList();

                foreach (var allocation in lapsed)
                {
                    allocation.Status = "Unclaimed";
                    allocation.IsActive = false;

                    if (allocation.RequestId.HasValue && allocation.DonationItem != null)
                    {
                        var requestItem = db.DonationRequestItems
                            .FirstOrDefault(i => i.DonationRequestId == allocation.RequestId.Value
                                                  && i.Category == allocation.DonationItem.Category
                                                  && i.Status == RequestItemStatus.Reserved);

                        if (requestItem != null)
                        {
                            requestItem.Status = RequestItemStatus.Pending;
                        }
                    }

                    db.DonationHistories.Add(new DonationHistory
                    {
                        DonationItemId = allocation.DonationItemId,
                        Action = "Unclaimed",
                        Description = $"Collection window (scheduled {allocation.ScheduledCollectionDate:dd MMM yyyy}) lapsed without pickup — item released back to the pool and the request re-queued.",
                        ActorType = "System",
                        ActionDate = DateTime.Now
                    });

                    var toEmail = allocation.Student?.User?.Email;
                    if (!string.IsNullOrEmpty(toEmail))
                    {
                        try
                        {
                            emailService.SendDonationUnclaimedEmail(
                                toEmail,
                                allocation.Student.FullName,
                                allocation.DonationItem?.ItemName ?? "your item");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Unclaimed-notice email failed: " + ex.Message);
                        }
                    }

                    reassigned++;
                }

                if (reassigned > 0) db.SaveChanges();
            }

            if (reassigned > 0)
            {
                RunMatchingSweep();
            }

            return reassigned;
        }
    }
}
