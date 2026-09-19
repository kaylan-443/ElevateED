using System;
using System.Collections.Generic;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    /// <summary>
    /// Rule-based food safety evaluator.
    /// Takes what the donor declared about the food and produces an
    /// automated safety decision — no human review required.
    /// </summary>
    public class FoodSafetyEvaluator
    {
        // How many months of shelf life must remain before we accept it?
        private const int MinimumMonthsRemaining = 3;

        public FoodSafetyDecision Evaluate(FoodDonationCheck check)
        {
            var decision = new FoodSafetyDecision();
            var reasons = new List<string>();
            var warnings = new List<string>();

            // --------------------------------------------------------
            // 1. Expiry
            // --------------------------------------------------------
            var today = DateTime.Now.Date;
            var expiry = check.ExpiryDate.Date;

            if (expiry <= today)
            {
                reasons.Add($"Expired on {expiry:dd MMM yyyy}.");
                decision.Expired = true;
            }
            else if (expiry <= today.AddMonths(MinimumMonthsRemaining))
            {
                // Not expired, but not enough shelf life left
                reasons.Add($"Only {(expiry - today).Days} days of shelf life remain " +
                            $"(minimum required: {MinimumMonthsRemaining} months).");
                decision.TooCloseToExpiry = true;
            }

            // --------------------------------------------------------
            // 2. Packaging integrity
            // --------------------------------------------------------
            if (!check.PackagingSealed)
                reasons.Add("Packaging is not sealed — food could be contaminated.");

            if (!check.NoDamage)
                reasons.Add("Packaging shows visible damage (tears, holes, dents).");

            if (!check.NoBulging)
                reasons.Add("Cans are bulging or dented — a sign of spoilage.");

            if (!check.NoPestDamage)
                reasons.Add("Signs of pest damage detected.");

            // --------------------------------------------------------
            // 3. Sensory checks
            // --------------------------------------------------------
            if (!check.AppearancePassed)
                reasons.Add("Appearance check failed (discoloration, mold, or visible spoilage).");

            if (!check.SmellPassed)
                reasons.Add("Smell check failed (off odours).");

            // --------------------------------------------------------
            // 4. Provenance / labelling
            // --------------------------------------------------------
            if (!check.IsCommercialSource)
                reasons.Add("Not from a commercial source — home-prepared food cannot be accepted.");

            if (!check.ProperlyLabeled)
                reasons.Add("Not properly labelled (missing ingredients or expiry date).");

            // Nutrition info is optional, so failing it produces a warning only.
            if (!check.NutritionInfoPresent)
                warnings.Add("Nutrition information is missing — accepted, but the student " +
                             "will not be able to review dietary details.");

            // --------------------------------------------------------
            // 5. Allergen risk
            // --------------------------------------------------------
            if (check.Allergens != FoodAllergen.None)
            {
                // We don't reject on allergens — we accept, but the student's
                // dietary requirements will be matched against the allergen later.
                // Note it clearly so the matching engine can enforce it.
                warnings.Add($"Contains declared allergens ({check.Allergens}). " +
                             $"Only students whose dietary profile allows this will be matched.");

                if (!string.IsNullOrWhiteSpace(check.AllergenDetails))
                    warnings.Add($"Allergen details: {check.AllergenDetails}");
            }

            // --------------------------------------------------------
            // 6. Quality grade (donor-declared)
            // --------------------------------------------------------
            if (string.Equals(check.QualityGrade, "Poor", StringComparison.OrdinalIgnoreCase))
                reasons.Add("Donor declared quality as 'Poor'.");

            // --------------------------------------------------------
            // Build the final decision
            // --------------------------------------------------------
            var isSafe = reasons.Count == 0 && !decision.Expired && !decision.TooCloseToExpiry;

            if (decision.Expired)
            {
                decision.Status = FoodDonationStatus.Expired;
                decision.Summary = "Rejected automatically — food has expired.";
            }
            else if (!isSafe)
            {
                decision.Status = FoodDonationStatus.Rejected;
                decision.Summary = "Rejected automatically — one or more safety rules failed.";
            }
            else
            {
                decision.Status = FoodDonationStatus.Approved;
                decision.Summary = "Approved automatically — all safety rules passed.";
            }

            decision.Reasons = reasons;
            decision.Warnings = warnings;
            return decision;
        }
    }

    public class FoodSafetyDecision
    {
        public FoodDonationStatus Status { get; set; }
        public string Summary { get; set; }
        public List<string> Reasons { get; set; }
        public List<string> Warnings { get; set; }
        public bool Expired { get; set; }
        public bool TooCloseToExpiry { get; set; }

        public FoodSafetyDecision()
        {
            Reasons = new List<string>();
            Warnings = new List<string>();
        }

        public bool IsSafe => Status == FoodDonationStatus.Approved;
    }
}