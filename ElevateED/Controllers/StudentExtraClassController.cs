using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentExtraClassController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

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

            // Show classes for student's grade
            var classes = _context.ExtraClasses
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .Include(c => c.Bookings)
                .Where(c => c.IsActive && c.GradeId == studentGradeId.Value)
                .Where(c => c.ClassDate >= DateTime.Today)
                .OrderBy(c => c.ClassDate)
                .ThenBy(c => c.StartTime)
                .ToList();

            // Get student's existing bookings
            var myBookings = _context.ExtraClassBookings
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.ExtraClassId)
                .ToList();

            ViewBag.MyBookings = myBookings;
            ViewBag.StudentId = student.Id;

            return View(classes);
        }

        // ============================================
        // JOIN CLASS
        // ============================================

        [HttpPost]
        public ActionResult Join(int classId)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Student not found" });

            var extraClass = _context.ExtraClasses
                .Include(c => c.Bookings)
                .FirstOrDefault(c => c.Id == classId);

            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            // Check if already booked
            var existingBooking = _context.ExtraClassBookings
                .FirstOrDefault(b => b.ExtraClassId == classId && b.StudentId == student.Id);

            if (existingBooking != null)
                return Json(new { success = false, message = "You have already joined this class" });

            // Check capacity
            var currentCount = extraClass.Bookings.Count(b => b.Status != BookingStatus.Cancelled);
            if (currentCount >= extraClass.MaxStudents)
                return Json(new { success = false, message = "This class is full" });

            var booking = new ExtraClassBooking
            {
                ExtraClassId = classId,
                StudentId = student.Id,
                Status = BookingStatus.Pending,
                BookedAt = DateTime.Now
            };

            _context.ExtraClassBookings.Add(booking);
            _context.SaveChanges();

            return Json(new { success = true, message = "Successfully joined! Proceed to payment to confirm your spot." });
        }

        // ============================================
        // MY BOOKINGS
        // ============================================

        public ActionResult MyBookings()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var bookings = _context.ExtraClassBookings
                .Include(b => b.ExtraClass)
                .Include(b => b.ExtraClass.Subject)
                .Include(b => b.ExtraClass.Grade)
                .Where(b => b.StudentId == student.Id)
                .OrderByDescending(b => b.BookedAt)
                .ToList();

            return View(bookings);
        }
        // ============================================
        // PAYMENT - PAYFAST INTEGRATION
        // ============================================

        public ActionResult PayNow(int bookingId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var booking = _context.ExtraClassBookings
                .Include(b => b.ExtraClass)
                .Include(b => b.ExtraClass.Subject)
                .FirstOrDefault(b => b.Id == bookingId && b.StudentId == student.Id);

            if (booking == null) return HttpNotFound();

            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "This booking is already " + booking.Status.ToString().ToLower() + ".";
                return RedirectToAction("MyBookings");
            }

            var extraClass = booking.ExtraClass;

            // Generate unique payment reference
            var paymentRef = "MHS-" + DateTime.Now.ToString("yyyyMMdd") + "-" + bookingId;

            // Create payment record
            var payment = _context.Payments.FirstOrDefault(p => p.BookingId == bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = bookingId,
                    PaymentReference = paymentRef,
                    Amount = extraClass.Fee,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "PayFast",
                    Status = "Pending"
                };
                _context.Payments.Add(payment);
                _context.SaveChanges();
            }
            else
            {
                paymentRef = payment.PaymentReference;
            }

            // Build PayFast URL
            var merchantId = System.Configuration.ConfigurationManager.AppSettings["PayFastMerchantId"] ?? "10000100";
            var merchantKey = System.Configuration.ConfigurationManager.AppSettings["PayFastMerchantKey"] ?? "46f0cd694581a";
            var site = System.Configuration.ConfigurationManager.AppSettings["PayFastSite"] ?? "https://sandbox.payfast.co.za/eng/process";
            var returnUrl = Url.Action("PaymentReturn", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");
            var cancelUrl = Url.Action("PaymentCancel", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");
            var notifyUrl = Url.Action("PaymentNotify", "StudentExtraClass", null, Request.Url?.Scheme ?? "https");

            // Build query string
            var payFastUrl = site + "?" +
                "merchant_id=" + merchantId +
                "&merchant_key=" + merchantKey +
                "&return_url=" + System.Web.HttpUtility.UrlEncode(returnUrl) +
                "&cancel_url=" + System.Web.HttpUtility.UrlEncode(cancelUrl) +
                "&notify_url=" + System.Web.HttpUtility.UrlEncode(notifyUrl) +
                "&m_payment_id=" + System.Web.HttpUtility.UrlEncode(paymentRef) +
                "&amount=" + extraClass.Fee.ToString("0.00") +
                "&item_name=" + System.Web.HttpUtility.UrlEncode(extraClass.Title) +
                "&item_description=" + System.Web.HttpUtility.UrlEncode("Extra Class Payment") +
                "&name_first=" + System.Web.HttpUtility.UrlEncode(student.FirstName) +
                "&name_last=" + System.Web.HttpUtility.UrlEncode(student.LastName) +
                "&email_address=" + System.Web.HttpUtility.UrlEncode(student.User?.Email ?? "");

            // Store booking ID in session for return
            Session["PaymentBookingId"] = bookingId;

            return Redirect(payFastUrl);
        }

        // ============================================
        // PAYMENT RETURN (SUCCESS)
        // ============================================

        public ActionResult PaymentReturn()
        {
            var bookingId = Session["PaymentBookingId"] as int?;
            if (!bookingId.HasValue)
            {
                TempData["ErrorMessage"] = "Payment session expired. Please try again.";
                return RedirectToAction("MyBookings");
            }

            var booking = _context.ExtraClassBookings.Include(b => b.ExtraClass).FirstOrDefault(b => b.Id == bookingId.Value);
            if (booking != null && booking.Status == BookingStatus.Pending)
            {
                booking.Status = BookingStatus.Paid;
                booking.PaidAt = DateTime.Now;
                booking.AmountPaid = booking.ExtraClass?.Fee ?? 0;

                var payment = _context.Payments.FirstOrDefault(p => p.BookingId == bookingId);
                if (payment != null)
                {
                    payment.Status = "Success";
                    payment.PaymentDate = DateTime.Now;
                }

                _context.SaveChanges();
            }

            Session.Remove("PaymentBookingId");
            TempData["SuccessMessage"] = "Payment successful! You are now confirmed for this class.";
            return RedirectToAction("MyBookings");
        }

        // ============================================
        // PAYMENT CANCEL
        // ============================================

        public ActionResult PaymentCancel()
        {
            Session.Remove("PaymentBookingId");
            TempData["ErrorMessage"] = "Payment was cancelled. Your booking is still pending.";
            return RedirectToAction("MyBookings");
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

                    var booking = _context.ExtraClassBookings.Find(payment.BookingId);
                    if (booking != null && booking.Status == BookingStatus.Pending)
                    {
                        booking.Status = BookingStatus.Paid;
                        booking.PaidAt = DateTime.Now;
                        booking.AmountPaid = payment.Amount;
                        booking.PaymentReference = paymentId;
                    }

                    _context.SaveChanges();
                }
            }

            return new EmptyResult();
        }
        // Add these methods to your StudentExtraClassController

        // ============================================
        // PAY WITH CARD (Demo)
        // ============================================

        public ActionResult PayWithCard(int bookingId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var booking = _context.ExtraClassBookings
                .Include(b => b.ExtraClass)
                .Include(b => b.ExtraClass.Subject)
                .FirstOrDefault(b => b.Id == bookingId && b.StudentId == student.Id);

            if (booking == null) return HttpNotFound();

            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "This booking is already " + booking.Status.ToString().ToLower() + ".";
                return RedirectToAction("MyBookings");
            }

            ViewBag.Booking = booking;
            ViewBag.StudentName = student.FullName;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessCardPayment(int bookingId, string cardNumber, string cardHolder, string expiryMonth, string expiryYear, string cvv)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var booking = _context.ExtraClassBookings
                .Include(b => b.ExtraClass)
                .FirstOrDefault(b => b.Id == bookingId && b.StudentId == student.Id);

            if (booking == null)
                return Json(new { success = false, message = "Booking not found" });

            // Demo validation
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Replace(" ", "").Length < 16)
                return Json(new { success = false, message = "Invalid card number" });

            if (string.IsNullOrEmpty(cardHolder))
                return Json(new { success = false, message = "Card holder name is required" });

            // Generate payment reference
            var paymentRef = "MHS-" + DateTime.Now.ToString("yyyyMMdd") + "-" + bookingId + "-" + new Random().Next(1000, 9999);

            try
            {
                // Create payment record
                var payment = new Payment
                {
                    BookingId = bookingId,
                    PaymentReference = paymentRef,
                    Amount = booking.ExtraClass?.Fee ?? 0,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Card - ****" + cardNumber.Replace(" ", "").Substring(Math.Max(0, cardNumber.Replace(" ", "").Length - 4)),
                    Status = "Success"
                };

                _context.Payments.Add(payment);

                // Update booking status
                booking.Status = BookingStatus.Paid;
                booking.PaidAt = DateTime.Now;
                booking.AmountPaid = booking.ExtraClass?.Fee ?? 0;
                booking.PaymentReference = paymentRef;

                _context.SaveChanges();

                // Create receipt data
                var receiptData = new
                {
                    receiptNumber = paymentRef,
                    date = DateTime.Now,
                    studentName = student.FullName,
                    studentNumber = student.User?.StudentNumber ?? "N/A",
                    className = booking.ExtraClass?.Title ?? "N/A",
                    subject = booking.ExtraClass?.Subject?.Name ?? "N/A",
                    amount = booking.AmountPaid,
                    paymentMethod = payment.PaymentMethod,
                    cardHolder = cardHolder,
                    transactionId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()
                };

                return Json(new { success = true, message = "Payment successful!", receipt = receiptData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Payment failed: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult DownloadReceipt(string receiptNumber)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var payment = _context.Payments
                .Include(p => p.Booking)
                .Include(p => p.Booking.ExtraClass)
                .Include(p => p.Booking.ExtraClass.Subject)
                .FirstOrDefault(p => p.PaymentReference == receiptNumber);

            if (payment == null) return HttpNotFound();

            ViewBag.Payment = payment;
            ViewBag.Student = student;

            return View();
        }
        // ============================================
        // HELPER
        // ============================================

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

    }
}