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