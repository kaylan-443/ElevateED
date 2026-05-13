using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
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
                .Include(c => c.Enrollments)
                .Where(c => c.IsActive
                    && c.GradeId == studentGradeId.Value
                    && c.StartDate >= DateTime.Today
                    && c.CurrentEnrollment < c.Capacity)
                .OrderBy(c => c.StartDate)
                .ThenBy(c => c.Schedule)
                .ToList();

            var myEnrollments = _context.ExtraClassEnrollments
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .Select(e => e.ExtraClassId)
                .ToList();

            var attendanceRecords = _context.ExtraClassAttendanceRecords
                .Where(r => r.StudentId == student.Id)
                .ToList();

            ViewBag.MyEnrollments = myEnrollments;
            ViewBag.StudentId = student.Id;
            ViewBag.AttendanceRecords = attendanceRecords;

            return View(availableClasses);
        }

        // ============================================
        // MY ENROLLMENTS (Current Classes)
        // ============================================

        public ActionResult MyEnrollments()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var enrollments = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .Include(e => e.ExtraClass.Subject)
                .Include(e => e.ExtraClass.Grade)
                .Include(e => e.ExtraClass.Teacher)
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            return View(enrollments);
        }

        // ============================================
        // JOIN CLASS (Enroll)
        // ============================================

        [HttpPost]
        public ActionResult Join(int classId)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Student not found" });

            var extraClass = _context.ExtraClasses
                .Include(c => c.Enrollments)
                .FirstOrDefault(c => c.Id == classId);

            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            var existingEnrollment = _context.ExtraClassEnrollments
                .FirstOrDefault(e => e.ExtraClassId == classId && e.StudentId == student.Id && e.IsActive);

            if (existingEnrollment != null)
                return Json(new { success = false, message = "You have already joined this class" });

            var currentCount = extraClass.Enrollments?.Count(e => e.IsActive) ?? 0;
            if (currentCount >= extraClass.Capacity)
                return Json(new { success = false, message = "This class is full" });

            if (extraClass.StartDate < DateTime.Today)
                return Json(new { success = false, message = "This class has already started" });

            var enrollment = new ExtraClassEnrollment
            {
                ExtraClassId = classId,
                StudentId = student.Id,
                IsPaid = false,
                EnrollmentDate = DateTime.Now,
                IsActive = true
            };

            _context.ExtraClassEnrollments.Add(enrollment);
            extraClass.CurrentEnrollment = (extraClass.Enrollments?.Count(e => e.IsActive) ?? 0) + 1;

            _context.SaveChanges();

            try
            {
                _emailService.SendCustomEmail(student.User.Email, "Extra Class Enrollment - ElevateED",
                    $@"<h3>Enrollment Successful!</h3>
                       <p>Dear {student.FullName},</p>
                       <p>You have successfully enrolled in:</p>
                       <p><strong>{extraClass.Name}</strong></p>
                       <p>Schedule: {extraClass.Schedule}</p>
                       <p>Venue: {extraClass.Venue ?? "To be confirmed"}</p>
                       <p>Start Date: {extraClass.StartDate:dd MMM yyyy}</p>
                       <p>Please complete payment to confirm your spot.</p>");
            }
            catch { }

            return Json(new { success = true, message = "Successfully enrolled! Please complete payment to confirm your spot.", requiresPayment = extraClass.Price > 0 });
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
                .Include(c => c.Enrollments)
                .Include(c => c.AttendanceSessions)
                .Include(c => c.Feedbacks)
                .FirstOrDefault(c => c.Id == id);

            if (extraClass == null) return HttpNotFound();

            var enrollment = _context.ExtraClassEnrollments
                .FirstOrDefault(e => e.ExtraClassId == id && e.StudentId == student.Id && e.IsActive);

            var attendanceRecords = _context.ExtraClassAttendanceRecords
                .Where(r => r.StudentId == student.Id && r.AttendanceSession.ExtraClassId == id)
                .ToList();

            var myFeedback = _context.ExtraClassFeedbacks
                .FirstOrDefault(f => f.ExtraClassId == id && f.StudentId == student.Id);

            var totalSessions = extraClass.AttendanceSessions?.Count ?? 0;
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

        // ============================================
        // PAYMENT - PAYFAST INTEGRATION
        // ============================================

        public ActionResult PayNow(int enrollmentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var enrollment = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .Include(e => e.ExtraClass.Subject)
                .FirstOrDefault(e => e.Id == enrollmentId && e.StudentId == student.Id);

            if (enrollment == null) return HttpNotFound();

            if (enrollment.IsPaid)
            {
                TempData["ErrorMessage"] = "Payment has already been completed.";
                return RedirectToAction("MyEnrollments");
            }

            var extraClass = enrollment.ExtraClass;

            if (extraClass.Price == 0)
            {
                enrollment.IsPaid = true;
                enrollment.PaymentDate = DateTime.Now;
                enrollment.PaymentReference = "FREE_CLASS";
                _context.SaveChanges();
                TempData["SuccessMessage"] = "This is a free class! You are confirmed.";
                return RedirectToAction("MyEnrollments");
            }

            var paymentRef = "EC-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + enrollment.Id;

            var payment = new Payment
            {
                BookingId = enrollment.Id,
                PaymentReference = paymentRef,
                Amount = extraClass.Price,
                PaymentDate = DateTime.Now,
                PaymentMethod = "PayFast",
                Status = "Pending"
            };
            _context.Payments.Add(payment);
            _context.SaveChanges();

            var merchantId = System.Configuration.ConfigurationManager.AppSettings["PayFastMerchantId"] ?? "10000100";
            var merchantKey = System.Configuration.ConfigurationManager.AppSettings["PayFastMerchantKey"] ?? "46f0cd694581a";
            var site = System.Configuration.ConfigurationManager.AppSettings["PayFastSite"] ?? "https://sandbox.payfast.co.za/eng/process";
            var returnUrl = Url.Action("PaymentReturn", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");
            var cancelUrl = Url.Action("PaymentCancel", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");
            var notifyUrl = Url.Action("PaymentNotify", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");

            var payFastUrl = $"{site}?" +
                $"merchant_id={merchantId}" +
                $"&merchant_key={merchantKey}" +
                $"&return_url={System.Web.HttpUtility.UrlEncode(returnUrl)}" +
                $"&cancel_url={System.Web.HttpUtility.UrlEncode(cancelUrl)}" +
                $"&notify_url={System.Web.HttpUtility.UrlEncode(notifyUrl)}" +
                $"&m_payment_id={System.Web.HttpUtility.UrlEncode(paymentRef)}" +
                $"&amount={extraClass.Price.ToString("0.00")}" +
                $"&item_name={System.Web.HttpUtility.UrlEncode(extraClass.Name)}" +
                $"&item_description={System.Web.HttpUtility.UrlEncode("Extra Class Payment")}" +
                $"&name_first={System.Web.HttpUtility.UrlEncode(student.FirstName)}" +
                $"&name_last={System.Web.HttpUtility.UrlEncode(student.LastName)}" +
                $"&email_address={System.Web.HttpUtility.UrlEncode(student.User?.Email ?? "")}";

            Session["PaymentEnrollmentId"] = enrollment.Id;

            return Redirect(payFastUrl);
        }

        // ============================================
        // PAYMENT RETURN (SUCCESS)
        // ============================================

        public ActionResult PaymentReturn()
        {
            var enrollmentId = Session["PaymentEnrollmentId"] as int?;
            if (!enrollmentId.HasValue)
            {
                TempData["ErrorMessage"] = "Payment session expired. Please try again.";
                return RedirectToAction("MyEnrollments");
            }

            var enrollment = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .FirstOrDefault(e => e.Id == enrollmentId.Value);

            if (enrollment != null && !enrollment.IsPaid)
            {
                enrollment.IsPaid = true;
                enrollment.PaymentDate = DateTime.Now;

                var payment = _context.Payments.FirstOrDefault(p => p.BookingId == enrollmentId);
                if (payment != null)
                {
                    payment.Status = "Success";
                    payment.PaymentDate = DateTime.Now;
                }

                _context.SaveChanges();

                try
                {
                    var student = GetCurrentStudent();
                    _emailService.SendCustomEmail(student.User.Email, "Payment Confirmed - ElevateED",
                        $@"<h3>Payment Successful!</h3>
                           <p>Dear {student.FullName},</p>
                           <p>Your payment for <strong>{enrollment.ExtraClass.Name}</strong> has been confirmed.</p>
                           <p>You are now fully enrolled in the class.</p>
                           <p>Please check the class schedule and arrive on time.</p>");
                }
                catch { }
            }

            Session.Remove("PaymentEnrollmentId");
            TempData["SuccessMessage"] = "Payment successful! You are now confirmed for this class.";
            return RedirectToAction("MyEnrollments");
        }

        // ============================================
        // PAYMENT CANCEL
        // ============================================

        public ActionResult PaymentCancel()
        {
            Session.Remove("PaymentEnrollmentId");
            TempData["ErrorMessage"] = "Payment was cancelled. Your enrollment is still pending payment.";
            return RedirectToAction("MyEnrollments");
        }

        // ============================================
        // PAYMENT NOTIFY (PAYFAST IPN)
        // ============================================

        [HttpPost]
        public ActionResult PaymentNotify()
        {
            var paymentStatus = Request.Form["payment_status"];
            var paymentId = Request.Form["m_payment_id"];

            System.Diagnostics.Debug.WriteLine($"PayFast IPN: {paymentId} - {paymentStatus}");

            if (paymentStatus == "COMPLETE")
            {
                var payment = _context.Payments.FirstOrDefault(p => p.PaymentReference == paymentId);
                if (payment != null)
                {
                    payment.Status = "Success";
                    payment.PaymentDate = DateTime.Now;

                    var enrollment = _context.ExtraClassEnrollments.Find(payment.BookingId);
                    if (enrollment != null && !enrollment.IsPaid)
                    {
                        enrollment.IsPaid = true;
                        enrollment.PaymentDate = DateTime.Now;
                        enrollment.PaymentReference = paymentId;

                        var extraClass = _context.ExtraClasses.Find(enrollment.ExtraClassId);
                        if (extraClass != null)
                        {
                            extraClass.CurrentEnrollment = _context.ExtraClassEnrollments
                                .Count(e => e.ExtraClassId == extraClass.Id && e.IsPaid && e.IsActive);
                        }

                        _context.SaveChanges();
                    }
                }
            }

            return new EmptyResult();
        }

        // ============================================
        // ATTENDANCE - SCAN QR CODE
        // ============================================

        [HttpPost]
        public ActionResult ScanAttendance(string qrCode)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Student not found" });

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
                return Json(new { success = false, message = "You have already been marked present for this session" });

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

            return Json(new { success = true, message = $"Attendance marked for {session.ExtraClass.Name}" });
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
        public ActionResult SubmitFeedback(int classId, int rating, string comment)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Student not found" });

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
                return Json(new { success = false, message = "You have already submitted feedback for this class" });

            var feedback = new ExtraClassFeedback
            {
                ExtraClassId = classId,
                StudentId = student.Id,
                Rating = rating,
                Comment = comment,
                DateSubmitted = DateTime.Now
            };

            _context.ExtraClassFeedbacks.Add(feedback);
            _context.SaveChanges();

            return Json(new { success = true, message = "Thank you for your feedback!" });
        }

        // ============================================
        // DOWNLOAD RECEIPT
        // ============================================

        [HttpGet]
        public ActionResult DownloadReceipt(string paymentReference)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var payment = _context.Payments
                .Include(p => p.Booking)
                .Include(p => p.Booking.ExtraClass)
                .Include(p => p.Booking.ExtraClass.Subject)
                .FirstOrDefault(p => p.PaymentReference == paymentReference);

            if (payment == null) return HttpNotFound();

            ViewBag.Payment = payment;
            ViewBag.Student = student;

            return View();
        }

        // ============================================
        // HELPER METHODS
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
    }
}