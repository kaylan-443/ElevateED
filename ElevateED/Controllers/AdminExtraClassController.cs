using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminExtraClassController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // LIST EXTRA CLASSES
        // ============================================

        public ActionResult Index()
        {
            var classes = _context.ExtraClasses
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .Include(c => c.Bookings)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.ClassDate)
                .ThenBy(c => c.StartTime)
                .ToList();

            return View(classes);
        }

        // ============================================
        // CREATE EXTRA CLASS
        // ============================================

        public ActionResult Create()
        {
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ExtraClass model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedBy = GetCurrentUserId();
                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.ExtraClasses.Add(model);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Extra class created successfully!";
                return RedirectToAction("Index");
            }

            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
            return View(model);
        }

        // ============================================
        // VIEW BOOKINGS
        // ============================================

        public ActionResult Bookings(int classId)
        {
            var extraClass = _context.ExtraClasses
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .FirstOrDefault(c => c.Id == classId);

            if (extraClass == null) return HttpNotFound();

            var bookings = _context.ExtraClassBookings
                .Include(b => b.Student)
                .Include(b => b.Student.User)
                .Where(b => b.ExtraClassId == classId)
                .OrderByDescending(b => b.BookedAt)
                .ToList();

            ViewBag.ExtraClass = extraClass;
            return View(bookings);
        }

        // ============================================
        // TRANSPORT LIST
        // ============================================

        public ActionResult TransportList(int classId)
        {
            var extraClass = _context.ExtraClasses
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .FirstOrDefault(c => c.Id == classId);

            if (extraClass == null) return HttpNotFound();

            // Only show PAID students for transport
            var paidBookings = _context.ExtraClassBookings
                .Include(b => b.Student)
                .Include(b => b.Student.User)
                .Include(b => b.Student.Class)
                .Where(b => b.ExtraClassId == classId && b.Status == BookingStatus.Paid)
                .OrderBy(b => b.Student.LastName)
                .ToList();

            ViewBag.ExtraClass = extraClass;
            return View(paidBookings);
        }

        // ============================================
        // DELETE EXTRA CLASS
        // ============================================

        [HttpPost]
        public ActionResult Delete(int id)
        {
            var extraClass = _context.ExtraClasses.Find(id);
            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            extraClass.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true, message = "Extra class deleted" });
        }

        // ============================================
        // HELPER
        // ============================================

        private int GetCurrentUserId()
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
            return user?.Id ?? 1;
        }
    }
}