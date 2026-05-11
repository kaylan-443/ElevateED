using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using ElevateED.Models;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    public class DriverController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // DRIVER LOGIN
        // ============================================

        [AllowAnonymous]
        public ActionResult Login()
        {
            if (Session["DriverId"] != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            var passwordHash = HashPassword(password);
            var driver = _context.Drivers
                .FirstOrDefault(d => d.Username == username
                    && d.PasswordHash == passwordHash
                    && d.IsActive
                    && d.ExpiresAt > DateTime.Now);

            if (driver == null)
            {
                ModelState.AddModelError("", "Invalid credentials or account expired.");
                return View();
            }

            Session["DriverId"] = driver.Id;
            Session["DriverName"] = driver.FullName;
            Session["DriverPhone"] = driver.PhoneNumber;
            Session["DriverVehicle"] = driver.VehicleRegistration;
            Session["IsDriver"] = true;

            return RedirectToAction("Dashboard");
        }

        // ============================================
        // CUSTOM AUTHORIZATION
        // ============================================

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.ActionDescriptor.ActionName == "Login")
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            if (Session["DriverId"] == null)
            {
                filterContext.Result = RedirectToAction("Login");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // ============================================
        // DRIVER DASHBOARD
        // ============================================

        public ActionResult Dashboard()
        {
            var driverId = GetDriverId();
            if (driverId == 0)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            var routes = _context.TransportRoutes
                .Include(r => r.ExtraClass)
                .Include(r => r.ExtraClass.Grade)
                .Include(r => r.ExtraClass.Subject)
                .Where(r => r.DriverId == driverId && r.Status != "Completed")
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            ViewBag.DriverName = Session["DriverName"] ?? "Driver";
            ViewBag.DriverPhone = Session["DriverPhone"] ?? "N/A";
            ViewBag.DriverVehicle = Session["DriverVehicle"] ?? "N/A";

            return View(routes);
        }

        // ============================================
        // VIEW TRANSPORT LIST
        // ============================================

        public ActionResult TransportList(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return RedirectToAction("Login");

            var route = _context.TransportRoutes
                .Include(r => r.ExtraClass)
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null) return HttpNotFound();

            var students = _context.ExtraClassBookings
                .Include(b => b.Student)
                .Include(b => b.Student.User)
                .Include(b => b.Student.Class)
                .Where(b => b.ExtraClassId == route.ExtraClassId
                    && b.Status == BookingStatus.Paid)
                .OrderBy(b => b.Student.LastName)
                .ToList();

            ViewBag.Route = route;
            return View(students);
        }

        // ============================================
        // GET STUDENTS FOR ROLL CALL
        // ============================================

        [HttpGet]
        public ActionResult GetStudentsForRollCall(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            var route = _context.TransportRoutes
                .Include(r => r.ExtraClass)
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null)
                return Json(new { success = false, message = "Route not found" }, JsonRequestBehavior.AllowGet);

            var students = _context.ExtraClassBookings
                .Include(b => b.Student)
                .Include(b => b.Student.Class)
                .Where(b => b.ExtraClassId == route.ExtraClassId && b.Status == BookingStatus.Paid)
                .OrderBy(b => b.Student.LastName)
                .Select(b => new
                {
                    bookingId = b.Id,
                    fullName = b.Student.FirstName + " " + b.Student.LastName,
                    className = b.Student.Class != null ? b.Student.Class.FullName : "N/A",
                    parentPhone = b.Student.ParentCellPhone ?? "N/A"
                })
                .ToList();

            return Json(new { success = true, students = students }, JsonRequestBehavior.AllowGet);
        }

        // ============================================
        // SAVE ROLL CALL
        // ============================================

        [HttpPost]
        public ActionResult SaveRollCall(int routeId, string tripType, List<int> presentStudentIds)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var trip = new Trip
                {
                    TransportRouteId = routeId,
                    DriverId = driverId,
                    TripType = tripType ?? "Outbound",
                    Status = "RollCall",
                    RollCallData = JsonConvert.SerializeObject(presentStudentIds),
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Trips.Add(trip);
                _context.SaveChanges();

                return Json(new { success = true, tripId = trip.Id, message = "Roll call saved!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // START TRIP (Simple - no maps/destinations)
        // ============================================

        [HttpPost]
        public ActionResult StartTrip(int routeId, string tripType)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var route = _context.TransportRoutes.Find(routeId);
                if (route == null) return Json(new { success = false, message = "Route not found" });

                // Get the roll call trip and mark it as active
                var rollCallTrip = _context.Trips
                    .FirstOrDefault(t => t.TransportRouteId == routeId && t.DriverId == driverId && t.Status == "RollCall");

                if (rollCallTrip != null)
                {
                    rollCallTrip.Status = "Active";
                    rollCallTrip.StartedAt = DateTime.Now;
                }

                // Update route status based on trip type
                route.Status = tripType == "Return" ? "Return_Active" : "Active";
                route.StartedAt = DateTime.Now;

                _context.SaveChanges();

                return Json(new { success = true, message = tripType + " trip started! Drive safely." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // END OUTBOUND TRIP (Arrived at venue)
        // ============================================

        [HttpPost]
        public ActionResult EndOutboundTrip(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes.Find(routeId);
            if (route == null) return Json(new { success = false, message = "Route not found" });

            route.Status = "Outbound_Complete";

            var activeTrips = _context.Trips
                .Where(t => t.TransportRouteId == routeId && t.DriverId == driverId && t.IsActive && t.TripType == "Outbound")
                .ToList();

            foreach (var trip in activeTrips)
            {
                trip.Status = "Completed";
                trip.EndedAt = DateTime.Now;
                trip.IsActive = false;
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Outbound trip completed! Ready for return trip." });
        }

        // ============================================
        // END ROUTE (Fully complete)
        // ============================================

        [HttpPost]
        public ActionResult EndRoute(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes.Find(routeId);
            if (route == null) return Json(new { success = false, message = "Route not found" });

            route.Status = "Completed";
            route.EndedAt = DateTime.Now;

            var activeTrips = _context.Trips
                .Where(t => t.TransportRouteId == routeId && t.DriverId == driverId && t.IsActive)
                .ToList();

            foreach (var trip in activeTrips)
            {
                trip.Status = "Completed";
                trip.EndedAt = DateTime.Now;
                trip.IsActive = false;
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "All trips completed! Welcome back to school." });
        }

        // ============================================
        // DRIVER LOGOUT
        // ============================================

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private int GetDriverId()
        {
            return Session["DriverId"] != null ? (int)Session["DriverId"] : 0;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}