using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ElevateED.Models;
using Newtonsoft.Json;

namespace ElevateED.Services
{
    public class AIDonationMatcher
    {
        private readonly ElevateEDContext _context;

        // Common synonyms and variations for matching
        private static readonly Dictionary<string, List<string>> SynonymMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "math", new List<string> { "mathematics", "maths", "algebra", "calculus" } },
            { "mathematics", new List<string> { "math", "maths", "algebra", "calculus" } },
            { "maths", new List<string> { "math", "mathematics", "algebra", "calculus" } },
            { "physical science", new List<string> { "physics", "chemistry", "physical sciences" } },
            { "physics", new List<string> { "physical science", "physical sciences" } },
            { "chemistry", new List<string> { "physical science", "physical sciences" } },
            { "life science", new List<string> { "biology", "life sciences" } },
            { "biology", new List<string> { "life science", "life sciences" } },
            { "english", new List<string> { "language", "literature" } },
            { "history", new List<string> { "social studies", "heritage" } },
            { "geography", new List<string> { "earth science", "environmental" } },
            { "accounting", new List<string> { "account", "finance", "bookkeeping" } },
            { "economics", new List<string> { "finance", "business", "commerce" } },
            { "business", new List<string> { "commerce", "entrepreneurship", "economics" } },
            { "it", new List<string> { "information technology", "computer", "coding" } },
            { "computer", new List<string> { "it", "information technology", "coding" } },
            { "coding", new List<string> { "it", "computer", "programming" } },
            { "agriculture", new List<string> { "agricultural", "farming", "agri" } },
            { "creative arts", new List<string> { "art", "design", "drama", "music" } },
            { "shirt", new List<string> { "top", "blouse", "jersey" } },
            { "jersey", new List<string> { "shirt", "top", "jumper" } },
            { "trousers", new List<string> { "pants", "skirt" } },
            { "pants", new List<string> { "trousers", "skirt" } },
            { "skirt", new List<string> { "trousers", "pants" } },
            { "shoes", new List<string> { "footwear", "sneakers", "trainers" } },
            { "sneakers", new List<string> { "shoes", "footwear", "trainers" } },
            { "book", new List<string> { "textbook", "novel", "reader" } },
            { "textbook", new List<string> { "book", "text", "guide" } },
            { "pen", new List<string> { "stationery", "writing" } },
            { "pencil", new List<string> { "stationery", "writing" } },
            { "notebook", new List<string> { "stationery", "book", "pad" } }
        };

        private static readonly Dictionary<string, int> GradeMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "grade 8", 8 },
            { "grade 9", 9 },
            { "grade 10", 10 },
            { "grade 11", 11 },
            { "grade 12", 12 },
            { "gr 8", 8 },
            { "gr 9", 9 },
            { "gr 10", 10 },
            { "gr 11", 11 },
            { "gr 12", 12 },
            { "g8", 8 },
            { "g9", 9 },
            { "g10", 10 },
            { "g11", 11 },
            { "g12", 12 },
            { "8th", 8 },
            { "9th", 9 },
            { "10th", 10 },
            { "11th", 11 },
            { "12th", 12 }
        };

        public AIDonationMatcher(ElevateEDContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Process all pending request items and available donations through AI matching
        /// </summary>
        public async Task<List<AIMatchResult>> ProcessMatchingAsync()
        {
            var results = new List<AIMatchResult>();

            // ✅ Get all pending REQUEST ITEMS (not the parent requests)
            var requestItems = _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Where(i => i.DonationRequest.IsActive && !i.IsFulfilled && i.Status != RequestItemStatus.Declined)
                .ToList();

            // Get all available donations that are verified and have remaining quantity
            var donations = _context.DonationItems
                .Where(d => d.IsActive && d.Status == DonationStatus.Verified && d.QuantityRemaining > 0)
                .ToList();

            if (!requestItems.Any() || !donations.Any())
                return results;

            foreach (var requestItem in requestItems)
            {
                var requestKeywords = ExtractKeywords(requestItem);

                foreach (var donation in donations)
                {
                    var donationKeywords = ExtractKeywords(donation);
                    var matchScore = CalculateMatchScore(requestKeywords, donationKeywords, requestItem, donation);

                    if (matchScore >= 0.5m)
                    {
                        results.Add(new AIMatchResult
                        {
                            DonationItemId = donation.Id,
                            RequestId = requestItem.DonationRequestId,
                            MatchScore = matchScore,
                            MatchReason = GenerateMatchReason(requestKeywords, donationKeywords, requestItem, donation),
                            ExtractedKeywords = string.Join(", ", requestKeywords.Take(10)),
                            IsExactMatch = matchScore >= 0.9m
                        });
                    }
                }

                // Store top matches on the parent request
                var topMatches = results
                    .Where(r => r.RequestId == requestItem.DonationRequestId)
                    .OrderByDescending(r => r.MatchScore)
                    .Take(3)
                    .ToList();

                if (topMatches.Any() && requestItem.DonationRequest != null)
                {
                    requestItem.DonationRequest.AiRecommendedMatches = JsonConvert.SerializeObject(topMatches);
                    requestItem.DonationRequest.AiConfidenceScore = topMatches.First().MatchScore;
                    requestItem.DonationRequest.AiExtractedKeywords = string.Join(", ", requestKeywords.Take(10));
                    await _context.SaveChangesAsync();
                }
            }

            return results;
        }

        /// <summary>
        /// Find the best match for a specific request item
        /// </summary>
        public async Task<AIMatchResult> FindBestMatchAsync(int requestItemId)
        {
            var requestItem = await _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .FirstOrDefaultAsync(i => i.Id == requestItemId);

            if (requestItem == null || !requestItem.DonationRequest.IsActive || requestItem.IsFulfilled
                || requestItem.Status == RequestItemStatus.Declined)
                return null;

            var donations = _context.DonationItems
                .Where(d => d.IsActive && d.Status == DonationStatus.Verified && d.QuantityRemaining > 0)
                .ToList();

            if (!donations.Any())
                return null;

            var requestKeywords = ExtractKeywords(requestItem);
            AIMatchResult bestMatch = null;
            decimal bestScore = 0;

            foreach (var donation in donations)
            {
                var donationKeywords = ExtractKeywords(donation);
                var matchScore = CalculateMatchScore(requestKeywords, donationKeywords, requestItem, donation);

                if (matchScore > bestScore)
                {
                    bestScore = matchScore;
                    bestMatch = new AIMatchResult
                    {
                        DonationItemId = donation.Id,
                        RequestId = requestItem.DonationRequestId,
                        MatchScore = matchScore,
                        MatchReason = GenerateMatchReason(requestKeywords, donationKeywords, requestItem, donation),
                        ExtractedKeywords = string.Join(", ", requestKeywords.Take(10)),
                        IsExactMatch = matchScore >= 0.9m
                    };
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Extract keywords from a request item for AI matching
        /// </summary>
        public List<string> ExtractKeywords(DonationRequestItem item)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(item.ItemName))
                AddKeywords(keywords, item.ItemName);

            if (!string.IsNullOrEmpty(item.Subject))
                AddKeywords(keywords, item.Subject);

            if (!string.IsNullOrEmpty(item.GradeLevel))
            {
                var gradeNum = ExtractGradeNumber(item.GradeLevel);
                if (gradeNum.HasValue)
                    keywords.Add("grade" + gradeNum.Value);
                AddKeywords(keywords, item.GradeLevel);
            }

            if (!string.IsNullOrEmpty(item.BookTitle))
                AddKeywords(keywords, item.BookTitle);

            if (!string.IsNullOrEmpty(item.ClothingSize))
                AddKeywords(keywords, item.ClothingSize);

            if (!string.IsNullOrEmpty(item.ClothingType))
                AddKeywords(keywords, item.ClothingType);

            if (!string.IsNullOrEmpty(item.ISBN))
                keywords.Add(item.ISBN.Trim());

            if (!string.IsNullOrEmpty(item.Description))
                AddKeywords(keywords, item.Description);

            if (!string.IsNullOrEmpty(item.UrgencyReason))
                AddKeywords(keywords, item.UrgencyReason);

            if (!string.IsNullOrEmpty(item.StationeryType))
                AddKeywords(keywords, item.StationeryType);

            if (!string.IsNullOrEmpty(item.FoodType))
                AddKeywords(keywords, item.FoodType);

            if (!string.IsNullOrEmpty(item.ItemSubCategory))
                AddKeywords(keywords, item.ItemSubCategory);

            // Category and type
            keywords.Add(item.Category.ToString());
            keywords.Add(item.ItemType.ToString());

            return keywords.ToList();
        }

        /// <summary>
        /// Extract keywords from a donation item for AI matching
        /// </summary>
        public List<string> ExtractKeywords(DonationItem donation)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(donation.ItemName))
                AddKeywords(keywords, donation.ItemName);

            if (!string.IsNullOrEmpty(donation.Subject))
                AddKeywords(keywords, donation.Subject);

            if (!string.IsNullOrEmpty(donation.GradeLevel))
            {
                var gradeNum = ExtractGradeNumber(donation.GradeLevel);
                if (gradeNum.HasValue)
                    keywords.Add("grade" + gradeNum.Value);
                AddKeywords(keywords, donation.GradeLevel);
            }

            if (!string.IsNullOrEmpty(donation.BookTitle))
                AddKeywords(keywords, donation.BookTitle);

            if (!string.IsNullOrEmpty(donation.ClothingSize))
                AddKeywords(keywords, donation.ClothingSize);

            if (!string.IsNullOrEmpty(donation.ClothingType))
                AddKeywords(keywords, donation.ClothingType);

            if (!string.IsNullOrEmpty(donation.ISBN))
                keywords.Add(donation.ISBN.Trim());

            if (!string.IsNullOrEmpty(donation.Description))
                AddKeywords(keywords, donation.Description);

            // Category and type
            keywords.Add(donation.Category.ToString());
            keywords.Add(donation.ItemType.ToString());

            return keywords.ToList();
        }

        private void AddKeywords(HashSet<string> keywords, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var words = Regex.Split(text, @"[\s,\.;:!""']+")
                .Where(w => w.Length >= 3)
                .Select(w => w.Trim().ToLowerInvariant());

            foreach (var word in words)
            {
                keywords.Add(word);

                if (SynonymMap.ContainsKey(word))
                {
                    foreach (var synonym in SynonymMap[word])
                        keywords.Add(synonym);
                }

                foreach (var kvp in SynonymMap)
                {
                    foreach (var value in kvp.Value)
                    {
                        if (string.Equals(value, word, StringComparison.OrdinalIgnoreCase))
                        {
                            keywords.Add(kvp.Key);
                            break;
                        }
                    }
                }
            }
        }

        private int? ExtractGradeNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            foreach (var kvp in GradeMapping)
            {
                if (text.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }

            var match = Regex.Match(text, @"\b([89]|1[012])\b");
            if (match.Success)
                return int.Parse(match.Value);

            return null;
        }

        /// <summary>
        /// Calculate match score between a request item and donation
        /// </summary>
        private decimal CalculateMatchScore(
            List<string> requestKeywords,
            List<string> donationKeywords,
            DonationRequestItem requestItem,
            DonationItem donation)
        {
            decimal score = 0;

            // Category & Item type (highest weight)
            if (requestItem.Category == donation.Category) score += 0.3m;
            if (requestItem.ItemType == donation.ItemType) score += 0.2m;

            // Subject match
            if (!string.IsNullOrEmpty(requestItem.Subject) && !string.IsNullOrEmpty(donation.Subject))
            {
                if (IsSubjectMatch(requestItem.Subject, donation.Subject))
                    score += 0.2m;
            }

            // Grade level match
            if (!string.IsNullOrEmpty(requestItem.GradeLevel) && !string.IsNullOrEmpty(donation.GradeLevel))
            {
                var requestGrade = ExtractGradeNumber(requestItem.GradeLevel);
                var donationGrade = ExtractGradeNumber(donation.GradeLevel);
                if (requestGrade.HasValue && donationGrade.HasValue && requestGrade.Value == donationGrade.Value)
                    score += 0.15m;
            }

            // Keyword overlap
            var overlap = requestKeywords
                .Where(k => donationKeywords.Any(d => string.Equals(k, d, StringComparison.OrdinalIgnoreCase)))
                .Count();

            if (overlap > 0)
            {
                score += Math.Min(0.15m, (decimal)overlap / 5 * 0.15m);
            }

            // Clothing-specific matches
            if (requestItem.Category == DonationCategory.Clothing)
            {
                if (!string.IsNullOrEmpty(requestItem.ClothingSize) &&
                    !string.IsNullOrEmpty(donation.ClothingSize) &&
                    string.Equals(requestItem.ClothingSize, donation.ClothingSize, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.05m;
                }

                if (!string.IsNullOrEmpty(requestItem.Gender) &&
                    !string.IsNullOrEmpty(donation.Gender) &&
                    string.Equals(requestItem.Gender, donation.Gender, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.05m;
                }
            }

            return Math.Round(Math.Min(score, 1.0m), 2);
        }

        private bool IsSubjectMatch(string requestSubject, string donationSubject)
        {
            if (string.IsNullOrEmpty(requestSubject) || string.IsNullOrEmpty(donationSubject))
                return false;

            if (string.Equals(requestSubject, donationSubject, StringComparison.OrdinalIgnoreCase))
                return true;

            var requestLower = requestSubject.ToLowerInvariant();
            var donationLower = donationSubject.ToLowerInvariant();

            if (requestLower.Contains(donationLower) || donationLower.Contains(requestLower))
                return true;

            return false;
        }

        private string GenerateMatchReason(
            List<string> requestKeywords,
            List<string> donationKeywords,
            DonationRequestItem requestItem,
            DonationItem donation)
        {
            var reasons = new List<string>();

            if (requestItem.Category == donation.Category)
                reasons.Add($"Same category: {requestItem.Category}");

            if (requestItem.ItemType == donation.ItemType)
                reasons.Add($"Same item type: {requestItem.ItemType}");

            if (!string.IsNullOrEmpty(requestItem.Subject) && !string.IsNullOrEmpty(donation.Subject))
            {
                if (string.Equals(requestItem.Subject, donation.Subject, StringComparison.OrdinalIgnoreCase))
                    reasons.Add($"Subject match: {requestItem.Subject}");
            }

            if (!string.IsNullOrEmpty(requestItem.GradeLevel) && !string.IsNullOrEmpty(donation.GradeLevel))
            {
                var requestGrade = ExtractGradeNumber(requestItem.GradeLevel);
                var donationGrade = ExtractGradeNumber(donation.GradeLevel);
                if (requestGrade.HasValue && donationGrade.HasValue && requestGrade.Value == donationGrade.Value)
                    reasons.Add($"Same grade level: {requestGrade.Value}");
            }

            var overlap = requestKeywords
                .Where(k => donationKeywords.Any(d => string.Equals(k, d, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (overlap.Any())
                reasons.Add($"{overlap.Count} matching keywords: {string.Join(", ", overlap.Take(3))}");

            if (requestItem.Category == DonationCategory.Clothing)
            {
                if (!string.IsNullOrEmpty(requestItem.ClothingSize) &&
                    !string.IsNullOrEmpty(donation.ClothingSize) &&
                    string.Equals(requestItem.ClothingSize, donation.ClothingSize, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add($"Same size: {requestItem.ClothingSize}");
                }
            }

            if (reasons.Count == 0)
                reasons.Add("General category match");

            return string.Join("; ", reasons);
        }
    }
}