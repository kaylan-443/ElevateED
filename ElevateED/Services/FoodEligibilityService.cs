using System;
using System.Collections.Generic;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    public class FoodEligibilityService
    {
        private readonly ElevateEDContext _context;

        public FoodEligibilityService(ElevateEDContext context)
        {
            _context = context;
        }

        public FoodEligibilityResult CheckEligibility(FoodDonationCheck check)
        {
            var result = new FoodEligibilityResult();
            var issues = new List<string>();

            // 1. Check expiry
            if (check.IsExpired)
            {
                issues.Add("Food has expired");
                result.ExpiryPassed = false;
            }
            else if (check.IsExpiringSoon)
            {
                issues.Add("Food expires within 3 months");
                result.ExpiryPassed = true;
                result.Warnings.Add("Expires within 3 months");
            }
            else
            {
                result.ExpiryPassed = true;
            }

            // 2. Check packaging
            if (!check.PackagingSealed)
            {
                issues.Add("Packaging is not sealed");
                result.PackagingPassed = false;
            }
            else
            {
                result.PackagingPassed = true;
            }

            // 3. Check for damage
            if (!check.NoDamage)
            {
                issues.Add("Packaging has visible damage");
                result.PackagingPassed = false;
            }

            // 4. Check for bulging
            if (!check.NoBulging)
            {
                issues.Add("Cans have bulges or dents");
                result.PackagingPassed = false;
            }

            // 5. Check for pest damage
            if (!check.NoPestDamage)
            {
                issues.Add("Signs of pest damage detected");
                result.PackagingPassed = false;
            }

            // 6. Check appearance
            if (!check.AppearancePassed)
            {
                issues.Add("Appearance check failed");
                result.QualityPassed = false;
            }
            else
            {
                result.QualityPassed = true;
            }

            // 7. Check smell
            if (!check.SmellPassed)
            {
                issues.Add("Smell check failed");
                result.QualityPassed = false;
            }

            // 8. Check commercial source
            if (!check.IsCommercialSource)
            {
                issues.Add("Not from commercial source");
                result.CompliancePassed = false;
            }
            else
            {
                result.CompliancePassed = true;
            }

            result.AllChecksPassed = result.ExpiryPassed &&
                                     result.PackagingPassed &&
                                     result.QualityPassed &&
                                     result.CompliancePassed;

            result.Issues = issues;
            result.RejectionReason = string.Join("; ", issues);

            if (result.AllChecksPassed)
            {
                result.Status = FoodDonationStatus.Approved;
                result.Message = "Food donation approved";
            }
            else
            {
                result.Status = FoodDonationStatus.Rejected;
                result.Message = "Food donation rejected: " + result.RejectionReason;
            }

            return result;
        }

        public FoodDonationStats GetStats()
        {
            var foodItems = _context.DonationItems
                .Where(d => d.IsFoodItem && d.IsActive)
                .ToList();

            var checks = _context.FoodDonationChecks.ToList();

            int total = foodItems.Count;
            int pending = foodItems.Count(d => d.Status == DonationStatus.PendingVerification || d.Status == DonationStatus.PendingEligibilityCheck);
            int approved = checks.Count(c => c.Status == FoodDonationStatus.Approved);
            int rejected = checks.Count(c => c.Status == FoodDonationStatus.Rejected);
            int expired = checks.Count(c => c.Status == FoodDonationStatus.Expired);

            return new FoodDonationStats
            {
                TotalFoodItems = total,
                PendingEligibility = pending,
                Approved = approved,
                Rejected = rejected,
                Expired = expired
            };
        }
    }

    public class FoodEligibilityResult
    {
        public bool AllChecksPassed { get; set; }
        public bool ExpiryPassed { get; set; }
        public bool PackagingPassed { get; set; }
        public bool QualityPassed { get; set; }
        public bool CompliancePassed { get; set; }
        public FoodDonationStatus Status { get; set; }
        public string Message { get; set; }
        public string RejectionReason { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Warnings { get; set; }

        public FoodEligibilityResult()
        {
            Issues = new List<string>();
            Warnings = new List<string>();
            ExpiryPassed = true;
            PackagingPassed = true;
            QualityPassed = true;
            CompliancePassed = true;
        }
    }

    public class FoodDonationStats
    {
        public int TotalFoodItems { get; set; }
        public int PendingEligibility { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Expired { get; set; }
    }
}