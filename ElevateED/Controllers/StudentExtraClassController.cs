using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentExtraClassController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private EmailService _emailService = new EmailService();

        // ============================================
        // LIST AVAILABLE CLASSES
        // ============================================

        public ActionResult Index()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var studentGradeId = student.Class?.GradeId;
            if (!studentGradeId.HasValue)
            {
                TempData["ErrorMessage"] = "You are not assigned to a class yet.";
                return RedirectToAction("Dashboard", "Student");
            }

            var availableClasses = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Teacher)
                .Where(c => c.IsActive
                    && c.GradeId == studentGradeId.Value
                    && c.StartDate >= DateTime.Today
                    && c.CurrentEnrollment < c.Capacity)
                .OrderBy(c => c.StartDate)
                .ToList();

            var myEnrollments = _context.ExtraClassEnrollments
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .Select(e => e.ExtraClassId)
                .ToList();

            ViewBag.MyEnrollments = myEnrollments;
            ViewBag.StudentId = student.Id;

            return View(availableClasses);
        }

        // ============================================
        // MY ENROLLMENTS
        // ============================================

        public ActionResult MyEnrollments()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var enrollments = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .Include(e => e.ExtraClass.Subject)
                .Include(e => e.ExtraClass.Grade)
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            return View(enrollments);
        }
        // ============================================
        // CLASS DETAILS
        // ============================================

        public ActionResult Details(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Teacher)
                .FirstOrDefault(c => c.Id == id);

            if (extraClass == null) return HttpNotFound();

            var enrollment = _context.ExtraClassEnrollments
                .FirstOrDefault(e => e.ExtraClassId == id && e.StudentId == student.Id && e.IsActive);

            var attendanceRecords = _context.ExtraClassAttendanceRecords
                .Include(r => r.AttendanceSession)  // ADD THIS LINE
                .Where(r => r.StudentId == student.Id && r.AttendanceSession.ExtraClassId == id)
                .ToList();

            var myFeedback = _context.ExtraClassFeedbacks
                .FirstOrDefault(f => f.ExtraClassId == id && f.StudentId == student.Id);

            var totalSessions = _context.ExtraClassAttendanceSessions.Count(s => s.ExtraClassId == id);
            double attendancePercentage = 0;
            if (totalSessions > 0)
            {
                attendancePercentage = Math.Round((double)attendanceRecords.Count(r => r.IsPresent) / totalSessions * 100, 1);
            }

            var viewModel = new StudentExtraClassDetailViewModel
            {
                ExtraClass = extraClass,
                IsEnrolled = enrollment != null,
                IsPaid = enrollment?.IsPaid ?? false,
                Enrollment = enrollment,
                AttendanceRecords = attendanceRecords,
                PresentCount = attendanceRecords.Count(r => r.IsPresent),
                TotalSessions = totalSessions,
                AttendancePercentage = attendancePercentage,
                MyFeedback = myFeedback,
                CanProvideFeedback = enrollment != null && enrollment.IsActive && extraClass.Status == ExtraClassStatus.Completed && myFeedback == null
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Join(int classId)
        {
            try
            {
                var student = GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Student not found. Please log in again." });

                var extraClass = _context.ExtraClasses.Find(classId);
                if (extraClass == null)
                    return Json(new { success = false, message = "Class not found." });

                // Check if already enrolled
                var existingEnrollment = _context.ExtraClassEnrollments
                    .FirstOrDefault(e => e.ExtraClassId == classId && e.StudentId == student.Id && e.IsActive);

                if (existingEnrollment != null)
                    return Json(new { success = false, message = "You have already joined this class." });

                // Check capacity
                var currentCount = _context.ExtraClassEnrollments.Count(e => e.ExtraClassId == classId && e.IsActive);
                if (currentCount >= extraClass.Capacity)
                    return Json(new { success = false, message = "This class is full. Maximum capacity reached." });

                // Check if class has already started
                if (extraClass.StartDate < DateTime.Today)
                    return Json(new { success = false, message = "This class has already started. Enrollment is closed." });

                // Check if class is active
                if (!extraClass.IsActive)
                    return Json(new { success = false, message = "This class is no longer available." });

                // Create enrollment
                var enrollment = new ExtraClassEnrollment
                {
                    ExtraClassId = classId,
                    StudentId = student.Id,
                    IsPaid = false,
                    PaymentDate = DateTime.Now, // Changed from DateTime.MinValue
                    PaymentReference = null,
                    EnrollmentDate = DateTime.Now,
                    IsActive = true
                };

                _context.ExtraClassEnrollments.Add(enrollment);

                // Update current enrollment count
                extraClass.CurrentEnrollment = currentCount + 1;

                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = extraClass.Price > 0
                        ? "Successfully enrolled! Please complete payment to confirm your spot."
                        : "Successfully enrolled! This is a FREE class.",
                    requiresPayment = extraClass.Price > 0
                });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"Property: {x.PropertyName}, Error: {x.ErrorMessage}");

                var fullErrorMessage = string.Join("; ", errorMessages);

                System.Diagnostics.Debug.WriteLine($"VALIDATION ERROR: {fullErrorMessage}");

                return Json(new { success = false, message = "Validation error: " + fullErrorMessage });
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                // This catches database update errors
                var innerException = ex.InnerException;
                while (innerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"DB UPDATE ERROR: {innerException.Message}");

                    // Check for SQL Server specific errors
                    if (innerException is System.Data.SqlClient.SqlException sqlEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"SQL ERROR NUMBER: {sqlEx.Number}");
                        System.Diagnostics.Debug.WriteLine($"SQL ERROR: {sqlEx.Message}");

                        // Foreign key violation
                        if (sqlEx.Number == 547)
                        {
                            return Json(new { success = false, message = "Cannot enroll: Invalid student or class reference." });
                        }

                        // Primary key violation
                        if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
                        {
                            return Json(new { success = false, message = "You are already enrolled in this class." });
                        }
                    }

                    innerException = innerException.InnerException;
                }

                var fullErrorMessage = ex.InnerException?.Message ?? ex.Message;

                // Get the full stack trace
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {ex.InnerException.StackTrace}");
                }

                return Json(new { success = false, message = "Database error: " + fullErrorMessage });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GENERAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"INNER ERROR: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"STACK TRACE: {ex.InnerException.StackTrace}");
                }

                return Json(new
                {
                    success = false,
                    message = "An error occurred while enrolling. Error: " +
                              (ex.InnerException?.Message ?? ex.Message)
                });
            }
        }

        // ============================================
        // PAY WITH CARD - GET
        // ============================================

        public ActionResult PayWithCard(int enrollmentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var enrollment = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .Include(e => e.ExtraClass.Subject)
                .Include(e => e.ExtraClass.Grade)
                .FirstOrDefault(e => e.Id == enrollmentId && e.StudentId == student.Id);

            if (enrollment == null)
            {
                TempData["ErrorMessage"] = "Enrollment not found.";
                return RedirectToAction("MyEnrollments");
            }

            if (enrollment.IsPaid)
            {
                TempData["ErrorMessage"] = "Payment has already been completed.";
                return RedirectToAction("MyEnrollments");
            }

            // Return the enrollment directly to the view
            return View(enrollment);
        }

        // ============================================
        // PROCESS CARD PAYMENT - POST (FIXED)
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessCardPayment(int enrollmentId, string cardNumber, string cardHolder, string expiryMonth, string expiryYear, string cvv)
        {
            try
            {
                var student = GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Student not found" });

                var enrollment = _context.ExtraClassEnrollments
                    .Include(e => e.ExtraClass)
                    .FirstOrDefault(e => e.Id == enrollmentId && e.StudentId == student.Id);

                if (enrollment == null)
                    return Json(new { success = false, message = "Enrollment not found" });

                if (enrollment.IsPaid)
                    return Json(new { success = false, message = "Payment already completed" });

                // Basic validation
                if (string.IsNullOrEmpty(cardNumber) || cardNumber.Replace(" ", "").Length < 13)
                    return Json(new { success = false, message = "Please enter a valid card number" });

                if (string.IsNullOrEmpty(cardHolder))
                    return Json(new { success = false, message = "Please enter card holder name" });

                if (string.IsNullOrEmpty(expiryMonth) || string.IsNullOrEmpty(expiryYear))
                    return Json(new { success = false, message = "Please select expiry date" });

                if (string.IsNullOrEmpty(cvv) || cvv.Length < 3)
                    return Json(new { success = false, message = "Please enter a valid CVV" });

                // Generate payment reference
                var paymentRef = "PAY-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + enrollment.Id;

                // Update enrollment payment details (no need for separate Payment record)
                enrollment.IsPaid = true;
                enrollment.PaymentDate = DateTime.Now;
                enrollment.PaymentReference = paymentRef;

                _context.SaveChanges();

                return Json(new { success = true, message = "Payment successful! Your enrollment is now confirmed." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Payment Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.InnerException.StackTrace}");
                }
                return Json(new
                {
                    success = false,
                    message = "Payment failed: " + (ex.InnerException?.Message ?? ex.Message)
                });
            }
        }

        // ============================================
        // MY ATTENDANCE HISTORY
        // ============================================

        public ActionResult MyAttendance()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var attendance = _context.ExtraClassAttendanceRecords
                .Include(r => r.AttendanceSession)
                .Include(r => r.AttendanceSession.ExtraClass)
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.ScanTime)
                .ToList();

            return View(attendance);
        }

        // ============================================
        // SUBMIT FEEDBACK
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitFeedback(int classId, int rating, string comment)
        {
            try
            {
                var student = GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Student not found" });

                var extraClass = _context.ExtraClasses.Find(classId);
                if (extraClass == null)
                    return Json(new { success = false, message = "Class not found" });

                var enrollment = _context.ExtraClassEnrollments
                    .FirstOrDefault(e => e.ExtraClassId == classId && e.StudentId == student.Id && e.IsActive);

                if (enrollment == null)
                    return Json(new { success = false, message = "You are not enrolled in this class" });

                if (extraClass.Status != ExtraClassStatus.Completed)
                    return Json(new { success = false, message = "Feedback can only be submitted after the class is completed" });

                var existingFeedback = _context.ExtraClassFeedbacks
                    .FirstOrDefault(f => f.ExtraClassId == classId && f.StudentId == student.Id);

                if (existingFeedback != null)
                    return Json(new { success = false, message = "You have already submitted feedback" });

                var feedback = new ExtraClassFeedback
                {
                    ExtraClassId = classId,
                    StudentId = student.Id,
                    Rating = rating,
                    Comment = comment ?? "",
                    DateSubmitted = DateTime.Now
                };

                _context.ExtraClassFeedbacks.Add(feedback);
                _context.SaveChanges();

                return Json(new { success = true, message = "Thank you for your feedback!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Feedback Error: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
        // ============================================
        // SCAN ATTENDANCE PAGE - GET (DISPLAY THE PAGE)
        // ============================================

        [HttpGet]
        public ActionResult ScanAttendance()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            return View(new ScanQRCodeViewModel());
        }
        // ============================================
        // SCAN ATTENDANCE (QR CODE)
        // ============================================

        [HttpPost]
        public ActionResult ScanAttendance(string qrCode)
        {
            try
            {
                var student = GetCurrentStudent();
                if (student == null)
                    return Json(new { success = false, message = "Student not found" });

                var session = _context.ExtraClassAttendanceSessions
                    .Include(s => s.ExtraClass)
                    .FirstOrDefault(s => s.QRCode == qrCode && s.QRCodeExpiry > DateTime.Now);

                if (session == null)
                    return Json(new { success = false, message = "Invalid or expired QR code" });

                var enrollment = _context.ExtraClassEnrollments
                    .FirstOrDefault(e => e.ExtraClassId == session.ExtraClassId && e.StudentId == student.Id && e.IsActive);

                if (enrollment == null)
                    return Json(new { success = false, message = "You are not enrolled in this class" });

                var existingRecord = _context.ExtraClassAttendanceRecords
                    .FirstOrDefault(r => r.AttendanceSessionId == session.Id && r.StudentId == student.Id);

                if (existingRecord != null && existingRecord.IsPresent)
                    return Json(new { success = false, message = "Attendance already marked" });

                if (existingRecord == null)
                {
                    var record = new ExtraClassAttendanceRecord
                    {
                        AttendanceSessionId = session.Id,
                        StudentId = student.Id,
                        ScanTime = DateTime.Now,
                        IsPresent = true,
                        Status = Models.AttendanceStatus.Present
                    };
                    _context.ExtraClassAttendanceRecords.Add(record);
                }
                else
                {
                    existingRecord.IsPresent = true;
                    existingRecord.ScanTime = DateTime.Now;
                    existingRecord.Status = Models.AttendanceStatus.Present;
                }

                _context.SaveChanges();

                return Json(new { success = true, message = $"Attendance marked for {session.ExtraClass?.Name}" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Attendance Error: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// ============================================
        // HELPER METHOD
        // ============================================

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.User)
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}