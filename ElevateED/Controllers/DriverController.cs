using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using ElevateED.Models;

namespace ElevateED.Controllers
{
    [Authorize]
    public class DriverController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // DRIVER LOGIN
        // ============================================

        [AllowAnonymous]
        public ActionResult Login()
        {
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

            // Store driver info in session
            Session["DriverId"] = driver.Id;
            Session["DriverName"] = driver.FullName;
            Session["DriverPhone"] = driver.PhoneNumber;
            Session["DriverVehicle"] = driver.VehicleRegistration;
            Session["IsDriver"] = true;

            return RedirectToAction("Dashboard");
        }

        // ============================================
        // DRIVER DASHBOARD
        // ============================================

        public ActionResult Dashboard()
        {
            var driverId = GetDriverId();
            if (driverId == 0) return RedirectToAction("Login");

            // Get assigned routes
            var routes = _context.TransportRoutes
                .Include(r => r.ExtraClass)
                .Include(r => r.ExtraClass.Grade)
                .Include(r => r.ExtraClass.Subject)
                .Where(r => r.DriverId == driverId && r.Status != "Completed")
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            ViewBag.DriverName = Session["DriverName"];
            ViewBag.DriverPhone = Session["DriverPhone"];
            ViewBag.DriverVehicle = Session["DriverVehicle"];

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

            // Get paid students for this extra class
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
        // START ROUTE
        // ============================================

        [HttpPost]
        public ActionResult StartRoute(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null)
                return Json(new { success = false, message = "Route not found" });

            if (route.Status == "Active")
                return Json(new { success = false, message = "Route is already active" });

            route.Status = "Active";
            route.StartedAt = DateTime.Now;

            // Record initial location (use a default location)
            _context.RouteTrackings.Add(new RouteTracking
            {
                TransportRouteId = routeId,
                Latitude = -26.2041,  // Default: Johannesburg
                Longitude = 28.0473,
                TrackedAt = DateTime.Now
            });

            _context.SaveChanges();

            return Json(new { success = true, message = "Route started! You can now update your location." });
        }

        // ============================================
        // SIMULATE LOCATION UPDATE (Button Click)
        // ============================================

        [HttpPost]
        public ActionResult SimulateLocation(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null)
                return Json(new { success = false, message = "Route not found" });

            if (route.Status != "Active")
                return Json(new { success = false, message = "Start the route first before updating location" });

            // Generate slightly different coordinates to simulate movement
            var random = new Random();
            var baseLat = -26.2041;  // Johannesburg
            var baseLng = 28.0473;

            var lat = Math.Round(baseLat + (random.NextDouble() * 0.02 - 0.01), 6);
            var lng = Math.Round(baseLng + (random.NextDouble() * 0.02 - 0.01), 6);

            _context.RouteTrackings.Add(new RouteTracking
            {
                TransportRouteId = routeId,
                Latitude = lat,
                Longitude = lng,
                TrackedAt = DateTime.Now
            });

            _context.SaveChanges();

            return Json(new
            {
                success = true,
                latitude = lat,
                longitude = lng,
                time = DateTime.Now.ToString("HH:mm:ss"),
                googleMapsUrl = $"https://www.google.com/maps?q={lat},{lng}"
            });
        }

        // ============================================
        // UPDATE LOCATION (Automatic/GPS)
        // ============================================

        [HttpPost]
        public ActionResult UpdateLocation(int routeId, double latitude, double longitude)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false });

            var route = _context.TransportRoutes
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId && r.Status == "Active");

            if (route == null) return Json(new { success = false });

            _context.RouteTrackings.Add(new RouteTracking
            {
                TransportRouteId = routeId,
                Latitude = latitude,
                Longitude = longitude,
                TrackedAt = DateTime.Now
            });

            _context.SaveChanges();

            return Json(new { success = true });
        }

        // ============================================
        // END ROUTE
        // ============================================

        [HttpPost]
        public ActionResult EndRoute(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null)
                return Json(new { success = false, message = "Route not found" });

            route.Status = "Completed";
            route.EndedAt = DateTime.Now;
            _context.SaveChanges();

            return Json(new { success = true, message = "Route completed! You can now log out." });
        }

        // ============================================
        // EMERGENCY ALERT
        // ============================================

        [HttpPost]
        public ActionResult EmergencyAlert(int routeId, string message)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false, message = "Unauthorized" });

            var route = _context.TransportRoutes
                .Include(r => r.Driver)
                .FirstOrDefault(r => r.Id == routeId && r.DriverId == driverId);

            if (route == null)
                return Json(new { success = false, message = "Route not found" });

            // Get last known location
            var lastLocation = _context.RouteTrackings
                .Where(t => t.TransportRouteId == routeId)
                .OrderByDescending(t => t.TrackedAt)
                .FirstOrDefault();

            var lat = lastLocation?.Latitude ?? 0;
            var lng = lastLocation?.Longitude ?? 0;

            var alert = new EmergencyAlert
            {
                TransportRouteId = routeId,
                DriverId = driverId,
                Message = message ?? "EMERGENCY! Driver needs immediate assistance!",
                Latitude = lat,
                Longitude = lng,
                Status = AlertStatus.Active,
                CreatedAt = DateTime.Now
            };

            _context.EmergencyAlerts.Add(alert);

            // Update route status
            route.Status = "Emergency";
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Emergency alert sent to admin! Help is on the way.",
                alertId = alert.Id,
                location = $"Lat: {lat}, Lng: {lng}"
            });
        }

        // ============================================
        // GET MY LOCATION HISTORY
        // ============================================

        [HttpGet]
        public ActionResult GetMyLocationHistory(int routeId)
        {
            var driverId = GetDriverId();
            if (driverId == 0) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var locations = _context.RouteTrackings
                .Where(t => t.TransportRouteId == routeId)
                .OrderByDescending(t => t.TrackedAt)
                .Take(20)
                .Select(t => new
                {
                    latitude = t.Latitude,
                    longitude = t.Longitude,
                    time = t.TrackedAt.ToString("HH:mm:ss")
                })
                .ToList();

            return Json(new { success = true, locations = locations }, JsonRequestBehavior.AllowGet);
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
        // HELPER
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