using System;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Models.ViewModels;
using ElevateED.Services;

namespace ElevateED.Controllers
{
    /// <summary>
    /// Controller for managing attendance sessions and marking.
    /// </summary>
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;

        public AttendanceController()
        {
            _attendanceService = new AttendanceService();
        }

        /// <summary>
        /// GET: Display form to start a new attendance session.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public ActionResult StartSession()
        {
            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;
                var teacher = db.Teachers.FirstOrDefault(t => t.User.Email == user);

                if (teacher == null)
                    return HttpNotFound();

                var viewModel = new StartSessionViewModel();

                // Get classes assigned to this teacher
                var classes = db.Classes
                    .Where(c => c.ClassTeacherId == teacher.Id)
                    .ToList();

                viewModel.AvailableClasses = classes
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.FullName })
                    .ToList();

                return View(viewModel);
            }
        }

        /// <summary>
        /// POST: Create a new attendance session with OTP.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public ActionResult StartSession(StartSessionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                using (var db = new ElevateEDContext())
                {
                    var user = User.Identity.Name;
                    var teacher = db.Teachers.FirstOrDefault(t => t.User.Email == user);
                    var classes = db.Classes.Where(c => c.ClassTeacherId == teacher.Id).ToList();

                    model.AvailableClasses = classes
                        .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.FullName })
                        .ToList();
                }
                return View(model);
            }

            Teacher teacher1;
            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;
                teacher1 = db.Teachers.FirstOrDefault(t => t.User.Email == user);

                if (teacher1 == null)
                    return HttpNotFound();

                // Verify teacher is assigned to this class
                var classExists = db.Classes.FirstOrDefault(c => c.Id == model.ClassId && c.ClassTeacherId == teacher1.Id);
                if (classExists == null)
                {
                    ModelState.AddModelError("", "You are not assigned to this class.");
                    var classes = db.Classes.Where(c => c.ClassTeacherId == teacher1.Id).ToList();
                    model.AvailableClasses = classes
                        .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.FullName })
                        .ToList();
                    return View(model);
                }
            }

            // Start the session via service
            var session = _attendanceService.StartSession(model.ClassId, teacher1.User.StudentNumber);

            if (session == null)
            {
                TempData["Error"] = "An active session already exists for this class today.";
                return RedirectToAction("StartSession");
            }

            TempData["OTPCode"] = session.OTPCode;
            TempData["SessionId"] = session.AttendanceSessionId;
            TempData["Success"] = "Attendance session started successfully!";

            return RedirectToAction("SessionConfirmation", new { sessionId = session.AttendanceSessionId });
        }

        /// <summary>
        /// Display OTP confirmation view with countdown timer.
        /// </summary>
        [Authorize(Roles = "Teacher")]
        public ActionResult SessionConfirmation(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null)
                    return HttpNotFound();

                var viewModel = new StartSessionViewModel
                {
                    OTPCode = session.OTPCode,
                    SessionId = sessionId
                };

                return View(viewModel);
            }
        }

        /// <summary>
        /// GET: Display OTP input form for students.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Student")]
        public ActionResult MarkAttendance()
        {
            return View(new MarkAttendanceViewModel());
        }

        /// <summary>
        /// POST: Mark student attendance based on OTP submission.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAttendance(MarkAttendanceViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;
                var student = db.Students.FirstOrDefault(s => s.Id.ToString() == user);

                if (student == null || student.ClassId == null)
                {
                    model.ErrorMessage = "Student profile not found or not enrolled in any class.";
                    return View(model);
                }

                var success = _attendanceService.SubmitOTP(model.OTPInput, student.Id);

                if (!success)
                {
                    // Check if already marked
                    var session = db.AttendanceSessions
                        .FirstOrDefault(s => s.OTPCode == model.OTPInput 
                            && s.IsActive 
                            && s.OTPExpiry > DateTime.Now 
                            && s.ClassId == student.ClassId);

                    if (session != null)
                    {
                        var record = db.AttendanceRecords
                            .FirstOrDefault(r => r.AttendanceSessionId == session.AttendanceSessionId 
                                && r.StudentId == student.Id);
                        
                        if (record != null && record.IsPresent)
                        {
                            model.ErrorMessage = "You have already been marked present for this session.";
                            return View(model);
                        }
                    }

                    model.ErrorMessage = "Invalid or expired OTP.";
                    return View(model);
                }
            }

            TempData["Success"] = "You have been marked present successfully!";
            return RedirectToAction("MarkAttendance");
        }

        /// <summary>
        /// GET: Display attendance records for manual editing.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public ActionResult EditAttendance(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;
                var teacher = db.Teachers.FirstOrDefault(t => t.User.Email == user);

                if (teacher == null)
                    return HttpNotFound();

                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null || session.Class.ClassTeacherId != teacher.Id)
                    return new HttpUnauthorizedResult();
            }

            var viewModel = _attendanceService.GetEditViewModel(sessionId);
            if (viewModel == null)
                return HttpNotFound();

            return View(viewModel);
        }

        /// <summary>
        /// POST: Save manual attendance overrides.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public ActionResult EditAttendance(EditAttendanceViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;
                var teacher = db.Teachers.FirstOrDefault(t => t.User.Email == user);

                if (teacher == null)
                    return HttpNotFound();

                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.AttendanceSessionId == model.SessionId);

                if (session == null || session.Class.ClassTeacherId != teacher.Id)
                    return new HttpUnauthorizedResult();
            }

            _attendanceService.SaveManualOverrides(model);

            TempData["Success"] = "Attendance records updated successfully!";
            return RedirectToAction("EditAttendance", new { sessionId = model.SessionId });
        }
    }
}
