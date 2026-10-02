using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Services;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    // ────────────────────────────────────────────────────────────────
    //  Smart Donation Management System (UC01-UC09) — see
    //  Smart_Donation_System_Redesign.docx for the full specification
    //  this controller implements. Action regions below are grouped by
    //  use case; the exact flow-of-activities steps are referenced in
    //  comments at each step so the code can be checked against the doc.
    // ────────────────────────────────────────────────────────────────
    [Authorize]
    public class DonationController : Controller
    {
        private readonly ElevateEDContext _context = new ElevateEDContext();
        private readonly EmailService _emailService = new EmailService();
        private readonly DonationAutomationEngine _engine = new DonationAutomationEngine();

        private static int IntakeDailyCapacity
        {
            get
            {
                var raw = System.Configuration.ConfigurationManager.AppSettings["DonationIntakeDailyCapacity"];
                int parsed;
                return int.TryParse(raw, out parsed) && parsed > 0 ? parsed : 10;
            }
        }

        // Guided urgency list (UC01 step 4) — short and consistent, rather
        // than free text, so Priority Score stays comparable across requests.
        public static readonly List<KeyValuePair<string, RequestPriority>> UrgencyContexts = new List<KeyValuePair<string, RequestPriority>>
        {
            new KeyValuePair<string, RequestPriority>("I have no usable item at all (daily essential)", RequestPriority.Urgent),
            new KeyValuePair<string, RequestPriority>("Needed for an upcoming exam or assessment", RequestPriority.High),
            new KeyValuePair<string, RequestPriority>("My current item is damaged or worn out", RequestPriority.Medium),
            new KeyValuePair<string, RequestPriority>("I'd like a spare / backup item", RequestPriority.Low),
        };

        public static readonly List<string> CollectionTimeWindows = new List<string>
        {
            "08:00 - 10:00", "10:00 - 12:00", "12:00 - 14:00", "14:00 - 15:30"
        };

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

            var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
            return user?.Id ?? 0;
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

            var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
            if (user == null) return "Guest";
            var teacher = _context.Teachers.FirstOrDefault(t => t.UserId == user.Id);
            return teacher?.FullName ?? user.Email ?? "Guest";
        }

        private string GetCurrentPersonEmail()
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.StudentNumber == User.Identity.Name);
            return user?.Email ?? User.Identity.Name;
        }

        private static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            T result;
            return !string.IsNullOrWhiteSpace(s) && Enum.TryParse(s, out result) ? result : fallback;
        }

        // Live queue position within a category: rank among Waitlisted
        // request items, highest Priority Score first (UC01 step 9).
        private int ComputeQueuePosition(DonationRequestItem item)
        {
            var waitlisted = _context.DonationRequestItems
                .Include(ri => ri.DonationRequest)
                .Where(ri => ri.Category == item.Category && ri.Status == RequestItemStatus.Waitlisted && ri.DonationRequest.IsActive)
                .ToList();

            var ranked = waitlisted
                .OrderByDescending(ri => ri.DonationRequest.PriorityScore)
                .ThenByDescending(ri => ri.CalculateItemScore())
                .ToList();

            var position = ranked.FindIndex(ri => ri.Id == item.Id);
            return position >= 0 ? position + 1 : ranked.Count + 1;
        }

        private void LogHistory(int donationItemId, string action, string description, string additionalData = null)
        {
            _context.DonationHistories.Add(new DonationHistory
            {
                DonationItemId = donationItemId,
                Action = action,
                Description = description,
                ActorId = GetCurrentPersonId(),
                ActorType = GetCurrentPersonType(),
                AdditionalData = additionalData
            });
        }

        #endregion

        #region Index / Dashboard

        public ActionResult Index() => RedirectToAction("Dashboard");

        public async Task<ActionResult> Dashboard()
        {
            var model = new DonationDashboardViewModel
            {
                PersonName = GetCurrentPersonName(),
                PersonRole = GetCurrentPersonType()
            };

            if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                if (student != null)
                {
                    model.MyOpenRequests = await _context.DonationRequestItems
                        .CountAsync(ri => ri.DonationRequest.StudentId == student.Id && ri.DonationRequest.IsActive
                                           && (ri.Status == RequestItemStatus.Waitlisted || ri.Status == RequestItemStatus.Reserved));

                    model.MyAwaitingCollection = await _context.DonationAllocations
                        .CountAsync(a => a.StudentId == student.Id && a.IsActive && a.Status == "AwaitingCollection");

                    model.MyPendingDonations = await _context.DonationItems
                        .CountAsync(i => i.DonorId == student.Id && i.DonorType == "Student" && i.IsActive
                                          && (i.Status == DonationStatus.PendingApproval || i.Status == DonationStatus.ApprovedAwaitingIntake));
                }
            }

            if (User.IsInRole("Teacher"))
            {
                var personId = GetCurrentPersonId();
                model.MyPendingDonations = await _context.DonationItems
                    .CountAsync(i => i.DonorId == personId && i.DonorType == "Teacher" && i.IsActive
                                      && (i.Status == DonationStatus.PendingApproval || i.Status == DonationStatus.ApprovedAwaitingIntake));
            }

            if (User.IsInRole("Admin") || User.IsInRole("Principal"))
            {
                model.PendingApprovals = await _context.DonationItems.CountAsync(i => i.IsActive && i.Status == DonationStatus.PendingApproval);
                model.AwaitingIntakeToday = await _context.DonationItems.CountAsync(i => i.IsActive && i.Status == DonationStatus.ApprovedAwaitingIntake
                                                                                           && i.ScheduledIntakeDate.HasValue && DbFunctions.TruncateTime(i.ScheduledIntakeDate) == DateTime.Today);
                model.AvailableItems = await _context.DonationItems.CountAsync(i => i.IsActive && i.Status == DonationStatus.Available);
                model.PendingMatchReview = await _context.DonationAllocations.CountAsync(a => a.IsActive && a.Status == "PendingReview");
                model.AwaitingCollectionTotal = await _context.DonationAllocations.CountAsync(a => a.IsActive && a.Status == "AwaitingCollection");
                model.WaitlistedRequests = await _context.DonationRequestItems.CountAsync(ri => ri.Status == RequestItemStatus.Waitlisted && ri.DonationRequest.IsActive);

                model.RecentActivity = await _context.DonationHistories.OrderByDescending(h => h.ActionDate).Take(10).ToListAsync();
            }

            model.RecentDonations = await _context.DonationItems.Where(i => i.IsActive).OrderByDescending(i => i.DonationDate).Take(5).ToListAsync();
            model.RecentRequests = await _context.DonationRequests.Where(r => r.IsActive).OrderByDescending(r => r.RequestDate).Take(5).ToListAsync();

            return View(model);
        }

        #endregion

        #region UC01 — Request Donation Item

        [Authorize(Roles = "Student")]
        public ActionResult RequestDonation()
        {
            ViewBag.Categories = Enum.GetValues(typeof(DonationCategory)).Cast<DonationCategory>().Where(c => c != DonationCategory.Food).ToList();
            ViewBag.UrgencyContexts = UrgencyContexts;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult> RequestDonation(string itemsJson)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("RequestDonation");

            List<DonationRequestItemViewModel> items;
            try { items = JsonConvert.DeserializeObject<List<DonationRequestItemViewModel>>(itemsJson) ?? new List<DonationRequestItemViewModel>(); }
            catch { items = new List<DonationRequestItemViewModel>(); }

            if (!items.Any())
            {
                TempData["Error"] = "Please add at least one item to your request.";
                return RedirectToAction("RequestDonation");
            }

            // Step 6 — reject duplicate open requests for the same item/category.
            var openCategories = await _context.DonationRequestItems
                .Where(ri => ri.DonationRequest.StudentId == student.Id && ri.DonationRequest.IsActive
                             && (ri.Status == RequestItemStatus.Waitlisted || ri.Status == RequestItemStatus.Reserved || ri.Status == RequestItemStatus.AwaitingCollection))
                .Select(ri => ri.Category)
                .ToListAsync();

            items = items.Where(i => !openCategories.Contains(i.Category)).ToList();
            if (!items.Any())
            {
                TempData["Error"] = "You already have an open request in the same category — see My Requests.";
                return RedirectToAction("MyRequests");
            }

            var request = new DonationRequest { StudentId = student.Id };
            foreach (var vm in items)
            {
                request.Items.Add(new DonationRequestItem
                {
                    Category = vm.Category,
                    ItemType = vm.ItemType,
                    ItemName = vm.ItemName,
                    Description = vm.Description,
                    BookTitle = vm.BookTitle,
                    Subject = vm.Subject,
                    GradeLevel = vm.GradeLevel,
                    ISBN = vm.ISBN,
                    ClothingSize = vm.ClothingSize,
                    ClothingType = vm.ClothingType,
                    Gender = vm.Gender,
                    StationeryType = vm.StationeryType,
                    BrandPreference = vm.BrandPreference,
                    ItemSubCategory = vm.ItemSubCategory,
                    SizeSpecifications = vm.SizeSpecifications,
                    QuantityRequested = vm.QuantityRequested,
                    Priority = vm.Priority,
                    UrgencyReason = vm.UrgencyReason,
                    Status = RequestItemStatus.Waitlisted
                });
            }

            // Step 7 — Priority Score. Step 8 — Waitlisted.
            request.PriorityScore = request.CalculatePriorityScore();
            request.WaitlistedDate = DateTime.Now;
            _context.DonationRequests.Add(request);
            await _context.SaveChangesAsync();

            // Step 10 — confirmation with reference number; step 9 — queue position.
            var firstItem = request.Items.First();
            var queuePosition = ComputeQueuePosition(firstItem);
            try
            {
                _emailService.SendDonationRequestConfirmedEmail(GetCurrentPersonEmail(), student.FullName,
                    string.Join(", ", items.Select(i => i.ItemName)), $"REQ-{request.Id:D6}", queuePosition);
            }
            catch { /* don't block the request on a flaky SMTP server */ }

            TempData["Success"] = $"Request submitted — reference REQ-{request.Id:D6}. You're currently #{queuePosition} in the queue for {firstItem.Category}.";
            return RedirectToAction("MyRequests");
        }

        [Authorize(Roles = "Student")]
        public async Task<ActionResult> MyRequests()
        {
            var student = GetCurrentStudent();
            var requests = student == null
                ? new List<DonationRequest>()
                : await _context.DonationRequests.Include(r => r.Items).Where(r => r.StudentId == student.Id).OrderByDescending(r => r.RequestDate).ToListAsync();

            ViewBag.QueuePositions = requests
                .SelectMany(r => r.Items)
                .Where(i => i.Status == RequestItemStatus.Waitlisted)
                .ToDictionary(i => i.Id, i => ComputeQueuePosition(i));

            return View(requests);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<JsonResult> CancelRequestItem(int id)
        {
            var student = GetCurrentStudent();
            var item = await _context.DonationRequestItems.Include(i => i.DonationRequest)
                .FirstOrDefaultAsync(i => i.Id == id && i.DonationRequest.StudentId == student.Id);
            if (item == null || item.Status != RequestItemStatus.Waitlisted)
                return Json(new { success = false, message = "Only waitlisted items can be cancelled." });

            item.Status = RequestItemStatus.Declined;
            item.DeclineReason = "Cancelled by learner";
            item.DeclinedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        #endregion

        #region UC02 — Donate an Item

        public async Task<ActionResult> AddDonation()
        {
            ViewBag.Categories = Enum.GetValues(typeof(DonationCategory)).Cast<DonationCategory>().Where(c => c != DonationCategory.Food).ToList();

            // UC02 step 5 (Named Allocation) — a plain, searchable-by-eye
            // dropdown of active learners, so nothing needs to be typed.
            // Projected into a named public class (DonationStudentOption),
            // not an anonymous type — see that class's comment for why.
            ViewBag.Students = await _context.Students
                .Include(s => s.User)
                .Where(s => s.IsActive)
                .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
                .Select(s => new DonationStudentOption { Name = s.FirstName + " " + s.LastName, StudentNumber = s.User.StudentNumber })
                .ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<ActionResult> AddDonation(string itemsJson)
        {
            List<DonationItemEntry> entries;
            try { entries = JsonConvert.DeserializeObject<List<DonationItemEntry>>(itemsJson) ?? new List<DonationItemEntry>(); }
            catch { entries = new List<DonationItemEntry>(); }

            if (!entries.Any())
            {
                TempData["Error"] = "Please add at least one item to donate.";
                return RedirectToAction("AddDonation");
            }

            var donorId = GetCurrentPersonId();
            var donorType = GetCurrentPersonType();
            var donorName = GetCurrentPersonName();
            var donorEmail = GetCurrentPersonEmail();
            var referenceCodes = new List<string>();

            foreach (var entry in entries)
            {
                int? targetStudentId = null;
                if (entry.AllocationType == AllocationType.NamedAllocation && !string.IsNullOrWhiteSpace(entry.TargetStudentNumber))
                {
                    var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == entry.TargetStudentNumber);
                    if (targetUser != null)
                        targetStudentId = (await _context.Students.FirstOrDefaultAsync(s => s.UserId == targetUser.Id))?.Id;
                }

                var item = new DonationItem
                {
                    DonorId = donorId,
                    DonorType = donorType,
                    DonorName = donorName,
                    DonorEmail = donorEmail,
                    Category = entry.Category,
                    ItemType = entry.ItemType,
                    ItemName = entry.ItemName,
                    BookTitle = entry.BookTitle,
                    Subject = entry.Subject,
                    GradeLevel = entry.GradeLevel,
                    ISBN = entry.ISBN,
                    ClothingSize = entry.ClothingSize,
                    ClothingType = entry.ClothingType,
                    Gender = entry.Gender,
                    Quantity = entry.Quantity,
                    QuantityRemaining = entry.Quantity,
                    AllocationType = entry.AllocationType,
                    TargetStudentId = targetStudentId,
                    Condition = entry.Condition,
                    ConditionNotes = entry.ConditionNotes,
                    PhotoEvidence = entry.PhotoEvidence,
                    Status = DonationStatus.PendingApproval,
                    IsFoodItem = false
                };
                _context.DonationItems.Add(item);
                await _context.SaveChangesAsync(); // need item.Id for history

                LogHistory(item.Id, "Submitted", $"Donated by {donorName} ({donorType}).");
                referenceCodes.Add(item.TrackingCode);

                try { _emailService.SendDonationSubmittedEmail(donorEmail, donorName, item.ItemName, item.TrackingCode); }
                catch { /* don't block submission on a flaky SMTP server */ }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Thank you! {entries.Count} item(s) submitted for approval. Reference code(s): {string.Join(", ", referenceCodes)}.";
            return RedirectToAction("MyPendingDeliveries");
        }

        [HttpGet]
        public async Task<JsonResult> SearchStudents(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var matches = await _context.Students
                .Include(s => s.User)
                .Where(s => s.IsActive && (s.FirstName.Contains(query) || s.LastName.Contains(query) || s.User.StudentNumber.Contains(query)))
                .Take(10)
                .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, StudentNumber = s.User.StudentNumber })
                .ToListAsync();

            return Json(matches, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> MyPendingDeliveries()
        {
            var donorId = GetCurrentPersonId();
            var donorType = GetCurrentPersonType();
            var items = await _context.DonationItems
                .Where(i => i.DonorId == donorId && i.DonorType == donorType)
                .OrderByDescending(i => i.DonationDate)
                .ToListAsync();
            return View(items);
        }

        #endregion

        #region UC03 — Approve Donation & Schedule Intake

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ApproveDonation()
        {
            // Step 1 — oldest first, Named Allocations flagged separately.
            var pending = await _context.DonationItems
                .Where(i => i.IsActive && i.Status == DonationStatus.PendingApproval)
                .OrderBy(i => i.DonationDate)
                .ToListAsync();

            var targetIds = pending.Where(i => i.TargetStudentId.HasValue).Select(i => i.TargetStudentId.Value).Distinct().ToList();
            var genuineRequestCategories = await _context.DonationRequestItems
                .Where(ri => targetIds.Contains(ri.DonationRequest.StudentId) && ri.Status == RequestItemStatus.Waitlisted)
                .Select(ri => new { ri.DonationRequest.StudentId, ri.Category })
                .ToListAsync();

            ViewBag.GenuineNamedRequest = pending
                .Where(i => i.TargetStudentId.HasValue)
                .ToDictionary(i => i.Id, i => genuineRequestCategories.Any(g => g.StudentId == i.TargetStudentId.Value && g.Category == i.Category));

            ViewBag.DailyIntakeCapacity = IntakeDailyCapacity;
            return View(pending);
        }

        [HttpGet]
        public async Task<JsonResult> IntakeCapacity(string date)
        {
            DateTime parsed;
            if (!DateTime.TryParse(date, out parsed)) return Json(new { remaining = IntakeDailyCapacity }, JsonRequestBehavior.AllowGet);

            var booked = await _context.DonationItems.CountAsync(i => i.IsActive && i.Status == DonationStatus.ApprovedAwaitingIntake
                                                                        && i.ScheduledIntakeDate.HasValue && DbFunctions.TruncateTime(i.ScheduledIntakeDate) == parsed.Date);
            return Json(new { remaining = Math.Max(0, IntakeDailyCapacity - booked), capacity = IntakeDailyCapacity }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ApproveDonationSubmit(int id, string decision, string rejectionReason, string intakeDate)
        {
            var item = await _context.DonationItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null || item.Status != DonationStatus.PendingApproval) return RedirectToAction("ApproveDonation");

            if (decision == "reject")
            {
                // Step 3 — rejection requires a reason, emailed to the donor.
                item.Status = DonationStatus.Rejected;
                item.RejectionReason = rejectionReason;
                item.IsActive = false;
                LogHistory(item.Id, "Rejected", rejectionReason);
                await _context.SaveChangesAsync();

                try { _emailService.SendDonationRejectedEmail(item.DonorEmail, item.DonorName, item.ItemName, item.TrackingCode, rejectionReason); }
                catch { }

                TempData["Success"] = $"{item.ItemName} rejected and the donor notified.";
                return RedirectToAction("ApproveDonation");
            }

            // Step 4 — capacity-limited intake date.
            DateTime date;
            if (!DateTime.TryParse(intakeDate, out date))
            {
                TempData["Error"] = "Please choose a valid intake date.";
                return RedirectToAction("ApproveDonation");
            }

            var booked = await _context.DonationItems.CountAsync(i => i.IsActive && i.Status == DonationStatus.ApprovedAwaitingIntake
                                                                        && i.ScheduledIntakeDate.HasValue && DbFunctions.TruncateTime(i.ScheduledIntakeDate) == date.Date);
            if (booked >= IntakeDailyCapacity)
            {
                TempData["Error"] = $"{date:dd MMM yyyy} is fully booked ({IntakeDailyCapacity}/day) — please choose another date.";
                return RedirectToAction("ApproveDonation");
            }

            // Step 5 — status + scheduled date.
            item.Status = DonationStatus.ApprovedAwaitingIntake;
            item.ScheduledIntakeDate = date;
            LogHistory(item.Id, "Approved", $"Approved — intake booked for {date:dd MMM yyyy}.");
            await _context.SaveChangesAsync();

            // Step 6 — email the donor.
            try { _emailService.SendDonationApprovedEmail(item.DonorEmail, item.DonorName, item.ItemName, item.TrackingCode, date); }
            catch { }

            TempData["Success"] = $"{item.ItemName} approved — intake booked for {date:dd MMM yyyy}.";
            return RedirectToAction("ApproveDonation");
        }

        #endregion

        #region UC04 — Confirm Intake & Generate QR Tag

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> IntakeCalendar(string date)
        {
            DateTime parsed;
            var selectedDate = DateTime.TryParse(date, out parsed) ? parsed.Date : DateTime.Today;

            var scheduled = await _context.DonationItems
                .Where(i => i.IsActive && i.Status == DonationStatus.ApprovedAwaitingIntake
                            && i.ScheduledIntakeDate.HasValue && DbFunctions.TruncateTime(i.ScheduledIntakeDate) == selectedDate)
                .ToListAsync();

            return View(new IntakeCalendarViewModel { SelectedDate = selectedDate, ScheduledForDate = scheduled, DailyIntakeCapacity = IntakeDailyCapacity });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ConfirmIntake(int id)
        {
            var item = await _context.DonationItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null || item.Status != DonationStatus.ApprovedAwaitingIntake) return RedirectToAction("IntakeCalendar");

            // Step 4-6 — mark Received, generate QR tag (TrackingCode + Id
            // encode the tag — see PrintTag), add to Available Inventory.
            item.Status = DonationStatus.Available;
            item.VerificationDate = DateTime.Now;
            item.VerifiedBy = GetCurrentPersonId();
            LogHistory(item.Id, "IntakeConfirmed", $"Physical intake confirmed by {GetCurrentPersonName()}. QR tag: {item.TrackingCode}.");
            await _context.SaveChangesAsync();

            // Step 9 — eligible for matching; trigger a sweep immediately
            // rather than waiting for the scheduled run (UC05's triggering
            // event explicitly includes "a new item becomes Available").
            try { _engine.RunMatchingSweep(); } catch { }

            TempData["Success"] = $"{item.ItemName} received — print its QR tag and attach it to the item.";
            return RedirectToAction("PrintTag", new { id = item.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeclineIntake(int id, string reason)
        {
            var item = await _context.DonationItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null || item.Status != DonationStatus.ApprovedAwaitingIntake) return RedirectToAction("IntakeCalendar");

            // Step 3 — item doesn't match / condition changed materially.
            item.Status = DonationStatus.NotReceived;
            item.RejectionReason = reason;
            item.IsActive = false;
            LogHistory(item.Id, "NotReceived", reason);
            await _context.SaveChangesAsync();

            try { _emailService.SendDonationRejectedEmail(item.DonorEmail, item.DonorName, item.ItemName, item.TrackingCode, reason); }
            catch { }

            TempData["Success"] = $"{item.ItemName} marked Not Received and the donor notified.";
            return RedirectToAction("IntakeCalendar");
        }

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PrintTag(int id)
        {
            var item = await _context.DonationItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null) return RedirectToAction("IntakeCalendar");
            return View(item);
        }

        #endregion

        #region UC05 — Auto-Match Donations to Requests

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> MatchDonations()
        {
            var pending = await _context.DonationAllocations
                .Include(a => a.DonationItem)
                .Include(a => a.Student)
                .Include(a => a.Request)
                .Where(a => a.IsActive && a.Status == "PendingReview")
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            var vms = pending.Select(a => new MatchReviewItemViewModel
            {
                AllocationId = a.Id,
                DonationItemId = a.DonationItemId,
                ItemName = a.DonationItem.ItemName,
                ItemPhoto = a.DonationItem.PhotoEvidence,
                Condition = a.DonationItem.Condition,
                Category = a.DonationItem.Category,
                RequestId = a.RequestId ?? 0,
                StudentName = a.Student?.FullName,
                MatchScore = a.MatchScore ?? 0,
                MatchReason = a.MatchReason,
                ProposedDate = a.AllocationDate
            }).ToList();

            ViewBag.CollectionTimeWindows = CollectionTimeWindows;
            return View(vms);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult RunMatchingNow()
        {
            var result = _engine.RunMatchingSweep();
            TempData["Success"] = result.ProposalsCreated > 0
                ? $"Matching run complete — {result.ProposalsCreated} new proposal(s) created."
                : "Matching run complete — no new pairs found.";
            return RedirectToAction("MatchDonations");
        }

        #endregion

        #region UC06 — Confirm Match & Schedule Collection

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ConfirmMatch(int allocationId, string collectionDate, string timeWindow)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem).Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.Status == "PendingReview");
            if (allocation == null) return RedirectToAction("MatchDonations");

            DateTime date;
            if (!DateTime.TryParse(collectionDate, out date))
            {
                TempData["Error"] = "Please choose a valid collection date.";
                return RedirectToAction("MatchDonations");
            }

            var requestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(ri => ri.DonationRequestId == allocation.RequestId && ri.Status == RequestItemStatus.Reserved);

            // Step 3-4 — collection date/window, Collection Code already
            // generated when the allocation was created; status moves on.
            allocation.Status = "AwaitingCollection";
            allocation.ScheduledCollectionDate = date;
            allocation.CollectionTimeWindow = timeWindow;
            allocation.DonationItem.Status = DonationStatus.AwaitingCollection;
            if (requestItem != null) requestItem.Status = RequestItemStatus.AwaitingCollection;

            LogHistory(allocation.DonationItemId, "MatchConfirmed", $"Collection booked for {date:dd MMM yyyy} ({timeWindow}).");
            await _context.SaveChangesAsync();

            // Step 5 — email the learner the match, date, window, and code.
            try
            {
                var email = allocation.Student?.User?.Email;
                if (!string.IsNullOrEmpty(email))
                    _emailService.SendDonationMatchConfirmedEmail(email, allocation.Student.FullName, allocation.DonationItem.ItemName, date, timeWindow, allocation.CollectionToken);
            }
            catch { }

            TempData["Success"] = "Match confirmed and the learner notified.";
            return RedirectToAction("MatchDonations");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> GetAlternativeRequests(int allocationId)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem).FirstOrDefaultAsync(a => a.Id == allocationId);
            if (allocation == null) return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var alternatives = await _context.DonationRequestItems
                .Include(ri => ri.DonationRequest).Include(ri => ri.DonationRequest.Student)
                .Where(ri => ri.Category == allocation.DonationItem.Category && ri.Status == RequestItemStatus.Waitlisted && ri.Id != allocation.RequestId)
                .OrderByDescending(ri => ri.DonationRequest.PriorityScore)
                .Select(ri => new { ri.Id, RequestId = ri.DonationRequestId, StudentName = ri.DonationRequest.Student.FirstName + " " + ri.DonationRequest.Student.LastName, ri.DonationRequest.PriorityScore })
                .Take(20)
                .ToListAsync();

            return Json(alternatives, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ReassignMatch(int allocationId, int newRequestItemId)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.Status == "PendingReview");
            var newRequestItem = await _context.DonationRequestItems.Include(ri => ri.DonationRequest)
                .FirstOrDefaultAsync(ri => ri.Id == newRequestItemId && ri.Status == RequestItemStatus.Waitlisted);
            if (allocation == null || newRequestItem == null) return RedirectToAction("MatchDonations");

            // Release the originally-proposed request back to Waitlisted
            // (same item, different request — UC06 step 2).
            var oldRequestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(ri => ri.DonationRequestId == allocation.RequestId && ri.Status == RequestItemStatus.Reserved);
            if (oldRequestItem != null) oldRequestItem.Status = RequestItemStatus.Waitlisted;

            allocation.RequestId = newRequestItem.DonationRequestId;
            allocation.StudentId = newRequestItem.DonationRequest.StudentId;
            allocation.MatchReason = $"Reassigned by admin to {newRequestItem.DonationRequest.StudentId} — manual override.";
            newRequestItem.Status = RequestItemStatus.Reserved;

            LogHistory(allocation.DonationItemId, "MatchReassigned", allocation.MatchReason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Match reassigned to the selected request.";
            return RedirectToAction("MatchDonations");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ReleaseMatch(int allocationId)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.Status == "PendingReview");
            if (allocation == null) return RedirectToAction("MatchDonations");

            // Step 6 — item back to Available, request back to Waitlisted
            // at its original position (WaitlistedDate untouched).
            allocation.DonationItem.Status = DonationStatus.Available;
            allocation.Status = "Released";
            allocation.IsActive = false;

            var requestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(ri => ri.DonationRequestId == allocation.RequestId && ri.Status == RequestItemStatus.Reserved);
            if (requestItem != null) requestItem.Status = RequestItemStatus.Waitlisted;

            LogHistory(allocation.DonationItemId, "MatchReleased", "Released back to Available pool by admin.");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Match released — item returned to the pool.";
            return RedirectToAction("MatchDonations");
        }

        [Authorize(Roles = "Student")]
        public async Task<ActionResult> MyCollections()
        {
            var student = GetCurrentStudent();
            var allocations = student == null
                ? new List<DonationAllocation>()
                : await _context.DonationAllocations.Include(a => a.DonationItem)
                    .Where(a => a.StudentId == student.Id)
                    .OrderByDescending(a => a.AllocationDate)
                    .ToListAsync();

            var vms = allocations.Select(a => new DonationDistributionViewModel
            {
                AllocationId = a.Id,
                StudentName = student.FullName,
                ItemName = a.DonationItem.ItemName,
                ItemType = a.DonationItem.ItemType.ToString(),
                Quantity = a.QuantityAllocated,
                CollectionToken = a.CollectionToken,
                ScheduledDate = a.ScheduledCollectionDate,
                Status = a.Status
            }).ToList();

            return View(vms);
        }

        #endregion

        #region UC07 — Verify & Issue Item (Dual-Verification Collection)

        [Authorize(Roles = "Admin")]
        public ActionResult ProcessCollection() => View();

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> LookupCollectionCode(string code)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem).Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.CollectionToken == code && a.Status == "AwaitingCollection");

            // Step 4 — not found / expired.
            if (allocation == null)
                return Json(new CollectionLookupViewModel { Found = false, Message = "Invalid or expired Collection Code." }, JsonRequestBehavior.AllowGet);

            // Step 3 — item details, requester identity, condition photo, due date.
            return Json(new CollectionLookupViewModel
            {
                Found = true,
                AllocationId = allocation.Id,
                CollectionToken = allocation.CollectionToken,
                StudentName = allocation.Student?.FullName,
                ItemName = allocation.DonationItem.ItemName,
                ItemPhoto = allocation.DonationItem.PhotoEvidence,
                Condition = allocation.DonationItem.Condition,
                Quantity = allocation.QuantityAllocated,
                ScheduledCollectionDate = allocation.ScheduledCollectionDate,
                Status = allocation.Status
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> VerifyItemTag(int allocationId, string scannedCode)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem).Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.Status == "AwaitingCollection");
            if (allocation == null) return Json(new { success = false, message = "Allocation not found." });

            var expected = allocation.DonationItem.TrackingCode;
            var matches = !string.IsNullOrWhiteSpace(scannedCode) &&
                          (scannedCode.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase) || scannedCode.Contains(expected));

            if (!matches)
            {
                // Step 7 — mismatch logged against admin and item for audit.
                LogHistory(allocation.DonationItemId, "CollectionMismatch", $"Scanned '{scannedCode}' did not match expected tag '{expected}'.");
                await _context.SaveChangesAsync();
                return Json(new { success = false, message = "Invalid Item — scanned tag does not match this allocation." });
            }

            // Step 8 — ready for the Confirm Delivery window.
            return Json(new
            {
                success = true,
                itemName = allocation.DonationItem.ItemName,
                itemPhoto = allocation.DonationItem.PhotoEvidence,
                studentName = allocation.Student?.FullName
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> ConfirmCollection(int allocationId)
        {
            var allocation = await _context.DonationAllocations.Include(a => a.DonationItem).Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == allocationId && a.Status == "AwaitingCollection");
            if (allocation == null) return Json(new { success = false, message = "Allocation not found." });

            var item = allocation.DonationItem;
            var adminId = GetCurrentPersonId();
            var adminName = GetCurrentPersonName();

            // Step 10 — atomically mark Collected / Fulfilled, write receipt,
            // remove from active inventory.
            item.Status = DonationStatus.Collected;
            item.IsActive = false;
            item.CollectionDate = DateTime.Now;

            allocation.Status = "Collected";
            allocation.CollectionDate = DateTime.Now;
            allocation.CollectedBy = adminId;
            allocation.CollectionConfirmation = $"Confirmed by {adminName} at {DateTime.Now:dd MMM yyyy HH:mm}";

            var requestItem = await _context.DonationRequestItems
                .FirstOrDefaultAsync(ri => ri.DonationRequestId == allocation.RequestId && ri.Status == RequestItemStatus.AwaitingCollection);
            if (requestItem != null)
            {
                requestItem.Status = RequestItemStatus.Fulfilled;
                requestItem.IsFulfilled = true;
                requestItem.FulfilledDate = DateTime.Now;

                var parentRequest = await _context.DonationRequests.FirstOrDefaultAsync(r => r.Id == requestItem.DonationRequestId);
                if (parentRequest != null && !parentRequest.Items.Any(i => i.Status != RequestItemStatus.Fulfilled && i.Status != RequestItemStatus.Declined))
                {
                    parentRequest.IsFulfilled = true;
                    parentRequest.FulfilledDate = DateTime.Now;
                }
            }

            LogHistory(item.Id, "Collected",
                $"Collection receipt — item: {item.ItemName}, learner: {allocation.Student?.FullName}, admin: {adminName}, scan result: match.",
                $"AllocationId={allocation.Id};CollectedBy={adminId};Timestamp={DateTime.Now:O}");

            await _context.SaveChangesAsync();

            // Step 11 — digital receipt email.
            try
            {
                var email = allocation.Student?.User?.Email;
                if (!string.IsNullOrEmpty(email))
                    _emailService.SendCollectionReceiptEmail(email, allocation.Student.FullName, item.ItemName, allocation.CollectionDate.Value);
            }
            catch { }

            return Json(new { success = true, message = $"{item.ItemName} collected by {allocation.Student?.FullName}." });
        }

        #endregion

        #region History, Details, Waitlist (supporting transparency views)

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DonationHistory()
        {
            var history = await _context.DonationHistories.Include(h => h.DonationItem).OrderByDescending(h => h.ActionDate).Take(200).ToListAsync();
            return View(history);
        }

        public async Task<ActionResult> DonationDetails(int? id)
        {
            if (!id.HasValue) return RedirectToAction("Dashboard");

            var item = await _context.DonationItems.FirstOrDefaultAsync(i => i.Id == id.Value);
            if (item == null) return RedirectToAction("Dashboard");

            ViewBag.History = await _context.DonationHistories.Where(h => h.DonationItemId == id.Value).OrderBy(h => h.ActionDate).ToListAsync();
            ViewBag.Allocation = await _context.DonationAllocations.Include(a => a.Student).FirstOrDefaultAsync(a => a.DonationItemId == id.Value && a.IsActive);
            return View(item);
        }

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PriorityWaitlist()
        {
            var waitlisted = await _context.DonationRequestItems
                .Include(ri => ri.DonationRequest).Include(ri => ri.DonationRequest.Student)
                .Where(ri => ri.Status == RequestItemStatus.Waitlisted && ri.DonationRequest.IsActive)
                .ToListAsync();

            var ranked = waitlisted
                .GroupBy(ri => ri.Category)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(ri => ri.DonationRequest.PriorityScore).ThenByDescending(ri => ri.CalculateItemScore()).ToList());

            return View(ranked);
        }

        #endregion

        #region UC09 — View Donation Insights

        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Insights(DateTime? fromDate, DateTime? toDate, string category, string grade)
        {
            var from = fromDate ?? DateTime.Today.AddMonths(-3);
            var to = toDate ?? DateTime.Today;

            var requestsQuery = _context.DonationRequestItems.Include(ri => ri.DonationRequest)
                .Where(ri => ri.CreatedAt >= from && ri.CreatedAt <= to);
            if (!string.IsNullOrEmpty(category)) requestsQuery = requestsQuery.Where(ri => ri.Category.ToString() == category);
            if (!string.IsNullOrEmpty(grade)) requestsQuery = requestsQuery.Where(ri => ri.GradeLevel == grade);
            var requests = await requestsQuery.ToListAsync();

            var itemsQuery = _context.DonationItems.Where(i => i.DonationDate >= from && i.DonationDate <= to);
            if (!string.IsNullOrEmpty(category)) itemsQuery = itemsQuery.Where(i => i.Category.ToString() == category);
            var items = await itemsQuery.ToListAsync();

            var allocationsQuery = _context.DonationAllocations.Include(a => a.DonationItem).Where(a => a.AllocationDate >= from && a.AllocationDate <= to);
            var allocations = await allocationsQuery.ToListAsync();

            var model = new DonationInsightsViewModel
            {
                FromDate = from,
                ToDate = to,
                CategoryFilter = category,
                GradeFilter = grade,
                TotalRequests = requests.Count,
                TotalFulfilled = requests.Count(r => r.IsFulfilled),
                TotalDonationsReceived = items.Count(i => i.Status != DonationStatus.PendingApproval && i.Status != DonationStatus.Rejected),
                TotalUnclaimed = allocations.Count(a => a.Status == "Unclaimed")
            };

            model.FulfilmentRatePercent = model.TotalRequests > 0 ? Math.Round(100.0 * model.TotalFulfilled / model.TotalRequests, 1) : 0;
            model.UnclaimedRatePercent = allocations.Count > 0 ? Math.Round(100.0 * model.TotalUnclaimed / allocations.Count, 1) : 0;

            var waitTimes = requests.Where(r => r.IsFulfilled && r.FulfilledDate.HasValue)
                .Select(r => (r.FulfilledDate.Value - r.CreatedAt).TotalDays).ToList();
            model.AverageWaitDays = waitTimes.Any() ? Math.Round(waitTimes.Average(), 1) : 0;

            // Step 3 — supply/demand gaps per category.
            foreach (DonationCategory cat in Enum.GetValues(typeof(DonationCategory)))
            {
                if (cat == DonationCategory.Food) continue;
                model.CategoryGaps.Add(new CategoryGapViewModel
                {
                    Category = cat.ToString(),
                    OpenRequests = requests.Count(r => r.Category == cat && (r.Status == RequestItemStatus.Waitlisted || r.Status == RequestItemStatus.Reserved)),
                    AvailableItems = items.Count(i => i.Category == cat && i.Status == DonationStatus.Available)
                });
            }

            // Step 5 — where unclaimed items cluster, as a proxy for "most
            // common logged reasons" (every unclaimed event has the same
            // system reason — a lapsed collection window — so the
            // actionable signal is which categories it clusters in).
            model.TopUnclaimedReasons = allocations.Where(a => a.Status == "Unclaimed")
                .GroupBy(a => a.DonationItem.Category.ToString())
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .ToList();

            return View(model);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}
