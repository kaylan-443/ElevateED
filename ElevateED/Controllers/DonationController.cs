using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ElevateED.Filters;
using ElevateED.Models;
using ElevateED.Services;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    [Authorize]
    public class DonationController : Controller
    {
        private readonly ElevateEDContext _context = new ElevateEDContext();
        private AIDonationMatcher _aiMatcher;
        private FoodEligibilityService _foodService;
        private FoodSafetyEvaluator _foodSafetyEvaluator;
        private FoodAllocationEngine _foodAllocationEngine;

        public DonationController()
        {
            _aiMatcher = new AIDonationMatcher(_context);
            _foodService = new FoodEligibilityService(_context);
            _foodSafetyEvaluator = new FoodSafetyEvaluator();
            _foodAllocationEngine = new FoodAllocationEngine();
        }

        #region Helper Methods

        private Student GetCurrentStudent()
        {
            var username = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Email == username || u.StudentNumber == username);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.User)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

        private int GetCurrentPersonId()
        {
            if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                return student?.Id ?? 0;
            }
            else
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
                return user?.Id ?? 0;
            }
        }

        private string GetCurrentPersonType()
        {
            if (User.IsInRole("Student")) return "Student";
            if (User.IsInRole("Teacher")) return "Teacher";
            return "Admin";
        }

        private string GetCurrentPersonName()
        {
            if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                return student?.FullName ?? "Guest";
            }
            else
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
                if (user == null) return "Guest";
                var teacher = _context.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                return teacher?.FullName ?? user.Email ?? "Guest";
            }
        }

        private string GetCurrentPersonEmail()
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
            return user?.Email ?? User.Identity.Name;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            return DateTime.TryParse(s, out d) ? d : (DateTime?)null;
        }

        private static bool ParseBool(string s)
        {
            return !string.IsNullOrWhiteSpace(s) &&
                   (s == "true" || s == "True" || s == "on" || s == "1");
        }

        private static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            T result;
            return !string.IsNullOrWhiteSpace(s) && Enum.TryParse(s, out result) ? result : fallback;
        }

        #endregion

        #region Index (Redirect)

        public ActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        #endregion

        #region Dashboard

        public async Task<ActionResult> Dashboard()
        {
            var viewModel = new DonationDashboardViewModel();

            if (User.IsInRole("Admin") || User.IsInRole("Teacher"))
            {
                viewModel.TotalRequests = _context.DonationRequests.Count(r => r.IsActive);
                viewModel.PendingRequests = _context.DonationRequests.Count(r => r.IsActive && !r.IsFulfilled);
                viewModel.FulfilledRequests = _context.DonationRequests.Count(r => r.IsActive && r.IsFulfilled);
                viewModel.TotalDonations = _context.DonationItems.Count(d => d.IsActive);
                viewModel.PendingVerifications = _context.DonationItems.Count(d => d.Status == DonationStatus.PendingVerification && d.IsActive);
                viewModel.AvailableDonations = _context.DonationItems.Count(d => d.Status == DonationStatus.Verified && d.QuantityRemaining > 0);
                viewModel.TotalAllocations = _context.DonationAllocations.Count(a => a.IsActive);
                viewModel.PendingCollection = _context.DonationAllocations.Count(a => a.Status == "Pending" && a.IsActive);

                viewModel.PendingFoodEligibility = 0;
                viewModel.ApprovedFoodItems = _context.FoodDonationChecks.Count(f => f.Status == FoodDonationStatus.Approved);
                viewModel.RejectedFoodItems = _context.FoodDonationChecks.Count(f => f.Status == FoodDonationStatus.Rejected);
                viewModel.ExpiredFoodItems = _context.FoodDonationChecks.Count(f => f.Status == FoodDonationStatus.Expired);

                viewModel.RecentRequests = await _context.DonationRequests
                    .Include(r => r.Student)
                    .Include(r => r.Items)
                    .Where(r => r.IsActive)
                    .OrderByDescending(r => r.RequestDate)
                    .Take(10)
                    .ToListAsync();

                viewModel.RecentDonations = await _context.DonationItems
                    .Include(d => d.FoodChecks)
                    .Where(d => d.IsActive)
                    .OrderByDescending(d => d.DonationDate)
                    .Take(10)
                    .ToListAsync();

                viewModel.RecentAllocations = await _context.DonationAllocations
                    .Include(a => a.Student)
                    .Include(a => a.DonationItem)
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.AllocationDate)
                    .Take(10)
                    .ToListAsync();

                viewModel.ActiveCampaigns = await _context.DonationCampaigns
                    .Where(c => c.Status == CampaignStatus.Active && c.IsActive)
                    .OrderBy(c => c.EndDate)
                    .Take(5)
                    .ToListAsync();

                ViewBag.AwaitingDeliveryCount = _context.DonationItems
                    .Count(d => d.Status == DonationStatus.AwaitingDelivery && d.IsActive);

                ViewBag.FoodReadyToAllocate = _context.DonationItems
                    .Count(d => d.IsActive
                                && d.IsFoodItem
                                && d.Status == DonationStatus.Verified
                                && d.QuantityRemaining > 0);
            }
            else if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                if (student != null)
                {
                    viewModel.TotalRequests = _context.DonationRequests.Count(r => r.StudentId == student.Id && r.IsActive);
                    viewModel.PendingRequests = _context.DonationRequests.Count(r => r.StudentId == student.Id && r.IsActive && !r.IsFulfilled);
                    viewModel.FulfilledRequests = _context.DonationRequests.Count(r => r.StudentId == student.Id && r.IsActive && r.IsFulfilled);
                    viewModel.TotalAllocations = _context.DonationAllocations.Count(a => a.StudentId == student.Id && a.IsActive);
                    viewModel.PendingCollection = _context.DonationAllocations.Count(a => a.StudentId == student.Id && a.Status == "Pending" && a.IsActive);

                    viewModel.RecentRequests = await _context.DonationRequests
                        .Include(r => r.Student)
                        .Include(r => r.Items)
                        .Where(r => r.StudentId == student.Id && r.IsActive)
                        .OrderByDescending(r => r.RequestDate)
                        .Take(10)
                        .ToListAsync();

                    ViewBag.AwaitingDeliveryCount = _context.DonationItems
                        .Count(d => d.DonorId == student.Id
                                    && d.Status == DonationStatus.AwaitingDelivery
                                    && d.IsActive);
                }
            }

            ViewBag.UserRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Teacher") ? "Teacher" : "Student";
            return View(viewModel);
        }

        #endregion

        #region Request Donation (Multi-Item)

        public ActionResult RequestDonation()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            ViewBag.StudentName = student.FullName;
            ViewBag.StudentNumber = student.User?.StudentNumber;
            ViewBag.Grade = student.Grade;
            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).Select(s => s.Name).ToList();

            return View(new DonationRequestViewModel());
        }

        // POST — receives a standard form post. Uses FormCollection (not a
        // bound model) so the multi-item payload can't be lost — the same
        // technique that fixed Add Donation.
        //
        // Food items are treated specially: the student only registers a
        // need, so we normalise the item name, quantity, and type here.
        // The allocation engine decides the actual amount later.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RequestDonation(FormCollection form)
        {
            try
            {
                var student = GetCurrentStudent();
                if (student == null)
                {
                    TempData["ErrorMessage"] = "Student not found. Please log in again.";
                    return RedirectToAction("RequestDonation");
                }

                int itemCount;
                if (!int.TryParse(form["itemCount"], out itemCount) || itemCount < 1)
                {
                    TempData["ErrorMessage"] = "Please add at least one item before submitting.";
                    return RedirectToAction("RequestDonation");
                }

                var request = new DonationRequest
                {
                    StudentId = student.Id,
                    RequestDate = DateTime.Now,
                    IsActive = true,
                    IsFulfilled = false
                };

                int savedItems = 0;

                for (int i = 0; i < itemCount; i++)
                {
                    var prefix = "Items[" + i + "].";

                    var categoryStr = form[prefix + "Category"];
                    var itemTypeStr = form[prefix + "ItemType"];
                    var itemName = form[prefix + "ItemName"];
                    var quantityStr = form[prefix + "QuantityRequested"];
                    var priorityStr = form[prefix + "Priority"];

                    if (string.IsNullOrWhiteSpace(categoryStr))
                        continue;

                    DonationCategory category;
                    DonationItemType itemType;
                    RequestPriority priority;
                    int quantity;

                    if (!Enum.TryParse(categoryStr, out category)) continue;
                    if (!Enum.TryParse(itemTypeStr, out itemType)) itemType = DonationItemType.Other;
                    if (!Enum.TryParse(priorityStr, out priority)) priority = RequestPriority.Medium;
                    if (!int.TryParse(quantityStr, out quantity) || quantity < 1) quantity = 1;

                    // ── Food special case ─────────────────────────────────
                    // The student only registers a need. We override the
                    // item name, type, and quantity. The allocation engine
                    // decides how much food they actually get, when
                    // donations are matched.
                    if (category == DonationCategory.Food)
                    {
                        itemName = "Food assistance";
                        itemType = DonationItemType.Food;
                        quantity = 1;    // placeholder — engine decides the real amount
                    }
                    else
                    {
                        // Non-food: must have an item name.
                        if (string.IsNullOrWhiteSpace(itemName))
                            continue;
                    }

                    var item = new DonationRequestItem
                    {
                        Category = category,
                        ItemType = itemType,
                        ItemName = itemName,
                        Description = form[prefix + "Description"],
                        BookTitle = form[prefix + "BookTitle"],
                        Subject = form[prefix + "Subject"],
                        GradeLevel = form[prefix + "GradeLevel"],
                        ISBN = form[prefix + "ISBN"],
                        ClothingSize = form[prefix + "ClothingSize"],
                        ClothingType = form[prefix + "ClothingType"],
                        Gender = form[prefix + "Gender"],
                        StationeryType = form[prefix + "StationeryType"],
                        BrandPreference = form[prefix + "BrandPreference"],
                        FoodType = form[prefix + "FoodType"],
                        DietaryRequirements = form[prefix + "DietaryRequirements"],
                        ItemSubCategory = form[prefix + "ItemSubCategory"],
                        SizeSpecifications = form[prefix + "SizeSpecifications"],
                        QuantityRequested = quantity,
                        QuantityReceived = 0,
                        Priority = priority,
                        UrgencyReason = form[prefix + "UrgencyReason"],
                        IsFulfilled = false,
                        CreatedAt = DateTime.Now
                    };

                    request.Items.Add(item);
                    savedItems++;
                }

                if (savedItems == 0)
                {
                    TempData["ErrorMessage"] = "No valid items were submitted.";
                    return RedirectToAction("RequestDonation");
                }

                request.PriorityScore = request.CalculatePriorityScore();

                _context.DonationRequests.Add(request);
                await _context.SaveChangesAsync();

                var waitlistPos = _context.DonationRequests
                    .Count(r => r.IsActive
                                && !r.IsFulfilled
                                && r.PriorityScore > request.PriorityScore
                                && r.Id != request.Id) + 1;

                request.WaitListPosition = waitlistPos;
                request.WaitlistedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                try { await _aiMatcher.ProcessMatchingAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("AI matching error: " + ex.Message); }

                TempData["SuccessMessage"] =
                    $"Thank you! Your request with {savedItems} item(s) has been submitted. " +
                    $"You are at position #{waitlistPos} on the waitlist.";

                return RedirectToAction("MyRequests");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                TempData["ErrorMessage"] = "Error saving request: " + innerMsg;
                return RedirectToAction("RequestDonation");
            }
        }

        public ActionResult MyRequests()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var requests = _context.DonationRequests
                .Include(r => r.Student)
                .Include(r => r.Items)
                .Include(r => r.Allocations.Select(a => a.DonationItem))
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.RequestDate)
                .ToList();

            // Per-item tracking info for the "Track" modal. DonationAllocation
            // links to a request as a whole (not a specific item), so we match
            // an item to its best allocation by category — good enough since a
            // request rarely has two open items in the same category at once.
            var tracking = new Dictionary<int, object>();
            foreach (var request in requests)
            {
                if (request.Items == null) continue;

                foreach (var item in request.Items)
                {
                    var matchingAllocation = request.Allocations != null
                        ? request.Allocations
                            .Where(a => a.IsActive && a.DonationItem != null && a.DonationItem.Category == item.Category)
                            .OrderByDescending(a => a.AllocationDate)
                            .FirstOrDefault()
                        : null;

                    tracking[item.Id] = new
                    {
                        status = item.Status.ToString(),
                        declineReason = item.DeclineReason,
                        declinedDate = item.DeclinedDate.HasValue ? item.DeclinedDate.Value.ToString("dd MMM yyyy") : null,
                        allocationStatus = matchingAllocation?.Status,
                        scheduledDate = matchingAllocation != null && matchingAllocation.ScheduledCollectionDate.HasValue
                            ? matchingAllocation.ScheduledCollectionDate.Value.ToString("dd MMM yyyy")
                            : null,
                        collectedDate = matchingAllocation != null && matchingAllocation.CollectionDate.HasValue
                            ? matchingAllocation.CollectionDate.Value.ToString("dd MMM yyyy")
                            : null,
                        pinCode = matchingAllocation != null && matchingAllocation.Status == "Ready" ? matchingAllocation.PinCode : null,
                        quantityRequested = item.QuantityRequested,
                        quantityReceived = item.QuantityReceived
                    };
                }
            }

            ViewBag.TrackingDataJson = JsonConvert.SerializeObject(tracking);

            return View(requests);
        }

        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> CancelRequest(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Unauthorized" });

            var request = await _context.DonationRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == student.Id);

            if (request == null)
                return Json(new { success = false, message = "Request not found" });

            if (request.IsFulfilled)
                return Json(new { success = false, message = "Cannot cancel a fulfilled request" });

            request.IsActive = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Request cancelled successfully" });
        }

        // Student-facing collection tracker: every donation allocated to
        // them, with the collection token/PIN surfaced once it's Ready for
        // pickup and a record of what's already been collected.
        public async Task<ActionResult> MyCollections()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var allocations = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Where(a => a.StudentId == student.Id && a.IsActive)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            var viewModel = allocations.Select(a => new DonationDistributionViewModel
            {
                AllocationId = a.Id,
                StudentName = student.FullName,
                StudentNumber = student.User?.StudentNumber,
                ItemName = a.DonationItem?.ItemName,
                ItemType = a.DonationItem?.ItemType.ToString(),
                Quantity = a.QuantityAllocated,
                CollectionToken = a.CollectionToken,
                QRCode = a.QRCode,
                PinCode = a.PinCode,
                ScheduledDate = a.ScheduledCollectionDate,
                Status = a.Status,
                IsFoodItem = a.DonationItem?.IsFoodItem ?? false
            }).ToList();

            return View(viewModel);
        }

        #endregion

        #region Add Donation (Multi-Item, with automatic food safety evaluation)

        [HttpGet]
        public ActionResult AddDonation()
        {
            ViewBag.DonorName = GetCurrentPersonName();

            ViewBag.Subjects = _context.Subjects
                .OrderBy(s => s.Name)
                .Select(s => s.Name)
                .ToList();

            ViewBag.NeedsCount = _context.DonationRequestItems
                .Count(i => i.DonationRequest.IsActive && !i.IsFulfilled);

            ViewBag.PublicNeeds = _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Where(i => i.DonationRequest.IsActive && !i.IsFulfilled)
                .OrderByDescending(i => i.DonationRequest.PriorityScore)
                .Take(10)
                .ToList()
                .Select(i =>
                    $"{i.QuantityRequested} {i.ItemName} needed ({i.Category})" +
                    (string.IsNullOrEmpty(i.GradeLevel) ? "" : $" for {i.GradeLevel}"))
                .ToList();

            return View(new DonationItemFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddDonation(FormCollection form)
        {
            try
            {
                int itemCount;
                if (!int.TryParse(form["itemCount"], out itemCount) || itemCount < 1)
                {
                    TempData["ErrorMessage"] = "Please add at least one item before submitting.";
                    return RedirectToAction("AddDonation");
                }

                var personId = GetCurrentPersonId();
                var personType = GetCurrentPersonType();
                var personName = GetCurrentPersonName();
                var personEmail = GetCurrentPersonEmail();

                if (personId == 0)
                {
                    TempData["ErrorMessage"] = "Could not identify the current user. Please log in again.";
                    return RedirectToAction("AddDonation");
                }

                int savedCount = 0;
                int autoApprovedFood = 0;
                int autoRejectedFood = 0;
                var trackingCodesAwaiting = new List<string>();
                var trackingCodesRejected = new List<string>();

                for (int i = 0; i < itemCount; i++)
                {
                    var prefix = "Items[" + i + "].";

                    var categoryStr = form[prefix + "Category"];
                    var itemTypeStr = form[prefix + "ItemType"];
                    var itemName = form[prefix + "ItemName"];
                    var quantityStr = form[prefix + "Quantity"];
                    var condition = form[prefix + "Condition"];

                    if (string.IsNullOrWhiteSpace(itemName) ||
                        string.IsNullOrWhiteSpace(categoryStr) ||
                        string.IsNullOrWhiteSpace(condition))
                        continue;

                    DonationCategory category;
                    DonationItemType itemType;
                    int quantity;

                    if (!Enum.TryParse(categoryStr, out category)) continue;
                    if (!Enum.TryParse(itemTypeStr, out itemType)) itemType = DonationItemType.Other;
                    if (!int.TryParse(quantityStr, out quantity) || quantity < 1) quantity = 1;

                    var donation = new DonationItem
                    {
                        DonorId = personId,
                        DonorType = personType,
                        DonorName = personName,
                        DonorEmail = personEmail,
                        Category = category,
                        ItemType = itemType,
                        ItemName = itemName,
                        BookTitle = form[prefix + "BookTitle"],
                        Subject = form[prefix + "Subject"],
                        GradeLevel = form[prefix + "GradeLevel"],
                        ISBN = form[prefix + "ISBN"],
                        ClothingSize = form[prefix + "ClothingSize"],
                        ClothingType = form[prefix + "ClothingType"],
                        Gender = form[prefix + "Gender"],
                        Quantity = quantity,
                        QuantityRemaining = quantity,
                        AllocationType = AllocationType.OpenDonation,
                        Condition = condition,
                        ConditionNotes = form[prefix + "ConditionNotes"],
                        Status = DonationStatus.AwaitingDelivery,
                        DonationDate = DateTime.Now,
                        IsActive = true,
                        IsFoodItem = category == DonationCategory.Food
                    };

                    _context.DonationItems.Add(donation);
                    await _context.SaveChangesAsync();

                    if (donation.IsFoodItem)
                    {
                        var foodCheck = new FoodDonationCheck
                        {
                            DonationItemId = donation.Id,
                            ExpiryDate = ParseDate(form[prefix + "FoodExpiryDate"]) ?? DateTime.Now.AddMonths(6),
                            ProductionDate = ParseDate(form[prefix + "FoodProductionDate"]),
                            StorageType = ParseEnum<FoodStorageType>(form[prefix + "FoodStorageType"], FoodStorageType.ShelfStable),
                            StorageNotes = form[prefix + "FoodStorageNotes"],
                            Allergens = ParseEnum<FoodAllergen>(form[prefix + "FoodAllergens"], FoodAllergen.None),
                            AllergenDetails = form[prefix + "FoodAllergenDetails"],
                            BatchNumber = form[prefix + "FoodBatchNumber"],
                            QualityGrade = form[prefix + "FoodQualityGrade"] ?? "Good",
                            PackagingSealed = ParseBool(form[prefix + "FoodPackagingSealed"]),
                            NoDamage = ParseBool(form[prefix + "FoodNoDamage"]),
                            NoBulging = ParseBool(form[prefix + "FoodNoBulging"]),
                            NoPestDamage = ParseBool(form[prefix + "FoodNoPestDamage"]),
                            AppearancePassed = ParseBool(form[prefix + "FoodAppearancePassed"]),
                            SmellPassed = ParseBool(form[prefix + "FoodSmellPassed"]),
                            IsCommercialSource = ParseBool(form[prefix + "FoodIsCommercialSource"]),
                            ProperlyLabeled = ParseBool(form[prefix + "FoodProperlyLabeled"]),
                            NutritionInfoPresent = ParseBool(form[prefix + "FoodNutritionInfoPresent"]),
                            CheckedAt = DateTime.Now,
                            CheckerName = "System (automatic)"
                        };

                        var decision = _foodSafetyEvaluator.Evaluate(foodCheck);

                        foodCheck.Status = decision.Status;
                        foodCheck.RejectionReason = decision.IsSafe
                            ? null
                            : string.Join(" ", decision.Reasons);

                        _context.FoodDonationChecks.Add(foodCheck);

                        if (decision.Status == FoodDonationStatus.Approved)
                        {
                            donation.Status = DonationStatus.AwaitingDelivery;
                            autoApprovedFood++;
                            trackingCodesAwaiting.Add(donation.TrackingCode);

                            _context.DonationHistories.Add(new DonationHistory
                            {
                                DonationItemId = donation.Id,
                                Action = "FoodSafetyDecision",
                                Description = decision.Summary,
                                ActorType = "System",
                                ActorId = personId,
                                ActionDate = DateTime.Now
                            });
                        }
                        else
                        {
                            donation.Status = decision.Status == FoodDonationStatus.Expired
                                ? DonationStatus.Expired
                                : DonationStatus.Rejected;
                            donation.IsActive = false;
                            autoRejectedFood++;
                            trackingCodesRejected.Add(donation.TrackingCode);

                            _context.DonationHistories.Add(new DonationHistory
                            {
                                DonationItemId = donation.Id,
                                Action = "FoodSafetyDecision",
                                Description = decision.Summary +
                                              (decision.Reasons.Any() ? " " + string.Join(" ", decision.Reasons) : ""),
                                ActorType = "System",
                                ActorId = personId,
                                ActionDate = DateTime.Now
                            });
                        }

                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        trackingCodesAwaiting.Add(donation.TrackingCode);

                        _context.DonationHistories.Add(new DonationHistory
                        {
                            DonationItemId = donation.Id,
                            Action = "DonationCreated",
                            Description = $"{personName} submitted {quantity} {itemName}(s). " +
                                          $"Tracking code: {donation.TrackingCode}. Awaiting delivery confirmation.",
                            ActorId = personId,
                            ActorType = personType,
                            AdditionalData = JsonConvert.SerializeObject(new { DonationId = donation.Id, TrackingCode = donation.TrackingCode })
                        });
                        await _context.SaveChangesAsync();
                    }

                    savedCount++;
                }

                if (savedCount == 0)
                {
                    TempData["ErrorMessage"] = "No valid items were submitted.";
                    return RedirectToAction("AddDonation");
                }

                var parts = new List<string>();
                parts.Add($"Thank you! {savedCount} donation(s) submitted.");

                if (autoApprovedFood > 0)
                    parts.Add($"{autoApprovedFood} food item(s) passed the automatic safety check and are ready for delivery.");

                if (autoRejectedFood > 0)
                    parts.Add($"{autoRejectedFood} food item(s) were auto-rejected and will not be delivered.");

                if (trackingCodesAwaiting.Any())
                    parts.Add("Please deliver the item(s) to the school and confirm delivery using the tracking code(s).");

                TempData["SuccessMessage"] = string.Join(" ", parts);
                TempData["TrackingCodesToConfirm"] = string.Join(", ", trackingCodesAwaiting);

                if (trackingCodesRejected.Any())
                {
                    TempData["RejectedTrackingCodes"] = string.Join(", ", trackingCodesRejected);
                }

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                TempData["ErrorMessage"] = "Error saving donation: " + innerMsg;
                return RedirectToAction("AddDonation");
            }
        }

        #endregion

        #region Delivery Confirmation

        [HttpGet]
        public ActionResult ConfirmDelivery(string trackingCode)
        {
            ViewBag.InitialTrackingCode = trackingCode ?? "";
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> CheckDeliveryProximity(
            string trackingCode,
            double? lat,
            double? lng)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(trackingCode))
                {
                    return Json(new
                    {
                        success = false,
                        stage = "no-code",
                        message = "Enter your tracking code to begin."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                if (!lat.HasValue || !lng.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "no-gps",
                        message = "Waiting for your GPS location."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                var code = trackingCode.Trim().ToUpperInvariant();

                var donation = await _context.DonationItems
                    .FirstOrDefaultAsync(d => d.TrackingCode == code && d.IsActive);

                if (donation == null)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "invalid-code",
                        message = "No active donation found with that tracking code."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                if (donation.Status == DonationStatus.Rejected)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "rejected",
                        message = "This donation was auto-rejected by the food safety check."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                if (donation.Status == DonationStatus.Expired)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "expired",
                        message = "This donation has been marked expired."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                if (donation.Status != DonationStatus.AwaitingDelivery)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "already-processed",
                        message = $"This donation has already been processed (status: {donation.Status})."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                var personId = GetCurrentPersonId();
                var isAdminOrTeacher = User.IsInRole("Admin") || User.IsInRole("Teacher");
                if (donation.DonorId != personId && !isAdminOrTeacher)
                {
                    return Json(new
                    {
                        success = false,
                        stage = "forbidden",
                        message = "You are not allowed to confirm delivery of this donation."
                    },
                        JsonRequestBehavior.AllowGet);
                }

                var distanceMeters = SchoolLocation.DistanceInMeters(
                    lat.Value, lng.Value,
                    SchoolLocation.Latitude, SchoolLocation.Longitude);

                var distanceDisplay = distanceMeters < 1000
                    ? $"{distanceMeters:F0} m"
                    : $"{(distanceMeters / 1000):F2} km";

                var inside = distanceMeters <= SchoolLocation.RadiusMeters;

                return Json(new
                {
                    success = true,
                    stage = inside ? "inside" : "outside",
                    insideGeofence = inside,
                    distanceMeters = Math.Round(distanceMeters, 1),
                    distanceDisplay = distanceDisplay,
                    radiusMeters = SchoolLocation.RadiusMeters,
                    itemName = donation.ItemName,
                    trackingCode = donation.TrackingCode,
                    message = inside
                        ? $"You are {distanceDisplay} from the school. You're within " +
                          $"{SchoolLocation.RadiusMeters} m — you can confirm delivery."
                        : $"You appear to be {distanceDisplay} away from the school. " +
                          $"Delivery can only be confirmed within {SchoolLocation.RadiusMeters} m."
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    stage = "error",
                    message = "Server error: " + ex.Message
                },
                    JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConfirmDeliverySubmit(FormCollection form)
        {
            try
            {
                var trackingCode = form["trackingCode"];
                var deliveryNotes = form["deliveryNotes"];
                var latStr = form["deliveryLatitude"];
                var lngStr = form["deliveryLongitude"];

                double? lat = null;
                double? lng = null;

                if (!string.IsNullOrWhiteSpace(latStr))
                {
                    double parsed;
                    if (double.TryParse(latStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        lat = parsed;
                    }
                }

                if (!string.IsNullOrWhiteSpace(lngStr))
                {
                    double parsed;
                    if (double.TryParse(lngStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        lng = parsed;
                    }
                }

                if (string.IsNullOrWhiteSpace(trackingCode))
                {
                    TempData["ErrorMessage"] = "Please enter the tracking code.";
                    return RedirectToAction("ConfirmDelivery");
                }

                if (!lat.HasValue || !lng.HasValue)
                {
                    TempData["ErrorMessage"] =
                        "We couldn't read your GPS location. Please allow location access in your browser and try again.";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                var code = trackingCode.Trim().ToUpperInvariant();

                var donation = await _context.DonationItems
                    .Include(d => d.FoodChecks)
                    .FirstOrDefaultAsync(d => d.TrackingCode == code && d.IsActive);

                if (donation == null)
                {
                    TempData["ErrorMessage"] = "No active donation found with that tracking code.";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                if (donation.Status == DonationStatus.Rejected)
                {
                    TempData["ErrorMessage"] =
                        "This donation was auto-rejected by the food safety check and cannot be delivered.";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                if (donation.Status == DonationStatus.Expired)
                {
                    TempData["ErrorMessage"] =
                        "This donation has been marked expired and cannot be delivered.";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                if (donation.Status != DonationStatus.AwaitingDelivery)
                {
                    TempData["ErrorMessage"] =
                        $"This donation has already been processed (current status: {donation.Status}).";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                var personId = GetCurrentPersonId();
                var personName = GetCurrentPersonName();
                var isAdminOrTeacher = User.IsInRole("Admin") || User.IsInRole("Teacher");

                if (donation.DonorId != personId && !isAdminOrTeacher)
                {
                    TempData["ErrorMessage"] = "You are not allowed to confirm delivery of this donation.";
                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                var distanceMeters = SchoolLocation.DistanceInMeters(
                    lat.Value, lng.Value,
                    SchoolLocation.Latitude, SchoolLocation.Longitude);

                var distanceDisplay = distanceMeters < 1000
                    ? $"{distanceMeters:F0} m"
                    : $"{(distanceMeters / 1000):F2} km";

                if (distanceMeters > SchoolLocation.RadiusMeters)
                {
                    TempData["ErrorMessage"] =
                        $"You appear to be {distanceDisplay} away from the school. " +
                        $"Delivery can only be confirmed within {SchoolLocation.RadiusMeters} m. " +
                        $"Please try again once you're at the school.";

                    _context.DonationHistories.Add(new DonationHistory
                    {
                        DonationItemId = donation.Id,
                        Action = "DeliveryAttemptOutsideGeofence",
                        Description = $"{personName} attempted delivery confirmation " +
                                      $"from {distanceDisplay} away (max allowed: {SchoolLocation.RadiusMeters} m). " +
                                      $"Coordinates: {lat.Value:F6}, {lng.Value:F6}.",
                        ActorId = personId,
                        ActorType = GetCurrentPersonType(),
                        ActionDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    return RedirectToAction("ConfirmDelivery", new { trackingCode });
                }

                donation.DeliveryLocation = $"Confirmed via GPS at {distanceDisplay} from school";
                donation.DeliveryConfirmedBy = personName;
                donation.DeliveryConfirmedDate = DateTime.Now;
                donation.DeliveryNotes = string.IsNullOrWhiteSpace(deliveryNotes)
                    ? null : deliveryNotes.Trim();
                donation.DeliveryLatitude = lat;
                donation.DeliveryLongitude = lng;
                donation.Status = DonationStatus.Verified;
                donation.VerificationDate = DateTime.Now;

                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = donation.Id,
                    Action = "DeliveryConfirmed",
                    Description = $"{personName} confirmed delivery via GPS " +
                                  $"({lat.Value:F6}, {lng.Value:F6}); " +
                                  $"distance to school: {distanceDisplay}.",
                    ActorId = personId,
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();

                try { await _aiMatcher.ProcessMatchingAsync(); } catch { }

                TempData["SuccessMessage"] =
                    $"Delivery confirmed for {donation.ItemName} ({distanceDisplay} from school). " +
                    $"It is now available for matching.";

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                TempData["ErrorMessage"] = "Error confirming delivery: " + innerMsg;
                return RedirectToAction("ConfirmDelivery");
            }
        }

        [HttpGet]
        public async Task<ActionResult> MyPendingDeliveries()
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return RedirectToAction("Login", "Account");

            var pending = await _context.DonationItems
                .Where(d => d.DonorId == personId
                            && d.IsActive
                            && d.Status == DonationStatus.AwaitingDelivery)
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            return View(pending);
        }

        #endregion

        #region Food Eligibility (read-only report)

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> FoodEligibilityList()
        {
            var foodItems = await _context.DonationItems
                .Include(d => d.FoodChecks)
                .Where(d => d.IsFoodItem)
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            var viewModel = foodItems.Select(d =>
            {
                var latestCheck = d.FoodChecks?.OrderByDescending(f => f.CheckedAt).FirstOrDefault();
                var status = latestCheck?.Status ?? FoodDonationStatus.PendingEligibilityCheck;

                return new FoodEligibilityListViewModel
                {
                    Id = latestCheck?.Id ?? 0,
                    DonationItemId = d.Id,
                    ItemName = d.ItemName,
                    DonorName = d.DonorName,
                    Quantity = d.Quantity,
                    FoodType = latestCheck?.FoodType ?? "Unknown",
                    ExpiryDate = latestCheck?.ExpiryDate ?? d.DonationDate.AddMonths(6),
                    ExpiryStatus = latestCheck != null && latestCheck.IsExpired ? "Expired"
                                 : latestCheck != null && latestCheck.IsExpiringSoon ? "Expiring Soon"
                                 : "OK",
                    Status = status,
                    StatusDisplay = GetFoodStatusDisplay(status),
                    StatusBadgeClass = GetFoodStatusBadgeClass(status),
                    CheckedAt = latestCheck?.CheckedAt ?? d.DonationDate,
                    CheckerName = latestCheck?.CheckerName ?? "System (automatic)",
                    IsFoodItem = true
                };
            }).ToList();

            ViewBag.ApprovedCount = viewModel.Count(v => v.Status == FoodDonationStatus.Approved);
            ViewBag.RejectedCount = viewModel.Count(v => v.Status == FoodDonationStatus.Rejected);
            ViewBag.ExpiredCount = viewModel.Count(v => v.Status == FoodDonationStatus.Expired);
            ViewBag.PendingCount = viewModel.Count(v => v.Status == FoodDonationStatus.PendingEligibilityCheck);
            ViewBag.TotalCount = viewModel.Count;

            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> FoodEligibilityCheck(int id)
        {
            var donation = await _context.DonationItems
                .Include(d => d.FoodChecks)
                .FirstOrDefaultAsync(d => d.Id == id && d.IsFoodItem);

            if (donation == null) return HttpNotFound();

            var foodCheck = donation.FoodChecks?
                .OrderByDescending(f => f.CheckedAt)
                .FirstOrDefault();

            if (foodCheck == null)
            {
                TempData["ErrorMessage"] = "No safety evaluation was recorded for this donation.";
                return RedirectToAction("FoodEligibilityList");
            }

            var decision = _foodSafetyEvaluator.Evaluate(foodCheck);

            var viewModel = new FoodEligibilityViewModel
            {
                Id = foodCheck.Id,
                DonationItemId = donation.Id,
                DonationItemName = donation.ItemName,
                DonorName = donation.DonorName,
                Quantity = donation.Quantity,
                DonationDate = donation.DonationDate,
                FoodType = foodCheck.FoodType,
                ExpiryDate = foodCheck.ExpiryDate,
                ProductionDate = foodCheck.ProductionDate,
                StorageType = foodCheck.StorageType,
                StorageNotes = foodCheck.StorageNotes,
                Allergens = foodCheck.Allergens,
                AllergenDetails = foodCheck.AllergenDetails,
                PackagingSealed = foodCheck.PackagingSealed,
                NoDamage = foodCheck.NoDamage,
                NoBulging = foodCheck.NoBulging,
                NoPestDamage = foodCheck.NoPestDamage,
                QualityGrade = foodCheck.QualityGrade,
                ConditionNotes = foodCheck.ConditionNotes,
                AppearancePassed = foodCheck.AppearancePassed,
                SmellPassed = foodCheck.SmellPassed,
                IsCommercialSource = foodCheck.IsCommercialSource,
                ProperlyLabeled = foodCheck.ProperlyLabeled,
                NutritionInfoPresent = foodCheck.NutritionInfoPresent,
                Status = foodCheck.Status,
                RejectionReason = foodCheck.RejectionReason,
                PhotoPath = donation.PhotoEvidence
            };

            ViewBag.DecisionSummary = decision.Summary;
            ViewBag.DecisionReasons = decision.Reasons;
            ViewBag.DecisionWarnings = decision.Warnings;

            return View(viewModel);
        }

        private string GetFoodStatusDisplay(FoodDonationStatus status)
        {
            switch (status)
            {
                case FoodDonationStatus.PendingEligibilityCheck: return "⏳ Pending Review";
                case FoodDonationStatus.Approved: return "✅ Auto-Approved";
                case FoodDonationStatus.Rejected: return "❌ Auto-Rejected";
                case FoodDonationStatus.Expired: return "⚠️ Expired";
                default: return "Unknown";
            }
        }

        private string GetFoodStatusBadgeClass(FoodDonationStatus status)
        {
            switch (status)
            {
                case FoodDonationStatus.PendingEligibilityCheck: return "badge-warning";
                case FoodDonationStatus.Approved: return "badge-success";
                case FoodDonationStatus.Rejected: return "badge-danger";
                case FoodDonationStatus.Expired: return "badge-secondary";
                default: return "badge-secondary";
            }
        }

        #endregion

        #region Donation History

        public async Task<ActionResult> DonationHistory()
        {
            IQueryable<DonationItem> query = _context.DonationItems
                .Include(d => d.Allocations)
                .Include(d => d.FoodChecks)
                .Where(d => d.IsActive);

            if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                if (student != null)
                {
                    query = query.Where(d => d.Allocations.Any(a => a.StudentId == student.Id) ||
                                            (d.DonorId == student.Id && d.DonorType == "Student"));
                }
            }

            var donations = await query
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            return View(donations);
        }

        public async Task<ActionResult> DonationDetails(int id)
        {
            var donation = await _context.DonationItems
                .Include(d => d.Allocations)
                .Include(d => d.Allocations.Select(a => a.Student))
                .Include(d => d.Campaign)
                .Include(d => d.FoodChecks)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (donation == null) return HttpNotFound();

            var latestCheck = donation.FoodChecks?.OrderByDescending(f => f.CheckedAt).FirstOrDefault();

            var history = await _context.DonationHistories
                .Where(h => h.DonationItemId == id)
                .OrderByDescending(h => h.ActionDate)
                .ToListAsync();

            ViewBag.History = history;
            ViewBag.IsFoodItem = donation.IsFoodItem;
            ViewBag.FoodCheck = latestCheck;
            return View(donation);
        }

        #endregion

        #region Verify Donation

        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> VerifyDonation(int id)
        {
            var donation = await _context.DonationItems
                .Include(d => d.FoodChecks)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (donation == null)
                return Json(new { success = false, message = "Donation not found" });

            if (donation.Status != DonationStatus.PendingVerification
                && donation.Status != DonationStatus.PendingEligibilityCheck)
            {
                return Json(new { success = false, message = "Donation is not pending verification" });
            }

            var personId = GetCurrentPersonId();

            if (donation.IsFoodItem)
            {
                var latestCheck = donation.FoodChecks?.OrderByDescending(f => f.CheckedAt).FirstOrDefault();
                if (latestCheck == null || latestCheck.Status != FoodDonationStatus.Approved)
                {
                    return Json(new { success = false, message = "Food item was not approved by the safety evaluator." });
                }
            }

            donation.Status = DonationStatus.Verified;
            donation.VerificationDate = DateTime.Now;
            donation.VerifiedBy = personId;

            await _context.SaveChangesAsync();

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = donation.Id,
                Action = "Verified",
                Description = $"Donation verified by {GetCurrentPersonName()}",
                ActorId = personId,
                ActorType = GetCurrentPersonType()
            });
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation verified successfully" });
        }

        #endregion

        #region Match Donations

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> MatchDonations()
        {
            var pendingItems = await _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Include(i => i.DonationRequest.Student)
                .Where(i => i.DonationRequest.IsActive && !i.IsFulfilled && i.Status != RequestItemStatus.Declined)
                .OrderByDescending(i => i.Priority)
                .ThenByDescending(i => i.DonationRequest.PriorityScore)
                .ToListAsync();

            var availableDonations = await _context.DonationItems
                .Where(d => d.IsActive && d.Status == DonationStatus.Verified && d.QuantityRemaining > 0)
                .ToListAsync();

            var matches = new List<PriorityMatchViewModel>();

            foreach (var item in pendingItems)
            {
                var compatibleDonation = availableDonations
                    .FirstOrDefault(d => d.Category == item.Category && d.QuantityRemaining > 0);

                if (compatibleDonation != null)
                {
                    matches.Add(new PriorityMatchViewModel
                    {
                        RequestId = item.DonationRequestId,
                        RequestItemId = item.Id,
                        Category = item.Category,
                        StudentName = item.DonationRequest.Student?.FullName,
                        StudentNumber = item.DonationRequest.Student?.User?.StudentNumber,
                        ItemNeeded = item.ItemName,
                        PriorityScore = item.DonationRequest.PriorityScore,
                        WaitListPosition = item.DonationRequest.WaitListPosition,
                        RequestDate = item.DonationRequest.RequestDate,
                        Priority = item.Priority,
                        DonationItemId = compatibleDonation.Id,
                        DonationItemName = compatibleDonation.ItemName,
                        MatchScore = 80
                    });
                }
                else
                {
                    matches.Add(new PriorityMatchViewModel
                    {
                        RequestId = item.DonationRequestId,
                        RequestItemId = item.Id,
                        Category = item.Category,
                        StudentName = item.DonationRequest.Student?.FullName,
                        StudentNumber = item.DonationRequest.Student?.User?.StudentNumber,
                        ItemNeeded = item.ItemName,
                        PriorityScore = item.DonationRequest.PriorityScore,
                        WaitListPosition = item.DonationRequest.WaitListPosition,
                        RequestDate = item.DonationRequest.RequestDate,
                        Priority = item.Priority,
                        DonationItemId = null,
                        MatchScore = 0
                    });
                }
            }

            ViewBag.PendingRequests = pendingItems.Count;
            ViewBag.AvailableDonations = availableDonations.Count;

            return View(matches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AllocateDonation(int requestId, int donationItemId, int quantity)
        {
            try
            {
                var request = await _context.DonationRequests
                    .Include(r => r.Items)
                    .Include(r => r.Student)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null || !request.IsActive)
                {
                    TempData["ErrorMessage"] = "Invalid request.";
                    return RedirectToAction("MatchDonations");
                }

                var donation = await _context.DonationItems.FindAsync(donationItemId);
                if (donation == null || donation.QuantityRemaining < quantity)
                {
                    TempData["ErrorMessage"] = "Insufficient donation quantity or donation not found.";
                    return RedirectToAction("MatchDonations");
                }

                var allocation = new DonationAllocation
                {
                    DonationItemId = donation.Id,
                    StudentId = request.StudentId,
                    RequestId = request.Id,
                    QuantityAllocated = quantity,
                    Status = "Pending",
                    AllocationDate = DateTime.Now,
                    IsActive = true,
                    Notes = "Manually matched"
                };

                _context.DonationAllocations.Add(allocation);

                donation.QuantityRemaining -= quantity;
                if (donation.QuantityRemaining == 0)
                    donation.Status = DonationStatus.Allocated;

                var matchingItems = request.Items
                    .Where(i => i.Category == donation.Category && !i.IsFulfilled && i.Status != RequestItemStatus.Declined)
                    .ToList();

                foreach (var item in matchingItems)
                {
                    var remaining = item.QuantityRequested - item.QuantityReceived;
                    var toApply = Math.Min(remaining, quantity);
                    item.QuantityReceived += toApply;

                    if (toApply > 0)
                    {
                        item.Status = RequestItemStatus.Allocated;
                    }

                    if (item.QuantityReceived >= item.QuantityRequested)
                    {
                        item.IsFulfilled = true;
                        item.FulfilledDate = DateTime.Now;
                    }
                }

                if (request.Items.All(i => i.IsFulfilled))
                {
                    request.IsFulfilled = true;
                    request.FulfilledDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Allocation created successfully — {quantity} item(s) assigned to {request.Student?.FullName ?? "student"}.";

                return RedirectToAction("MatchDonations");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                TempData["ErrorMessage"] = "Error creating allocation: " + innerMsg;
                return RedirectToAction("MatchDonations");
            }
        }

        // Lets Admin/Teacher explicitly decline a single request item (e.g. it
        // can't realistically be sourced) so the student sees an honest status
        // instead of it sitting on the waitlist forever. Declined items are
        // excluded from MatchDonations/PriorityWaitlist going forward.
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<JsonResult> DeclineRequestItem(int id, string reason)
        {
            var item = await _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return Json(new { success = false, message = "Request item not found." });

            if (item.IsFulfilled || item.Status == RequestItemStatus.Allocated)
                return Json(new { success = false, message = "This item has already been allocated and can no longer be declined." });

            item.Status = RequestItemStatus.Declined;
            item.DeclineReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();
            item.DeclinedDate = DateTime.Now;
            item.DeclinedBy = GetCurrentPersonId();

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Item declined." });
        }

        #endregion

        #region Priority Waitlist

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> PriorityWaitlist()
        {
            var pendingRequests = await _context.DonationRequests
                .Include(r => r.Student)
                .Include(r => r.Items)
                .Where(r => r.IsActive && !r.IsFulfilled)
                .OrderByDescending(r => r.PriorityScore)
                .ThenBy(r => r.RequestDate)
                .ToListAsync();

            int position = 1;
            foreach (var req in pendingRequests)
            {
                req.WaitListPosition = position++;
            }
            await _context.SaveChangesAsync();

            var viewModel = new List<PriorityMatchViewModel>();
            foreach (var r in pendingRequests)
            {
                foreach (var item in r.Items.Where(i => !i.IsFulfilled && i.Status != RequestItemStatus.Declined))
                {
                    viewModel.Add(new PriorityMatchViewModel
                    {
                        RequestId = r.Id,
                        RequestItemId = item.Id,
                        Category = item.Category,
                        StudentName = r.Student?.FullName,
                        StudentNumber = r.Student?.User?.StudentNumber,
                        ItemNeeded = item.ItemName,
                        PriorityScore = item.CalculateItemScore(),
                        WaitListPosition = r.WaitListPosition,
                        RequestDate = r.RequestDate,
                        Priority = item.Priority
                    });
                }
            }

            return View(viewModel);
        }

        #endregion

        #region Auto-Match Food

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> AutoMatchFood()
        {
            var donations = await _context.DonationItems
                .Include(d => d.FoodChecks)
                .Where(d => d.IsActive
                            && d.IsFoodItem
                            && d.Status == DonationStatus.Verified
                            && d.QuantityRemaining > 0)
                .OrderBy(d => d.DonationDate)
                .ToListAsync();

            var pendingFoodRequestCount = await _context.DonationRequestItems
                .CountAsync(i => i.Category == DonationCategory.Food
                                 && i.DonationRequest.IsActive
                                 && !i.IsFulfilled);

            var donationChecks = new Dictionary<int, FoodDonationCheck>();
            foreach (var d in donations)
            {
                var check = d.FoodChecks?
                    .OrderByDescending(f => f.CheckedAt)
                    .FirstOrDefault();
                if (check != null) donationChecks[d.Id] = check;
            }

            ViewBag.DonationChecks = donationChecks;
            ViewBag.PendingFoodRequestCount = pendingFoodRequestCount;

            return View(donations);
        }

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> PreviewFoodAllocation(int id)
        {
            var donation = await _context.DonationItems
                .Include(d => d.FoodChecks)
                .FirstOrDefaultAsync(d => d.Id == id && d.IsFoodItem && d.IsActive);

            if (donation == null)
            {
                TempData["ErrorMessage"] = "Food donation not found.";
                return RedirectToAction("AutoMatchFood");
            }

            if (donation.Status != DonationStatus.Verified || donation.QuantityRemaining <= 0)
            {
                TempData["ErrorMessage"] =
                    "This donation is not available for allocation " +
                    $"(status: {donation.Status}, remaining: {donation.QuantityRemaining}).";
                return RedirectToAction("AutoMatchFood");
            }

            var foodCheck = donation.FoodChecks?
                .OrderByDescending(f => f.CheckedAt)
                .FirstOrDefault();

            var candidates = await _foodAllocationEngine.BuildCandidatesAsync(_context);
            var plan = _foodAllocationEngine.ComputePlanForDonation(donation, foodCheck, candidates);

            ViewBag.DonationItem = donation;
            ViewBag.FoodCheck = foodCheck;

            return View(plan);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CommitFoodAllocation(FormCollection form)
        {
            try
            {
                int donationId;
                if (!int.TryParse(form["donationItemId"], out donationId))
                {
                    TempData["ErrorMessage"] = "Missing donation id.";
                    return RedirectToAction("AutoMatchFood");
                }

                var donation = await _context.DonationItems
                    .Include(d => d.FoodChecks)
                    .FirstOrDefaultAsync(d => d.Id == donationId && d.IsFoodItem && d.IsActive);

                if (donation == null)
                {
                    TempData["ErrorMessage"] = "Food donation not found.";
                    return RedirectToAction("AutoMatchFood");
                }

                if (donation.Status != DonationStatus.Verified || donation.QuantityRemaining <= 0)
                {
                    TempData["ErrorMessage"] =
                        "This donation has already been fully allocated or is no longer available.";
                    return RedirectToAction("AutoMatchFood");
                }

                var inputs = new List<CommitLineInput>();
                int totalRequested = 0;

                for (int i = 0; ; i++)
                {
                    var studentIdStr = form["Lines[" + i + "].StudentId"];
                    if (string.IsNullOrWhiteSpace(studentIdStr)) break;

                    int studentId, requestId, requestItemId, qty;

                    if (!int.TryParse(studentIdStr, out studentId)) continue;
                    if (!int.TryParse(form["Lines[" + i + "].RequestId"], out requestId)) requestId = 0;
                    if (!int.TryParse(form["Lines[" + i + "].RequestItemId"], out requestItemId)) continue;
                    if (!int.TryParse(form["Lines[" + i + "].Quantity"], out qty)) qty = 0;

                    if (qty <= 0) continue;

                    totalRequested += qty;

                    inputs.Add(new CommitLineInput
                    {
                        StudentId = studentId,
                        RequestId = requestId,
                        RequestItemId = requestItemId,
                        Quantity = qty
                    });
                }

                if (!inputs.Any())
                {
                    TempData["ErrorMessage"] = "No allocations were submitted.";
                    return RedirectToAction("PreviewFoodAllocation", new { id = donation.Id });
                }

                if (totalRequested > donation.QuantityRemaining)
                {
                    TempData["ErrorMessage"] =
                        $"The submitted total ({totalRequested}) exceeds the " +
                        $"available stock ({donation.QuantityRemaining}). " +
                        $"Please adjust the quantities and try again.";
                    return RedirectToAction("PreviewFoodAllocation", new { id = donation.Id });
                }

                var staffId = GetCurrentPersonId();
                var staffName = GetCurrentPersonName();

                var decision = new FoodAllocationDecision
                {
                    DonationItemId = donation.Id,
                    DecidedAt = DateTime.Now,
                    TotalUnitsAvailable = donation.QuantityRemaining,
                    TotalUnitsAllocated = totalRequested,
                    EligibleStudentCount = inputs.Count,
                    DecidedBy = staffId,
                    DecidedByName = staffName
                };

                int.TryParse(form["excludedCount"], out int excludedCount);
                decision.ExcludedStudentCount = excludedCount;
                decision.ExcludedReasonSummary = form["excludedSummary"];

                _context.FoodAllocationDecisions.Add(decision);
                await _context.SaveChangesAsync();

                foreach (var input in inputs)
                {
                    var alloc = new DonationAllocation
                    {
                        DonationItemId = donation.Id,
                        StudentId = input.StudentId,
                        RequestId = input.RequestId > 0 ? (int?)input.RequestId : null,
                        QuantityAllocated = input.Quantity,
                        Status = "Pending",
                        AllocationDate = DateTime.Now,
                        IsActive = true,
                        Notes = "Auto-allocated from food donation"
                    };
                    _context.DonationAllocations.Add(alloc);

                    decision.Lines.Add(new FoodAllocationDecisionLine
                    {
                        FoodAllocationDecisionId = decision.Id,
                        StudentId = input.StudentId,
                        RequestItemId = input.RequestItemId,
                        AllocatedQuantity = input.Quantity,
                        Priority = "Auto"
                    });

                    var reqItem = await _context.DonationRequestItems
                        .Include(i => i.DonationRequest)
                        .FirstOrDefaultAsync(i => i.Id == input.RequestItemId);

                    if (reqItem != null)
                    {
                        reqItem.QuantityReceived += input.Quantity;
                        reqItem.Status = RequestItemStatus.Allocated;

                        if (reqItem.Category == DonationCategory.Food)
                        {
                            reqItem.IsFulfilled = true;
                            reqItem.FulfilledDate = DateTime.Now;
                        }
                        else if (reqItem.QuantityReceived >= reqItem.QuantityRequested)
                        {
                            reqItem.IsFulfilled = true;
                            reqItem.FulfilledDate = DateTime.Now;
                        }

                        if (reqItem.DonationRequest != null)
                        {
                            var anyOpen = await _context.DonationRequestItems
                                .AnyAsync(i => i.DonationRequestId == reqItem.DonationRequestId
                                               && !i.IsFulfilled
                                               && i.Id != reqItem.Id);

                            if (!anyOpen)
                            {
                                reqItem.DonationRequest.IsFulfilled = true;
                                reqItem.DonationRequest.FulfilledDate = DateTime.Now;
                            }
                        }
                    }
                }

                donation.QuantityRemaining -= totalRequested;
                if (donation.QuantityRemaining <= 0)
                {
                    donation.Status = DonationStatus.Allocated;
                }

                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = donation.Id,
                    Action = "FoodAutoAllocated",
                    Description = $"{staffName} auto-allocated {totalRequested} unit(s) " +
                                  $"of {donation.ItemName} to {inputs.Count} student(s).",
                    ActorId = staffId,
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Allocated {totalRequested} × {donation.ItemName} to {inputs.Count} student(s). " +
                    $"These allocations are now pending and ready to be scheduled.";

                return RedirectToAction("AutoMatchFood");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                TempData["ErrorMessage"] = "Error committing allocation: " + innerMsg;
                return RedirectToAction("AutoMatchFood");
            }
        }

        private class CommitLineInput
        {
            public int StudentId { get; set; }
            public int RequestId { get; set; }
            public int RequestItemId { get; set; }
            public int Quantity { get; set; }
        }

        #endregion

        #region Distribution

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> Distribution()
        {
            var pendingAllocations = await _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.DonationItem)
                .Where(a => a.Status == "Pending" && a.IsActive)
                .OrderBy(a => a.AllocationDate)
                .ToListAsync();

            var viewModel = pendingAllocations.Select(a => new DonationDistributionViewModel
            {
                AllocationId = a.Id,
                StudentName = a.Student?.FullName,
                StudentNumber = a.Student?.User?.StudentNumber,
                ItemName = a.DonationItem?.ItemName,
                ItemType = a.DonationItem?.ItemType.ToString(),
                Quantity = a.QuantityAllocated,
                CollectionToken = a.CollectionToken,
                QRCode = a.QRCode,
                PinCode = a.PinCode,
                ScheduledDate = a.ScheduledCollectionDate,
                Status = a.Status,
                IsFoodItem = a.DonationItem?.IsFoodItem ?? false
            }).ToList();

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,Teacher")]
        public ActionResult ProcessCollection(string token)
        {
            if (string.IsNullOrEmpty(token))
                return View(new DonationDistributionViewModel());

            var allocation = _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.DonationItem)
                .FirstOrDefault(a => a.CollectionToken == token && a.IsActive);

            if (allocation == null)
            {
                ViewBag.ErrorMessage = "Invalid collection token.";
                return View(new DonationDistributionViewModel());
            }

            var viewModel = new DonationDistributionViewModel
            {
                AllocationId = allocation.Id,
                StudentName = allocation.Student?.FullName,
                StudentNumber = allocation.Student?.User?.StudentNumber,
                ItemName = allocation.DonationItem?.ItemName,
                ItemType = allocation.DonationItem?.ItemType.ToString(),
                Quantity = allocation.QuantityAllocated,
                CollectionToken = allocation.CollectionToken,
                PinCode = allocation.PinCode,
                Status = allocation.Status,
                IsFoodItem = allocation.DonationItem?.IsFoodItem ?? false
            };

            return View(viewModel);
        }

        // Scheduling always leaves the allocation collection-ready: it sets
        // the date, flips status to Ready, and makes sure a collection
        // token/QR/PIN exist (they're set when the allocation is first
        // created, but we refresh them here so a freshly-scheduled pickup
        // always has a clean code) — no separate "generate token" step is
        // required before collection can happen.
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> ScheduleDistribution(int allocationId, DateTime scheduleDate)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive);

            if (allocation == null)
                return Json(new { success = false, message = "Allocation not found" });

            allocation.ScheduledCollectionDate = scheduleDate;
            allocation.Status = "Ready";

            if (string.IsNullOrEmpty(allocation.CollectionToken))
                allocation.CollectionToken = "COL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            allocation.QRCode = "QR:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(allocation.CollectionToken));

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Distribution scheduled successfully" });
        }

        // Re-issues a fresh token/QR/PIN for an allocation (e.g. the
        // student lost their PIN, or it needs to be reissued for security).
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> GenerateCollectionToken(int allocationId)
        {
            var allocation = await _context.DonationAllocations.FindAsync(allocationId);
            if (allocation == null)
                return Json(new { success = false, message = "Allocation not found" });

            allocation.CollectionToken = "COL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            allocation.QRCode = "QR:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(allocation.CollectionToken));
            allocation.PinCode = new Random().Next(1000, 9999).ToString();
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                token = allocation.CollectionToken,
                qrCode = allocation.QRCode,
                pin = allocation.PinCode
            });
        }

        // Matches on the allocation's CollectionToken (always populated —
        // set the moment the allocation is created) plus the 4-digit PIN.
        // The QRCode field just encodes the token for a scannable image;
        // it isn't a second independent secret.
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> ConfirmCollection(int allocationId, string token, string pinCode)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive);

            if (allocation == null)
                return Json(new { success = false, message = "Allocation not found" });

            if (string.IsNullOrEmpty(allocation.CollectionToken) || allocation.CollectionToken != (token ?? "").Trim().ToUpper())
                return Json(new { success = false, message = "Invalid collection token" });

            if (string.IsNullOrEmpty(allocation.PinCode) || allocation.PinCode != (pinCode ?? "").Trim())
                return Json(new { success = false, message = "Invalid PIN" });

            if (allocation.Status != "Ready")
                return Json(new { success = false, message = "Not ready for collection" });

            allocation.Status = "Collected";
            allocation.CollectionDate = DateTime.Now;
            allocation.CollectedBy = GetCurrentPersonId();

            var donation = await _context.DonationItems.FindAsync(allocation.DonationItemId);
            if (donation != null && donation.QuantityRemaining == 0)
            {
                donation.Status = DonationStatus.Collected;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Collection confirmed!" });
        }

        #endregion

        #region Campaigns

        [Authorize(Roles = "Admin")]
        public ActionResult Campaigns()
        {
            var campaigns = _context.DonationCampaigns
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            ViewBag.Statuses = Enum.GetNames(typeof(CampaignStatus));
            return View(campaigns);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult CreateCampaign()
        {
            return View(new CampaignViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateCampaign(CampaignViewModel model)
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var campaign = new DonationCampaign
                {
                    Name = model.Name,
                    Description = model.Description,
                    Category = model.Category,
                    Status = CampaignStatus.Draft,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    TargetQuantity = model.TargetQuantity,
                    TargetGroup = model.TargetGroup,
                    IsExternal = model.IsExternal,
                    ExternalPortalUrl = model.ExternalPortalUrl,
                    CreatedBy = personId,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.DonationCampaigns.Add(campaign);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Campaign created successfully!";
                return RedirectToAction("CampaignDetails", new { id = campaign.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                return View(model);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CampaignDetails(int id)
        {
            var campaign = await _context.DonationCampaigns
                .Include(c => c.Donations)
                .Include(c => c.Pledges)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (campaign == null) return HttpNotFound();

            ViewBag.Donations = _context.DonationItems
                .Where(d => d.CampaignId == id)
                .ToList();

            return View(campaign);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> ActivateCampaign(int id)
        {
            var campaign = await _context.DonationCampaigns.FindAsync(id);
            if (campaign == null)
                return Json(new { success = false, message = "Campaign not found" });

            campaign.Status = CampaignStatus.Active;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Campaign activated successfully" });
        }

        #endregion

        #region Bulk Pledge

        [Authorize(Roles = "Admin")]
        public ActionResult BulkPledge(int campaignId)
        {
            var campaign = _context.DonationCampaigns.Find(campaignId);
            if (campaign == null) return HttpNotFound();

            ViewBag.CampaignName = campaign.Name;
            ViewBag.CampaignId = campaignId;
            return View(new BulkPledgeViewModel { CampaignId = campaignId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BulkPledge(BulkPledgeViewModel model)
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                var campaign = _context.DonationCampaigns.Find(model.CampaignId);
                ViewBag.CampaignName = campaign?.Name;
                ViewBag.CampaignId = model.CampaignId;
                return View(model);
            }

            try
            {
                var pledge = new BulkPledge
                {
                    CampaignId = model.CampaignId,
                    DonorName = model.DonorName,
                    DonorEmail = model.DonorEmail,
                    DonorOrganization = model.DonorOrganization,
                    DonorPhone = model.DonorPhone,
                    Category = model.Category,
                    ItemType = model.ItemType,
                    ItemName = model.ItemName,
                    Description = model.Description,
                    QuantityPledged = model.QuantityPledged,
                    QuantityDelivered = 0,
                    Status = PledgeStatus.PendingApproval,
                    ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                    DeliveryNotes = model.DeliveryNotes,
                    CreatedBy = personId,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.BulkPledges.Add(pledge);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bulk pledge submitted for approval!";
                return RedirectToAction("CampaignDetails", new { id = model.CampaignId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                var campaign = _context.DonationCampaigns.Find(model.CampaignId);
                ViewBag.CampaignName = campaign?.Name;
                ViewBag.CampaignId = model.CampaignId;
                return View(model);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Pledges()
        {
            var pledges = await _context.BulkPledges
                .Include(p => p.Campaign)
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(pledges);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> ApprovePledge(int id)
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return Json(new { success = false, message = "Unauthorized" });

            var pledge = await _context.BulkPledges.FindAsync(id);
            if (pledge == null)
                return Json(new { success = false, message = "Pledge not found" });

            pledge.Status = PledgeStatus.Approved;
            pledge.ApprovedAt = DateTime.Now;
            pledge.ApprovedBy = personId;
            pledge.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Pledge approved successfully" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> RecordPledgeDelivery(int id, int quantityDelivered)
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return Json(new { success = false, message = "Unauthorized" });

            var pledge = await _context.BulkPledges.FindAsync(id);
            if (pledge == null)
                return Json(new { success = false, message = "Pledge not found" });

            pledge.QuantityDelivered += quantityDelivered;
            pledge.UpdatedAt = DateTime.Now;

            if (pledge.QuantityDelivered >= pledge.QuantityPledged)
            {
                pledge.Status = PledgeStatus.FullyDelivered;
                pledge.ActualDeliveryDate = DateTime.Now;

                var donation = new DonationItem
                {
                    DonorId = personId,
                    DonorType = "External",
                    DonorName = pledge.DonorName,
                    DonorEmail = pledge.DonorEmail,
                    Category = pledge.Category,
                    ItemType = pledge.ItemType,
                    ItemName = pledge.ItemName,
                    Description = pledge.Description,
                    Quantity = quantityDelivered,
                    QuantityRemaining = quantityDelivered,
                    AllocationType = AllocationType.OpenDonation,
                    Condition = "New",
                    Status = DonationStatus.AwaitingDelivery,
                    DonationDate = DateTime.Now,
                    CampaignId = pledge.CampaignId,
                    IsActive = true,
                    IsFoodItem = pledge.Category == DonationCategory.Food
                };

                _context.DonationItems.Add(donation);
                await _context.SaveChangesAsync();

                if (donation.IsFoodItem)
                {
                    var foodCheck = new FoodDonationCheck
                    {
                        DonationItemId = donation.Id,
                        ExpiryDate = DateTime.Now.AddMonths(6),
                        CheckedAt = DateTime.Now,
                        CheckerName = "System (awaiting donor questionnaire)"
                    };
                    _context.FoodDonationChecks.Add(foodCheck);
                    donation.Status = DonationStatus.PendingEligibilityCheck;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                pledge.Status = PledgeStatus.PartiallyDelivered;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Delivery recorded successfully" });
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}