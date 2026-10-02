using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
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

        // UC03 — how many items the office can physically process on a single
        // intake day. Kept as one constant rather than a settings table since
        // nothing in this upload persists admin-configurable settings.
        private const int IntakeDailyCapacity = 10;

        public DonationController()
        {
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

                // ── Redesigned (non-food) item pipeline — UC01-UC09 ──
                ViewBag.PendingApprovalCount = _context.DonationItems
                    .Count(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.PendingApproval);
                ViewBag.ApprovedAwaitingIntakeCount = _context.DonationItems
                    .Count(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.ApprovedAwaitingIntake);
                ViewBag.AvailableItemsCount = _context.DonationItems
                    .Count(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.Available && d.QuantityRemaining > 0);
                ViewBag.PendingReviewMatchesCount = _context.DonationAllocations
                    .Count(a => a.IsActive && a.Status == "PendingReview");
                ViewBag.AwaitingCollectionCount = _context.DonationAllocations
                    .Count(a => a.IsActive && a.Status == "AwaitingCollection");
                ViewBag.OpenNonFoodRequestsCount = _context.DonationRequestItems
                    .Count(i => i.Category != DonationCategory.Food
                                && i.DonationRequest.IsActive
                                && !i.IsFulfilled
                                && i.Status != RequestItemStatus.Declined);
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

                    // ── Redesigned (non-food) item pipeline — student's own view ──
                    ViewBag.MyAwaitingCollectionCount = _context.DonationAllocations
                        .Count(a => a.StudentId == student.Id && a.IsActive && a.Status == "AwaitingCollection");
                    ViewBag.MyOpenRequestsCount = _context.DonationRequestItems
                        .Count(i => i.DonationRequest.StudentId == student.Id
                                    && i.DonationRequest.IsActive
                                    && !i.IsFulfilled
                                    && i.Status != RequestItemStatus.Declined);
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
                var duplicateCategories = new List<string>();

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

                        // UC01 precondition: no duplicate open request in the
                        // same category for this learner.
                        var alreadyOpen = _context.DonationRequestItems.Any(ri =>
                            ri.DonationRequest.StudentId == student.Id
                            && ri.DonationRequest.IsActive
                            && ri.Category == category
                            && !ri.IsFulfilled
                            && ri.Status != RequestItemStatus.Declined);

                        if (alreadyOpen)
                        {
                            duplicateCategories.Add(category.ToString());
                            continue;
                        }
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
                    TempData["ErrorMessage"] = duplicateCategories.Any()
                        ? "You already have an open request in: " + string.Join(", ", duplicateCategories) + ". Cancel it first if you'd like to submit a new one."
                        : "No valid items were submitted.";
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

                // UC05: give the new item a chance to be matched immediately,
                // on top of the periodic sweep in Global.asax.
                try { DonationAutomationEngine.RunMatchingSweep(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Matching sweep error: " + ex.Message); }

                var successMsg =
                    $"Thank you! Your request with {savedItems} item(s) has been submitted. " +
                    $"You are at position #{waitlistPos} on the waitlist.";

                if (duplicateCategories.Any())
                    successMsg += " (Skipped — you already have an open request in: " + string.Join(", ", duplicateCategories) + ".)";

                TempData["SuccessMessage"] = successMsg;

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
                        // Food (old flow): PIN, shown once "Ready". Non-food
                        // (new flow, UC06): the Collection Code, shown once
                        // "AwaitingCollection" — already emailed to the
                        // student by ConfirmMatch, surfaced here too so they
                        // don't have to dig through email to find it.
                        pinCode = matchingAllocation != null && matchingAllocation.Status == "Ready" ? matchingAllocation.PinCode : null,
                        collectionCode = matchingAllocation != null && matchingAllocation.Status == "AwaitingCollection" ? matchingAllocation.CollectionToken : null,
                        waitListPosition = (item.Status == RequestItemStatus.Pending || item.Status == RequestItemStatus.Reserved) ? request.WaitListPosition : (int?)null,
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

            // "PendingReview" matches are the system's internal proposals —
            // UC05/UC06: nothing is shown to the learner until an admin
            // confirms them (ConfirmMatch), which is when the status flips
            // to "AwaitingCollection" and the match/code email goes out.
            var allocations = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Where(a => a.StudentId == student.Id && a.IsActive && a.Status != "PendingReview")
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

        #region Add Donation (Multi-Item, UC02)

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

        // Saves an uploaded condition photo under ~/Content/DonationPhotos and
        // returns the web-relative path to store on DonationItem.PhotoEvidence.
        // UC02 requires at least one photo per item; UC04/UC07 show it again
        // to the admin and the learner for a final visual check.
        private string SaveDonationPhoto(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0) return null;

            var folder = Server.MapPath("~/Content/DonationPhotos");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);

            file.SaveAs(fullPath);

            return "/Content/DonationPhotos/" + fileName;
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
                var trackingCodesAwaitingApproval = new List<string>();
                var namedAllocationCount = 0;
                var unresolvedNamedLearners = new List<string>();

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

                    // UC02 step 5 — Open Donation (default) or Named Allocation.
                    // A named allocation still goes through the normal approval
                    // queue (UC03); the admin confirms a genuine request exists
                    // for the named learner before approving.
                    var allocationTypeStr = form[prefix + "AllocationType"];
                    var allocationType = AllocationType.OpenDonation;
                    int? targetStudentId = null;

                    if (allocationTypeStr == "NamedAllocation")
                    {
                        var targetStudentNumber = (form[prefix + "TargetStudentNumber"] ?? "").Trim();
                        if (!string.IsNullOrEmpty(targetStudentNumber))
                        {
                            var targetUser = _context.Users.FirstOrDefault(u => u.StudentNumber == targetStudentNumber);
                            var targetStudent = targetUser != null
                                ? _context.Students.FirstOrDefault(s => s.UserId == targetUser.Id)
                                : null;

                            if (targetStudent != null)
                            {
                                allocationType = AllocationType.NamedAllocation;
                                targetStudentId = targetStudent.Id;
                                namedAllocationCount++;
                            }
                            else
                            {
                                unresolvedNamedLearners.Add(targetStudentNumber);
                            }
                        }
                    }

                    // UC02 step 4 — at least one condition photo is required.
                    var photoFile = Request.Files[prefix + "Photo"];
                    var photoPath = SaveDonationPhoto(photoFile);

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
                        AllocationType = allocationType,
                        TargetStudentId = targetStudentId,
                        Condition = condition,
                        ConditionNotes = form[prefix + "ConditionNotes"],
                        PhotoEvidence = photoPath,
                        // UC02: every item now goes through admin approval +
                        // scheduled intake (UC03/UC04) — there is no more
                        // self-service delivery-confirmation step.
                        Status = DonationStatus.PendingApproval,
                        DonationDate = DateTime.Now,
                        IsActive = true,
                        IsFoodItem = false
                    };

                    _context.DonationItems.Add(donation);
                    await _context.SaveChangesAsync();

                    trackingCodesAwaitingApproval.Add(donation.TrackingCode);

                    _context.DonationHistories.Add(new DonationHistory
                    {
                        DonationItemId = donation.Id,
                        Action = "DonationRegistered",
                        Description = $"{personName} registered {quantity} {itemName}(s)" +
                                      (allocationType == AllocationType.NamedAllocation
                                          ? " as a Named Allocation."
                                          : ".") +
                                      $" Tracking code: {donation.TrackingCode}. Awaiting admin approval.",
                        ActorId = personId,
                        ActorType = personType,
                        AdditionalData = JsonConvert.SerializeObject(new { DonationId = donation.Id, TrackingCode = donation.TrackingCode })
                    });
                    await _context.SaveChangesAsync();

                    savedCount++;
                }

                if (savedCount == 0)
                {
                    TempData["ErrorMessage"] = "No valid items were submitted.";
                    return RedirectToAction("AddDonation");
                }

                var parts = new List<string>();
                parts.Add($"Thank you! {savedCount} donation(s) submitted and awaiting admin approval.");

                if (namedAllocationCount > 0)
                    parts.Add($"{namedAllocationCount} item(s) were submitted as a Named Allocation — the admin will confirm a matching request exists before approving.");

                if (unresolvedNamedLearners.Any())
                    parts.Add($"Couldn't find a learner with student number(s) {string.Join(", ", unresolvedNamedLearners)} — those item(s) were submitted as Open Donations instead.");

                TempData["SuccessMessage"] = string.Join(" ", parts);

                if (trackingCodesAwaitingApproval.Any())
                {
                    TempData["TrackingCodesAwaitingApproval"] = string.Join(", ", trackingCodesAwaitingApproval);
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

        #region Approve Donation & Schedule Intake (UC03)

        // Pending-approval queue, oldest first. Food is excluded — it has
        // its own automatic safety-check path and never reaches this status.
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> ApproveDonation()
        {
            var pending = await _context.DonationItems
                .Where(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.PendingApproval)
                .OrderBy(d => d.DonationDate)
                .ToListAsync();

            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApproveDonationSubmit(int id, bool approve, string intakeDate, string reason)
        {
            var donation = await _context.DonationItems.FindAsync(id);
            if (donation == null || donation.Status != DonationStatus.PendingApproval)
            {
                TempData["ErrorMessage"] = "Donation not found or already processed.";
                return RedirectToAction("ApproveDonation");
            }

            var personName = GetCurrentPersonName();
            var personId = GetCurrentPersonId();

            if (!approve)
            {
                donation.Status = DonationStatus.Rejected;
                donation.IsActive = false;

                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = donation.Id,
                    Action = "Rejected",
                    Description = $"Rejected by {personName}." + (string.IsNullOrWhiteSpace(reason) ? "" : " Reason: " + reason.Trim()),
                    ActorId = personId,
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(donation.DonorEmail))
                {
                    try
                    {
                        new EmailService().SendDonationRejectedEmail(donation.DonorEmail, donation.DonorName, donation.ItemName, reason);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Donation-rejected email failed: " + ex.Message);
                    }
                }

                TempData["SuccessMessage"] = $"Donation #{donation.Id} rejected.";
                return RedirectToAction("ApproveDonation");
            }

            var date = ParseDate(intakeDate);
            if (!date.HasValue)
            {
                TempData["ErrorMessage"] = "Please choose a valid intake date.";
                return RedirectToAction("ApproveDonation");
            }

            // UC03 — capacity-limited intake booking: the office can only
            // physically process so many drop-offs on one day.
            var alreadyBooked = await _context.DonationItems
                .CountAsync(d => d.IsActive && !d.IsFoodItem
                                  && d.Status == DonationStatus.ApprovedAwaitingIntake
                                  && d.ScheduledIntakeDate.HasValue
                                  && DbFunctions.TruncateTime(d.ScheduledIntakeDate) == date.Value.Date);
            if (alreadyBooked >= IntakeDailyCapacity)
            {
                TempData["ErrorMessage"] = $"{date.Value:dd MMM yyyy} is fully booked ({IntakeDailyCapacity} intake slots). Please choose another date.";
                return RedirectToAction("ApproveDonation");
            }

            donation.Status = DonationStatus.ApprovedAwaitingIntake;
            donation.ScheduledIntakeDate = date.Value;

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = donation.Id,
                Action = "Approved",
                Description = $"Approved by {personName}. Intake scheduled for {date.Value:dd MMM yyyy}.",
                ActorId = personId,
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(donation.DonorEmail))
            {
                try
                {
                    new EmailService().SendDonationApprovedEmail(donation.DonorEmail, donation.DonorName, donation.ItemName, date.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Donation-approved email failed: " + ex.Message);
                }
            }

            TempData["SuccessMessage"] = $"Donation #{donation.Id} approved — intake scheduled for {date.Value:dd MMM yyyy}.";
            return RedirectToAction("ApproveDonation");
        }

        // UC03 — lets the Approve modal show remaining slots as the admin
        // picks a date, before they submit and hit the server-side cap above.
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<JsonResult> IntakeCapacity(string date)
        {
            var parsed = ParseDate(date);
            if (!parsed.HasValue)
                return Json(new { success = false, message = "Invalid date" }, JsonRequestBehavior.AllowGet);

            var booked = await _context.DonationItems
                .CountAsync(d => d.IsActive && !d.IsFoodItem
                                  && d.Status == DonationStatus.ApprovedAwaitingIntake
                                  && d.ScheduledIntakeDate.HasValue
                                  && DbFunctions.TruncateTime(d.ScheduledIntakeDate) == parsed.Value.Date);

            return Json(new { success = true, capacity = IntakeDailyCapacity, booked, remaining = Math.Max(0, IntakeDailyCapacity - booked) }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Confirm Intake & Generate QR Tag (UC04)

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> IntakeCalendar()
        {
            var scheduled = await _context.DonationItems
                .Where(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.ApprovedAwaitingIntake)
                .OrderBy(d => d.ScheduledIntakeDate)
                .ToListAsync();

            return View(scheduled);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConfirmIntake(int id, bool received, string reason)
        {
            var donation = await _context.DonationItems.FindAsync(id);
            if (donation == null || donation.Status != DonationStatus.ApprovedAwaitingIntake)
            {
                TempData["ErrorMessage"] = "Donation not found or not awaiting intake.";
                return RedirectToAction("IntakeCalendar");
            }

            var personName = GetCurrentPersonName();
            var personId = GetCurrentPersonId();

            if (!received)
            {
                donation.Status = DonationStatus.NotReceived;
                donation.IsActive = false;

                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = donation.Id,
                    Action = "NotReceived",
                    Description = $"Marked not received by {personName}." + (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason.Trim()),
                    ActorId = personId,
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Donation #{donation.Id} marked as not received.";
                return RedirectToAction("IntakeCalendar");
            }

            // The item's existing TrackingCode (already unique, set at
            // creation) becomes the QR tag payload — no new column needed.
            donation.Status = DonationStatus.Available;

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = donation.Id,
                Action = "IntakeConfirmed",
                Description = $"Physical intake confirmed by {personName}. QR tag issued ({donation.TrackingCode}). Item is now available for matching.",
                ActorId = personId,
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            try { DonationAutomationEngine.RunMatchingSweep(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Matching sweep error: " + ex.Message); }

            TempData["SuccessMessage"] = $"Intake confirmed — print the QR tag and attach it to the item.";
            return RedirectToAction("PrintTag", new { id = donation.Id });
        }

        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> PrintTag(int id)
        {
            var donation = await _context.DonationItems.FindAsync(id);
            if (donation == null) return HttpNotFound();

            return View(donation);
        }

        #endregion

        #region My Donations (status tracker)

        [HttpGet]
        // "My Donations" status tracker (UC02-UC04): PendingApproval ->
        // ApprovedAwaitingIntake -> Available, with no donor action needed
        // beyond the initial drop-off at the scheduled intake date.
        public async Task<ActionResult> MyPendingDeliveries()
        {
            var personId = GetCurrentPersonId();
            if (personId == 0) return RedirectToAction("Login", "Account");

            var pending = await _context.DonationItems
                .Where(d => d.DonorId == personId
                            && d.IsActive
                            && (d.Status == DonationStatus.PendingApproval
                                || d.Status == DonationStatus.ApprovedAwaitingIntake))
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            return View(pending);
        }

        #endregion

        #region Donation History

        public async Task<ActionResult> DonationHistory(string filter, string status, string search)
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

            if (!string.IsNullOrWhiteSpace(filter))
            {
                DonationCategory cat;
                if (Enum.TryParse(filter, out cat))
                    query = query.Where(d => d.Category == cat);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                DonationStatus st;
                if (Enum.TryParse(status, out st))
                    query = query.Where(d => d.Status == st);
            }

            var donations = await query
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                donations = donations.Where(d =>
                    (d.ItemName != null && d.ItemName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (d.DonorName != null && d.DonorName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (d.TrackingCode != null && d.TrackingCode.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            ViewBag.Categories = Enum.GetNames(typeof(DonationCategory)).ToList();
            ViewBag.Statuses = Enum.GetNames(typeof(DonationStatus)).ToList();
            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

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

        // UC05/UC06 — the admin Match Review queue: every system-proposed
        // "PendingReview" allocation, awaiting Confirm or Release. Food is
        // excluded — it has its own FoodAllocationEngine/CommitFoodAllocation
        // pathway, untouched by this redesign.
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> MatchDonations()
        {
            var proposals = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Include(a => a.Student)
                .Include(a => a.Student.User)
                .Where(a => a.IsActive && a.Status == "PendingReview"
                            && a.DonationItem != null && !a.DonationItem.IsFoodItem)
                .OrderBy(a => a.AllocationDate)
                .ToListAsync();

            var requestIds = proposals.Where(a => a.RequestId.HasValue).Select(a => a.RequestId.Value).Distinct().ToList();
            var reservedItems = await _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Where(i => requestIds.Contains(i.DonationRequestId) && i.Status == RequestItemStatus.Reserved)
                .ToListAsync();

            var viewModel = proposals.Select(a =>
            {
                var requestItem = reservedItems.FirstOrDefault(i => i.DonationRequestId == a.RequestId
                    && a.DonationItem != null && i.Category == a.DonationItem.Category);

                return new MatchReviewViewModel
                {
                    AllocationId = a.Id,
                    DonationItemId = a.DonationItemId,
                    ItemName = a.DonationItem?.ItemName,
                    Category = a.DonationItem?.Category ?? DonationCategory.Other,
                    DonorName = a.DonationItem?.DonorName,
                    QuantityAvailableOnItem = a.DonationItem?.QuantityRemaining ?? 0,
                    RequestId = a.RequestId ?? 0,
                    RequestItemId = requestItem?.Id ?? 0,
                    StudentId = a.StudentId,
                    StudentName = a.Student?.FullName,
                    StudentNumber = a.Student?.User?.StudentNumber,
                    Priority = requestItem?.Priority ?? RequestPriority.Medium,
                    RequestDate = requestItem?.DonationRequest?.RequestDate ?? a.AllocationDate,
                    QuantityAllocated = a.QuantityAllocated,
                    MatchReason = a.Notes,
                    ProposedDate = a.AllocationDate
                };
            }).ToList();

            var unmatchedWaiting = await _context.DonationRequestItems
                .Where(i => i.Category != DonationCategory.Food
                            && i.DonationRequest.IsActive && !i.IsFulfilled
                            && i.Status == RequestItemStatus.Pending)
                .CountAsync();

            var availableItems = await _context.DonationItems
                .CountAsync(d => d.IsActive && !d.IsFoodItem && d.Status == DonationStatus.Available && d.QuantityRemaining > 0);

            ViewBag.PendingReviewCount = viewModel.Count;
            ViewBag.UnmatchedWaitingCount = unmatchedWaiting;
            ViewBag.AvailableItemsCount = availableItems;

            return View(viewModel);
        }

        // Manual trigger so the "smart" matching is demoable without
        // waiting on the Global.asax timer (UC05).
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public JsonResult RunMatchingNow()
        {
            try
            {
                var created = DonationAutomationEngine.RunMatchingSweep();
                return Json(new { success = true, message = created > 0 ? $"{created} new match(es) proposed." : "No new matches right now." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Matching sweep failed: " + ex.Message });
            }
        }

        // UC06 — admin confirms a proposed match and books a collection
        // date. This is when the learner first hears about it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConfirmMatch(int allocationId, string collectionDate)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Include(a => a.Student)
                .Include(a => a.Student.User)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive && a.Status == "PendingReview");

            if (allocation == null)
            {
                TempData["ErrorMessage"] = "Match proposal not found or already handled.";
                return RedirectToAction("MatchDonations");
            }

            var date = ParseDate(collectionDate);
            if (!date.HasValue)
            {
                TempData["ErrorMessage"] = "Please choose a valid collection date.";
                return RedirectToAction("MatchDonations");
            }

            allocation.Status = "AwaitingCollection";
            allocation.ScheduledCollectionDate = date.Value;
            if (string.IsNullOrEmpty(allocation.CollectionToken))
                allocation.CollectionToken = "COL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

            var requestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(i => i.DonationRequestId == allocation.RequestId
                                           && i.Category == allocation.DonationItem.Category
                                           && i.Status == RequestItemStatus.Reserved);
            if (requestItem != null)
            {
                requestItem.QuantityReceived += allocation.QuantityAllocated;
                requestItem.Status = RequestItemStatus.Allocated;
                if (requestItem.QuantityReceived >= requestItem.QuantityRequested)
                {
                    requestItem.IsFulfilled = true;
                    requestItem.FulfilledDate = DateTime.Now;
                }
            }

            var request = await _context.DonationRequests.FindAsync(allocation.RequestId);
            if (request != null)
            {
                var items = _context.DonationRequestItems.Where(i => i.DonationRequestId == request.Id).ToList();
                if (items.All(i => i.IsFulfilled || i.Status == RequestItemStatus.Declined))
                {
                    request.IsFulfilled = true;
                    request.FulfilledDate = DateTime.Now;
                }
            }

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = allocation.DonationItemId,
                Action = "MatchConfirmed",
                Description = $"{GetCurrentPersonName()} confirmed the match for {allocation.Student?.FullName} and scheduled collection for {date.Value:dd MMM yyyy}.",
                ActorId = GetCurrentPersonId(),
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var toEmail = allocation.Student?.User?.Email;
            if (!string.IsNullOrEmpty(toEmail))
            {
                try
                {
                    new EmailService().SendDonationMatchConfirmedEmail(
                        toEmail, allocation.Student.FullName, allocation.DonationItem?.ItemName ?? "your item",
                        date.Value, allocation.CollectionToken);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Match-confirmed email failed: " + ex.Message);
                }
            }

            TempData["SuccessMessage"] = $"Match confirmed — {allocation.Student?.FullName} has been emailed their collection code.";
            return RedirectToAction("MatchDonations");
        }

        // UC06 — releases a proposed match the admin doesn't want to confirm.
        // The item returns to the Available pool and the request returns to
        // Waitlisted at its original priority score — no penalty for an
        // admin override.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ReleaseMatch(int allocationId)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive && a.Status == "PendingReview");

            if (allocation == null)
            {
                TempData["ErrorMessage"] = "Match proposal not found or already handled.";
                return RedirectToAction("MatchDonations");
            }

            allocation.IsActive = false;
            allocation.Status = "Released";

            var requestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(i => i.DonationRequestId == allocation.RequestId
                                           && i.Category == allocation.DonationItem.Category
                                           && i.Status == RequestItemStatus.Reserved);
            if (requestItem != null)
            {
                requestItem.Status = RequestItemStatus.Pending;
            }

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = allocation.DonationItemId,
                Action = "MatchReleased",
                Description = $"{GetCurrentPersonName()} released a proposed match back to the pool.",
                ActorId = GetCurrentPersonId(),
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Match released back to the pool.";
            return RedirectToAction("MatchDonations");
        }

        // UC06 — lets the admin swap a proposed match for a different
        // available item of the same category, instead of releasing it back
        // to the pool and hoping the automated sweep (UC05) finds something
        // better. The original proposal is released; a fresh PendingReview
        // proposal is created for the same student/request against the
        // chosen item, so it goes through the same Confirm/Release path.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ReassignMatch(int allocationId, int newDonationItemId)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive && a.Status == "PendingReview");

            if (allocation == null)
            {
                TempData["ErrorMessage"] = "Match proposal not found or already handled.";
                return RedirectToAction("MatchDonations");
            }

            var newItem = await _context.DonationItems.FindAsync(newDonationItemId);
            if (newItem == null || !newItem.IsActive || newItem.IsFoodItem
                || newItem.Status != DonationStatus.Available || newItem.QuantityRemaining <= 0
                || newItem.Category != allocation.DonationItem.Category)
            {
                TempData["ErrorMessage"] = "That item isn't available to reassign to.";
                return RedirectToAction("MatchDonations");
            }

            var personName = GetCurrentPersonName();
            var personId = GetCurrentPersonId();
            var oldItemName = allocation.DonationItem?.ItemName;

            allocation.IsActive = false;
            allocation.Status = "Reassigned";

            var reassigned = new DonationAllocation
            {
                DonationItemId = newItem.Id,
                StudentId = allocation.StudentId,
                RequestId = allocation.RequestId,
                QuantityAllocated = Math.Min(allocation.QuantityAllocated, newItem.QuantityRemaining),
                Status = "PendingReview",
                AllocationDate = DateTime.Now,
                IsActive = true,
                Notes = $"Reassigned by {personName} from \"{oldItemName}\"."
            };
            _context.DonationAllocations.Add(reassigned);

            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = allocation.DonationItemId,
                Action = "MatchReassigned",
                Description = $"{personName} reassigned this proposed match to \"{newItem.ItemName}\" instead.",
                ActorId = personId,
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });
            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = newItem.Id,
                Action = "MatchReassigned",
                Description = $"{personName} reassigned a proposed match onto this item (from \"{oldItemName}\").",
                ActorId = personId,
                ActorType = GetCurrentPersonType(),
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Reassigned to \"{newItem.ItemName}\" — review it in the list below.";
            return RedirectToAction("MatchDonations");
        }

        // Alternative Available items in the same category, for the Reassign
        // modal's dropdown.
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<JsonResult> GetAlternativeItems(string category, int excludeItemId)
        {
            var cat = ParseEnum(category, DonationCategory.Other);

            var items = await _context.DonationItems
                .Where(d => d.IsActive && !d.IsFoodItem && d.Category == cat
                            && d.Status == DonationStatus.Available && d.QuantityRemaining > 0
                            && d.Id != excludeItemId)
                .OrderBy(d => d.DonationDate)
                .Select(d => new { id = d.Id, itemName = d.ItemName, donorName = d.DonorName, quantityRemaining = d.QuantityRemaining, condition = d.Condition })
                .ToListAsync();

            return Json(new { success = true, items }, JsonRequestBehavior.AllowGet);
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
                // Food requests are handled by their own allocation engine
                // (AutoMatchFood/CommitFoodAllocation) — excluded here.
                foreach (var item in r.Items.Where(i => !i.IsFulfilled && i.Status != RequestItemStatus.Declined && i.Category != DonationCategory.Food))
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

        #region Donation Insights (UC09)

        // Scoped entirely to the non-food pipeline this redesign covers —
        // Food has its own allocation engine and its own metrics aren't
        // mixed in here. days defaults to a trailing 30-day window.
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> Insights(int days = 30)
        {
            if (days < 1) days = 30;
            var rangeEnd = DateTime.Now;
            var rangeStart = rangeEnd.AddDays(-days);

            var viewModel = new DonationInsightsViewModel
            {
                RangeStart = rangeStart,
                RangeEnd = rangeEnd
            };

            var categories = Enum.GetValues(typeof(DonationCategory))
                .Cast<DonationCategory>()
                .Where(c => c != DonationCategory.Food)
                .ToList();

            foreach (var category in categories)
            {
                var openRequests = await _context.DonationRequestItems
                    .CountAsync(i => i.Category == category
                                      && i.DonationRequest.IsActive
                                      && !i.IsFulfilled
                                      && i.Status != RequestItemStatus.Declined);

                var availableItems = await _context.DonationItems
                    .CountAsync(d => d.IsActive && !d.IsFoodItem && d.Category == category
                                      && d.Status == DonationStatus.Available && d.QuantityRemaining > 0);

                viewModel.CategoryStats.Add(new DonationCategoryStat
                {
                    Category = category.ToString(),
                    OpenRequests = openRequests,
                    AvailableItems = availableItems
                });
            }

            var nonFoodItems = await _context.DonationRequestItems
                .Include(i => i.DonationRequest)
                .Where(i => i.Category != DonationCategory.Food
                            && i.DonationRequest.RequestDate >= rangeStart
                            && i.DonationRequest.RequestDate <= rangeEnd)
                .ToListAsync();

            viewModel.TotalRequestsInRange = nonFoodItems.Count;
            var fulfilledInRange = nonFoodItems.Where(i => i.IsFulfilled && i.FulfilledDate.HasValue).ToList();
            viewModel.FulfilledInRange = fulfilledInRange.Count;
            viewModel.FulfillmentRatePercent = viewModel.TotalRequestsInRange > 0
                ? Math.Round(100.0 * viewModel.FulfilledInRange / viewModel.TotalRequestsInRange, 1)
                : 0;

            viewModel.AverageWaitDays = fulfilledInRange.Any()
                ? Math.Round(fulfilledInRange.Average(i => (i.FulfilledDate.Value - i.DonationRequest.RequestDate).TotalDays), 1)
                : 0;

            var collectedAllocations = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Where(a => a.DonationItem != null && !a.DonationItem.IsFoodItem
                            && a.Status == "Collected"
                            && a.CollectionDate != null
                            && a.CollectionDate >= rangeStart && a.CollectionDate <= rangeEnd)
                .CountAsync();

            // No dedicated "went unclaimed on" column — the scheduled
            // collection date (the window that lapsed) is the closest honest
            // proxy, and DonationAutomationEngine.ReassignUnclaimedSweep is
            // the only thing that ever sets this status.
            var unclaimedAllocations = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Where(a => a.DonationItem != null && !a.DonationItem.IsFoodItem
                            && a.Status == "Unclaimed"
                            && a.ScheduledCollectionDate != null
                            && a.ScheduledCollectionDate >= rangeStart && a.ScheduledCollectionDate <= rangeEnd)
                .CountAsync();

            viewModel.CollectedInRange = collectedAllocations;
            viewModel.UnclaimedInRange = unclaimedAllocations;
            var collectionOutcomes = collectedAllocations + unclaimedAllocations;
            viewModel.UnclaimedRatePercent = collectionOutcomes > 0
                ? Math.Round(100.0 * unclaimedAllocations / collectionOutcomes, 1)
                : 0;

            ViewBag.Days = days;

            return View(viewModel);
        }

        #endregion

        #region Distribution (Collection Worklist — UC06/UC07)

        // One combined worklist:
        //   • "Pending"            — needs a collection date. In practice this is
        //                             food, straight from CommitFoodAllocation (the
        //                             untouched food engine) — old flow, unchanged.
        //   • "Ready"               — food, already scheduled via ScheduleDistribution
        //                             below, awaiting the PIN-based collection — old
        //                             flow, unchanged.
        //   • "AwaitingCollection"  — non-food, matched by DonationAutomationEngine
        //                             and confirmed by an admin via ConfirmMatch
        //                             (UC06) — already has a collection date, ready
        //                             for the new QR dual-verification collection.
        // ConfirmCollection below branches purely on Status, so one worklist safely
        // serves both the untouched food path and the new non-food path.
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult> Distribution()
        {
            var allocations = await _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.Student.User)
                .Include(a => a.DonationItem)
                .Where(a => a.IsActive && (a.Status == "Pending" || a.Status == "Ready" || a.Status == "AwaitingCollection"))
                .ToListAsync();

            var viewModel = allocations
                .OrderBy(a => a.Status == "Pending" ? 0 : 1)
                .ThenBy(a => a.ScheduledCollectionDate ?? a.AllocationDate)
                .Select(a => new DonationDistributionViewModel
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
                    IsFoodItem = a.DonationItem?.IsFoodItem ?? false,
                    DonationItemId = a.DonationItemId,
                    ItemTrackingCode = a.DonationItem?.TrackingCode
                }).ToList();

            ViewBag.NeedsSchedulingCount = viewModel.Count(v => v.Status == "Pending");
            ViewBag.ReadyForCollectionCount = viewModel.Count(v => v.Status == "Ready" || v.Status == "AwaitingCollection");

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,Teacher")]
        public ActionResult ProcessCollection(string token)
        {
            if (string.IsNullOrEmpty(token))
                return View(new DonationDistributionViewModel());

            var allocation = _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.Student.User)
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
                IsFoodItem = allocation.DonationItem?.IsFoodItem ?? false,
                DonationItemId = allocation.DonationItemId,
                ItemTrackingCode = allocation.DonationItem?.TrackingCode
            };

            return View(viewModel);
        }

        // Food-only in practice (Status starts "Pending" only from
        // CommitFoodAllocation now that AllocateDonation has no view calling
        // it). Scheduling always leaves the allocation collection-ready: it
        // sets the date, flips status to Ready, and makes sure a collection
        // token/QR/PIN exist — no separate "generate token" step is required
        // before collection can happen. Left exactly as it worked before the
        // redesign.
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
        // student lost their PIN, or a non-food collection code needs
        // resending). Works for either path — untouched.
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

        // UC07 step 1 of 2 — "peek" before finalizing. Validates the
        // collection code and the scanned/typed QR tag exactly like
        // ConfirmCollection below does, but does NOT mutate anything. Lets
        // the admin see the item's condition photo and the learner's name
        // and confirm this is really the right hand-over before the final,
        // irreversible step (ConfirmCollection).
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> VerifyItemTag(int allocationId, string token, string scannedItemCode)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive);

            if (allocation == null)
                return Json(new { success = false, message = "Allocation not found" });

            if (string.IsNullOrEmpty(allocation.CollectionToken) || allocation.CollectionToken != (token ?? "").Trim().ToUpper())
                return Json(new { success = false, message = "Invalid collection code" });

            if (allocation.Status != "AwaitingCollection")
                return Json(new { success = false, message = "This allocation isn't awaiting the QR hand-over step." });

            var expectedTag = (allocation.DonationItem?.TrackingCode ?? "").Trim();
            var scanned = (scannedItemCode ?? "").Trim();

            if (string.IsNullOrEmpty(scanned))
                return Json(new { success = false, message = "Scan the item's QR tag (or type its code) to verify." });

            if (string.IsNullOrEmpty(expectedTag) || !expectedTag.Equals(scanned, StringComparison.OrdinalIgnoreCase))
            {
                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = allocation.DonationItemId,
                    Action = "CollectionMismatch",
                    Description = $"{GetCurrentPersonName()} scanned a QR tag that did not match the item on this allocation (scanned: \"{scanned}\").",
                    ActorId = GetCurrentPersonId(),
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });
                await _context.SaveChangesAsync();

                return Json(new { success = false, invalidItem = true, message = "Invalid item — that QR tag doesn't match what was matched to this student." });
            }

            return Json(new
            {
                success = true,
                studentName = allocation.Student?.FullName,
                itemName = allocation.DonationItem?.ItemName,
                quantity = allocation.QuantityAllocated,
                condition = allocation.DonationItem?.Condition,
                photoUrl = allocation.DonationItem?.PhotoEvidence
            });
        }

        // UC07 step 2 of 2 — dual-verification collection, the final,
        // irreversible hand-over. Both paths start by matching the
        // allocation's Collection Code (the first factor, always present);
        // which second factor is required then branches purely on Status,
        // since that's set by two entirely different flows:
        //   • Status == "Ready"              → food (or any legacy manually
        //     scheduled allocation) — unchanged PIN-based confirmation.
        //   • Status == "AwaitingCollection"  → non-food — the second factor
        //     is the physical item's own printed QR tag (DonationItem.TrackingCode),
        //     scanned or typed in as scannedItemCode. This is the piece the
        //     user's original design called for: admin searches the code,
        //     sees the item details + a Scan QR button, scans the tag, and the
        //     system checks it matches before handing anything over.
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [ValidateJsonAntiForgeryToken]
        public async Task<JsonResult> ConfirmCollection(int allocationId, string token, string pinCode, string scannedItemCode)
        {
            var allocation = await _context.DonationAllocations
                .Include(a => a.Student)
                .Include(a => a.Student.User)
                .Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.IsActive);

            if (allocation == null)
                return Json(new { success = false, message = "Allocation not found" });

            if (string.IsNullOrEmpty(allocation.CollectionToken) || allocation.CollectionToken != (token ?? "").Trim().ToUpper())
                return Json(new { success = false, message = "Invalid collection code" });

            if (allocation.Status == "AwaitingCollection")
            {
                // ── Non-food — second factor is the scanned/typed QR tag ──
                var expectedTag = (allocation.DonationItem?.TrackingCode ?? "").Trim();
                var scanned = (scannedItemCode ?? "").Trim();

                if (string.IsNullOrEmpty(scanned))
                    return Json(new { success = false, message = "Scan the item's QR tag (or type its code) to confirm." });

                if (string.IsNullOrEmpty(expectedTag) || !expectedTag.Equals(scanned, StringComparison.OrdinalIgnoreCase))
                {
                    _context.DonationHistories.Add(new DonationHistory
                    {
                        DonationItemId = allocation.DonationItemId,
                        Action = "CollectionMismatch",
                        Description = $"{GetCurrentPersonName()} scanned a QR tag that did not match the item on this allocation (scanned: \"{scanned}\").",
                        ActorId = GetCurrentPersonId(),
                        ActorType = GetCurrentPersonType(),
                        ActionDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    return Json(new { success = false, invalidItem = true, message = "Invalid item — that QR tag doesn't match what was matched to this student." });
                }

                allocation.Status = "Collected";
                allocation.QuantityCollected = allocation.QuantityAllocated;
                allocation.CollectionDate = DateTime.Now;
                allocation.CollectedBy = GetCurrentPersonId();

                var item = await _context.DonationItems.FindAsync(allocation.DonationItemId);
                if (item != null)
                {
                    item.QuantityRemaining = Math.Max(0, item.QuantityRemaining - allocation.QuantityAllocated);
                    if (item.QuantityRemaining == 0)
                        item.Status = DonationStatus.Collected;
                }

                _context.DonationHistories.Add(new DonationHistory
                {
                    DonationItemId = allocation.DonationItemId,
                    Action = "Collected",
                    Description = $"{GetCurrentPersonName()} confirmed collection for {allocation.Student?.FullName} — collection code and scanned QR tag both matched.",
                    ActorId = GetCurrentPersonId(),
                    ActorType = GetCurrentPersonType(),
                    ActionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();

                var toEmail = allocation.Student?.User?.Email;
                if (!string.IsNullOrEmpty(toEmail))
                {
                    try
                    {
                        new EmailService().SendCollectionReceiptEmail(
                            toEmail, allocation.Student.FullName, allocation.DonationItem?.ItemName ?? "your item", allocation.CollectionDate.Value);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Collection-receipt email failed: " + ex.Message);
                    }
                }

                return Json(new { success = true, message = "Collection confirmed!" });
            }

            // ── Food (and any legacy manually-scheduled allocation) — unchanged PIN flow ──
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