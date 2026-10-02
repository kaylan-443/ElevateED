using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    // ────────────────────────────────────────────────────────────────
    //  Smart Donation Management System — automated engine.
    //
    //  RunMatchingSweep()      implements UC05 (Auto-Match Donations to
    //                          Requests): pairs the highest-priority
    //                          waitlisted request in each category with
    //                          the best-fit available item, and creates a
    //                          Pending Review proposal for admin sign-off
    //                          (UC06) rather than notifying anyone directly.
    //
    //  ReassignUnclaimedSweep() implements UC08 (Reassign Unclaimed
    //                          Donation): finds allocations whose
    //                          collection window has lapsed, returns the
    //                          item to Available, returns the request to
    //                          Waitlisted at its original position, and
    //                          immediately re-runs the matching sweep for
    //                          that category.
    //
    //  Both methods open their own short-lived ElevateEDContext so they
    //  can be called either from an HTTP request (the "Run Matching Now"
    //  admin button, UC05's triggering event) or from the background timer
    //  in Global.asax.cs (the scheduled run UC05/UC08 both describe).
    // ────────────────────────────────────────────────────────────────
    public class DonationAutomationEngine
    {
        // UC08 step 1 — "Collection Date plus grace period (e.g. 5 school days)".
        private static int UnclaimedGraceDays
        {
            get
            {
                var raw = System.Configuration.ConfigurationManager.AppSettings["DonationUnclaimedGraceDays"];
                int parsed;
                return int.TryParse(raw, out parsed) && parsed > 0 ? parsed : 5;
            }
        }

        public class MatchSweepResult
        {
            public int ProposalsCreated { get; set; }
            public List<string> Notes { get; set; } = new List<string>();
        }

        public class ReassignSweepResult
        {
            public int ItemsReassigned { get; set; }
            public List<string> Notes { get; set; } = new List<string>();
        }

        // ──────────────────────────────────────────────────────────
        //  UC05 — Auto-Match Donations to Requests
        // ──────────────────────────────────────────────────────────
        public MatchSweepResult RunMatchingSweep()
        {
            var result = new MatchSweepResult();

            using (var context = new ElevateEDContext())
            {
                foreach (DonationCategory category in Enum.GetValues(typeof(DonationCategory)))
                {
                    if (category == DonationCategory.Food) continue; // food subsystem retired

                    RunMatchingSweepForCategory(context, category, result);
                }
            }

            return result;
        }

        // Overload used by ReassignUnclaimedSweep so a just-freed item is
        // reconsidered immediately, inside the same context/transaction
        // scope as the reassignment that freed it.
        private void RunMatchingSweepForCategory(ElevateEDContext context, DonationCategory category, MatchSweepResult result)
        {
            // Step 1 — collect all Available items and all Waitlisted
            // requests in this category.
            var availableItems = context.DonationItems
                .Where(i => i.IsActive && i.Category == category && i.Status == DonationStatus.Available)
                .OrderBy(i => i.DonationDate) // oldest first, for the step-4 tiebreak
                .ToList();

            if (!availableItems.Any()) return;

            var waitlistedItems = context.DonationRequestItems
                .Include(ri => ri.DonationRequest)
                .Include(ri => ri.DonationRequest.Student)
                .Where(ri => ri.Category == category
                             && ri.Status == RequestItemStatus.Waitlisted
                             && ri.DonationRequest.IsActive)
                .ToList();

            if (!waitlistedItems.Any()) return;

            // Step 2 — order requests by Priority Score, highest first.
            var rankedRequests = waitlistedItems
                .OrderByDescending(ri => ri.DonationRequest.PriorityScore)
                .ThenByDescending(ri => ri.CalculateItemScore())
                .ToList();

            int totalInCategory = rankedRequests.Count;
            int rank = 0;

            // Steps 3-7 — attempt to pair the highest-priority request with
            // the best-fit item; repeat until no further valid pairs exist.
            foreach (var requestItem in rankedRequests)
            {
                rank++;
                if (!availableItems.Any()) break;

                var scored = availableItems
     .Select(candidate => new
     {
         Item = candidate,
         Fit = ScoreFit(requestItem, candidate)
     })
     .Where(x => x.Fit.Score > 0)
     .OrderByDescending(x => x.Fit.Score)
     .ThenBy(x => x.Item.DonationDate)
     .ToList();

                if (!scored.Any()) continue;

                var bestMatch = scored.First();
                var item = bestMatch.Item;

                // Step 5 — create a proposed Match record, Pending Review.
                var allocation = new DonationAllocation
                {
                    DonationItemId = item.Id,
                    StudentId = requestItem.DonationRequest.StudentId,
                    RequestId = requestItem.DonationRequestId,
                    QuantityAllocated = Math.Min(item.QuantityRemaining, requestItem.QuantityRequested),
                    Status = "PendingReview",
                    MatchScore = bestMatch.Fit.Score,
                    MatchReason = $"Priority {rank} of {totalInCategory} in {category} — {bestMatch.Fit.Reason}"
                };
                context.DonationAllocations.Add(allocation);

                // Step 6 — reserve the item so it can't be matched twice
                // while awaiting review.
                item.Status = DonationStatus.Reserved;
                requestItem.Status = RequestItemStatus.Reserved;

                context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = item.Id,
                    Action = "MatchProposed",
                    Description = allocation.MatchReason,
                    ActorType = "System",
                    AdditionalData = $"RequestItemId={requestItem.Id}"
                });

                availableItems.Remove(item);
                result.ProposalsCreated++;
                result.Notes.Add($"Proposed {item.ItemName} (#{item.Id}) → {requestItem.DonationRequest.Student?.FullName} ({allocation.MatchReason})");
            }

            context.SaveChanges();
        }

        private class FitScore
        {
            public int Score { get; set; }
            public string Reason { get; set; }
        }

        // Step 3 — fit scoring: category (already filtered), size/attribute
        // match, and item condition versus urgency.
        private static FitScore ScoreFit(DonationRequestItem request, DonationItem item)
        {
            if (item.QuantityRemaining <= 0) return new FitScore { Score = 0, Reason = "no stock remaining" };

            int score = 50; // base: same category
            var reasons = new List<string> { "category match" };

            if (!string.IsNullOrEmpty(request.ClothingSize) && !string.IsNullOrEmpty(item.ClothingSize)
                && string.Equals(request.ClothingSize, item.ClothingSize, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                reasons.Add("size match");
            }

            if (!string.IsNullOrEmpty(request.GradeLevel) && !string.IsNullOrEmpty(item.GradeLevel)
                && string.Equals(request.GradeLevel, item.GradeLevel, StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
                reasons.Add("grade match");
            }

            if (!string.IsNullOrEmpty(request.Subject) && !string.IsNullOrEmpty(item.Subject)
                && string.Equals(request.Subject, item.Subject, StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
                reasons.Add("subject match");
            }

            // Condition versus urgency: an Urgent request can still match a
            // Fair-condition item (getting something beats waiting longer
            // for a New one); a Low/Medium request weighs condition more.
            int conditionBonus = item.Condition == "New" ? 10 : item.Condition == "Good" ? 5 : 0;
            if (request.Priority == RequestPriority.Urgent || request.Priority == RequestPriority.High)
            {
                conditionBonus = Math.Min(conditionBonus, 5); // urgency outweighs holding out for condition
            }
            score += conditionBonus;

            return new FitScore { Score = score, Reason = string.Join(", ", reasons) };
        }

        // ──────────────────────────────────────────────────────────
        //  UC08 — Reassign Unclaimed Donation
        // ──────────────────────────────────────────────────────────
        public ReassignSweepResult ReassignUnclaimedSweep(EmailService emailService = null)
        {
            var result = new ReassignSweepResult();
            emailService = emailService ?? new EmailService();

            using (var context = new ElevateEDContext())
            {
                var cutoff = DateTime.Now.AddDays(-UnclaimedGraceDays);

                // Step 1 — all matches past their collection window.
                var lapsed = context.DonationAllocations
                    .Include(a => a.DonationItem)
                    .Include(a => a.Student)
                    .Include(a => a.Student.User)
                    .Include(a => a.Request)
                    .Where(a => a.IsActive
                                && a.Status == "AwaitingCollection"
                                && a.ScheduledCollectionDate.HasValue
                                && a.ScheduledCollectionDate.Value < cutoff)
                    .ToList();

                var touchedCategories = new HashSet<DonationCategory>();

                foreach (var allocation in lapsed)
                {
                    var item = allocation.DonationItem;
                    var requestItem = allocation.RequestId.HasValue
                        ? context.DonationRequestItems.FirstOrDefault(ri => ri.DonationRequestId == allocation.RequestId.Value
                                                                             && ri.Category == item.Category
                                                                             && ri.Status == RequestItemStatus.AwaitingCollection)
                        : null;

                    // Step 2 — mark Unclaimed, notify the learner.
                    allocation.Status = "Unclaimed";
                    allocation.UnclaimedDate = DateTime.Now;
                    allocation.IsActive = false;

                    // Step 4 — return the item to Available Inventory.
                    item.Status = DonationStatus.Available;

                    // Request returns to Waitlisted at its original rank —
                    // WaitlistedDate/PriorityScore are left untouched so its
                    // position in the queue isn't penalised (UC06's "no
                    // learner is penalised for an admin's correction"
                    // principle applies equally here).
                    if (requestItem != null)
                    {
                        requestItem.Status = RequestItemStatus.Waitlisted;
                    }

                    context.DonationHistories.Add(new DonationHistory
                    {
                        DonationItemId = item.Id,
                        Action = "Unclaimed",
                        Description = $"Collection window lapsed ({UnclaimedGraceDays} day grace period) — returned to Available pool.",
                        ActorType = "System",
                        AdditionalData = $"AllocationId={allocation.Id}"
                    });

                    touchedCategories.Add(item.Category);
                    result.ItemsReassigned++;
                    result.Notes.Add($"{item.ItemName} (#{item.Id}) unclaimed by {allocation.Student?.FullName} — returned to pool.");

                    try
                    {
                        var email = allocation.Student?.User?.Email;
                        if (!string.IsNullOrEmpty(email))
                            emailService.SendDonationUnclaimedEmail(email, allocation.Student.FullName, item.ItemName);
                    }
                    catch
                    {
                        // Never let a flaky SMTP server block the reassignment sweep.
                    }
                }

                context.SaveChanges();

                // Step 5 — immediately re-run matching for every touched
                // category so the freed item is reconsidered right away.
                foreach (var category in touchedCategories)
                {
                    var sweepResult = new MatchSweepResult();
                    RunMatchingSweepForCategory(context, category, sweepResult);
                    result.Notes.AddRange(sweepResult.Notes);
                }
            }

            return result;
        }
    }
}
