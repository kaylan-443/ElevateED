using ElevateED.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

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

            var classIds = classes.Select(c => c.Id).ToList();
            var transportRoutes = _context.TransportRoutes
                .Include(r => r.Driver)
                .Where(r => classIds.Contains(r.ExtraClassId))
                .ToList();

            ViewBag.TransportRoutes = transportRoutes;

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

            var routes = _context.TransportRoutes.Where(r => r.ExtraClassId == id).ToList();
            foreach (var route in routes)
            {
                var trips = _context.Trips.Where(t => t.TransportRouteId == route.Id);
                _context.Trips.RemoveRange(trips);
            }
            _context.TransportRoutes.RemoveRange(routes);

            var bookings = _context.ExtraClassBookings.Where(b => b.ExtraClassId == id);
            _context.ExtraClassBookings.RemoveRange(bookings);

            var bookingIds = bookings.Select(b => b.Id).ToList();
            var payments = _context.Payments.Where(p => bookingIds.Contains(p.BookingId));
            _context.Payments.RemoveRange(payments);

            extraClass.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true, message = "Extra class and all related records deleted" });
        }

        // ============================================
        // DRIVER MANAGEMENT
        // ============================================

        public ActionResult Drivers()
        {
            var drivers = _context.Drivers
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            return View(drivers);
        }

        [HttpPost]
        public ActionResult CreateDriver(string fullName, string username, string password,
            string phoneNumber, string vehicleRegistration, int expiryHours = 8)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Json(new { success = false, message = "Username and password are required" });

            if (_context.Drivers.Any(d => d.Username == username))
                return Json(new { success = false, message = "Username already exists" });

            var driver = new Driver
            {
                FullName = fullName,
                Username = username,
                PasswordHash = HashPassword(password),
                PhoneNumber = phoneNumber,
                VehicleRegistration = vehicleRegistration,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(expiryHours),
                CreatedBy = GetCurrentUserId(),
                IsActive = true
            };

            _context.Drivers.Add(driver);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Driver created successfully!",
                credentials = new
                {
                    username = username,
                    password = password,
                    fullName = fullName,
                    expiresAt = driver.ExpiresAt.ToString("dd MMM yyyy HH:mm"),
                    loginUrl = Url.Action("Login", "Driver", null, Request.Url?.Scheme ?? "https")
                }
            });
        }

        [HttpGet]
        public ActionResult GetActiveDrivers()
        {
            if (!IsAdmin()) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var drivers = _context.Drivers
                .Where(d => d.IsActive && d.ExpiresAt > DateTime.Now)
                .OrderBy(d => d.FullName)
                .Select(d => new
                {
                    id = d.Id,
                    fullName = d.FullName,
                    username = d.Username
                })
                .ToList();

            return Json(new { success = true, drivers = drivers }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult TerminateDriver(int driverId)
        {
            if (!IsAdmin()) return Json(new { success = false });

            var driver = _context.Drivers.Find(driverId);
            if (driver == null) return Json(new { success = false, message = "Driver not found" });

            driver.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true, message = "Driver account terminated" });
        }

        // ============================================
        // ASSIGN DRIVER TO ROUTE
        // ============================================

        [HttpPost]
        public ActionResult AssignDriver(int classId, int driverId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var existingRoute = _context.TransportRoutes
                .FirstOrDefault(r => r.ExtraClassId == classId && r.Status != "Completed");

            if (existingRoute != null)
                return Json(new { success = false, message = "This class already has an active route" });

            var route = new TransportRoute
            {
                ExtraClassId = classId,
                DriverId = driverId,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.TransportRoutes.Add(route);
            _context.SaveChanges();

            return Json(new { success = true, message = "Driver assigned to route" });
        }

        // ============================================
        // TRIP HISTORY
        // ============================================

        [HttpGet]
        public ActionResult GetTripHistory(int classId)
        {
            if (!IsAdmin()) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var route = _context.TransportRoutes.FirstOrDefault(r => r.ExtraClassId == classId);
            if (route == null) return Json(new { success = false, message = "No route found" }, JsonRequestBehavior.AllowGet);

            var trips = _context.Trips
                .Where(t => t.TransportRouteId == route.Id)
                .OrderBy(t => t.CreatedAt)
                .ToList()
                .Select(t => new
                {
                    tripType = t.TripType,
                    destinationName = t.DestinationName ?? "N/A",
                    destinationAddress = t.DestinationAddress ?? "N/A",
                    passengerCount = !string.IsNullOrEmpty(t.RollCallData) ?
                        JsonConvert.DeserializeObject<List<int>>(t.RollCallData).Count : 0,
                    startedAt = t.StartedAt?.ToString("dd MMM yyyy, HH:mm") ?? "N/A",
                    endedAt = t.EndedAt?.ToString("dd MMM yyyy, HH:mm") ?? "N/A",
                    status = t.Status
                })
                .ToList();

            return Json(new { success = true, trips = trips }, JsonRequestBehavior.AllowGet);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private bool IsAdmin()
        {
            if (!User.Identity.IsAuthenticated) return false;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
            return user != null && user.Role == UserRole.Admin;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private int GetCurrentUserId()
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
            return user?.Id ?? 1;
        }
    }
}