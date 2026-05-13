using ElevateED.Models;
using ElevateED.ViewModels;
using ElevateED.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;

        public AttendanceController()
        {
            _attendanceService = new AttendanceService();
        }

        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public ActionResult StartSession()
        {
            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return HttpNotFound();

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return HttpNotFound();

                var viewModel = new StartSessionViewModel();

                // Get ALL classes the teacher is assigned to
                var assignedClasses = new List<Class>();

                // 1. Classes where teacher is the Class Teacher
                var classTeacherClasses = db.Classes
                    .Include("Grade")
                    .Where(c => c.ClassTeacherId == teacher.Id)
                    .ToList();
                assignedClasses.AddRange(classTeacherClasses);

                // 2. Classes where teacher has subject assignments
                var subjectClasses = db.TeacherSubjectAssignments
                    .Include("Class")
                    .Include("Class.Grade")
                    .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                    .Select(a => a.Class)
                    .ToList();

                foreach (var cls in subjectClasses)
                {
                    if (cls != null && !assignedClasses.Any(c => c.Id == cls.Id))
                    {
                        assignedClasses.Add(cls);
                    }
                }

                if (!assignedClasses.Any())
                {
                    TempData["Error"] = "You are not assigned to any classes. Please contact the administrator.";
                    return RedirectToAction("Dashboard", "Teacher");
                }

                viewModel.AvailableClasses = assignedClasses
                    .OrderBy(c => c.Grade?.Level)
                    .ThenBy(c => c.Name)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FullName + " (" + c.Grade?.Name + ")"
                    })
                    .ToList();

                return View(viewModel);
            }
        }
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public ActionResult StartSession(StartSessionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ReloadClassesForView(model);
                return View(model);
            }

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return HttpNotFound();

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return HttpNotFound();

                // Verify teacher is assigned to this class
                var isClassTeacher = db.Classes.Any(c => c.Id == model.ClassId && c.ClassTeacherId == teacher.Id);
                var isSubjectTeacher = db.TeacherSubjectAssignments
                    .Any(a => a.TeacherId == teacher.Id && a.ClassId == model.ClassId && a.IsActive);

                if (!isClassTeacher && !isSubjectTeacher)
                {
                    ModelState.AddModelError("", "You are not assigned to this class.");
                    ReloadClassesForView(model);
                    return View(model);
                }

                // Start the session via service
                var session = _attendanceService.StartSession(model.ClassId, teacher.Id);

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
        }

        // Helper method to reload classes for the dropdown
        private void ReloadClassesForView(StartSessionViewModel model)
        {
            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return;

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return;

                var assignedClasses = new List<Class>();

                var classTeacherClasses = db.Classes
                    .Include("Grade")
                    .Where(c => c.ClassTeacherId == teacher.Id)
                    .ToList();
                assignedClasses.AddRange(classTeacherClasses);

                var subjectClasses = db.TeacherSubjectAssignments
                    .Include("Class")
                    .Include("Class.Grade")
                    .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                    .Select(a => a.Class)
                    .ToList();

                foreach (var cls in subjectClasses)
                {
                    if (cls != null && !assignedClasses.Any(c => c.Id == cls.Id))
                    {
                        assignedClasses.Add(cls);
                    }
                }

                model.AvailableClasses = assignedClasses
                    .OrderBy(c => c.Grade?.Level)
                    .ThenBy(c => c.Name)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FullName + " (" + c.Grade?.Name + ")"
                    })
                    .ToList();
            }
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult SessionConfirmation(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var session = db.AttendanceSessions
                    .Include("Class")
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null)
                    return HttpNotFound();

                ViewBag.QRCode = session.QRCode;
                ViewBag.Expiry = session.QRCodeExpiry ?? DateTime.Now.AddHours(2); // SET THIS
                ViewBag.ClassName = session.Class?.FullName;
                ViewBag.SessionId = sessionId;

                var viewModel = new StartSessionViewModel
                {
                    OTPCode = session.OTPCode,
                    SessionId = sessionId
                };

                return View(viewModel);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Student")]
        public ActionResult MarkAttendance()
        {
            return View(new MarkAttendanceViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAttendance(MarkAttendanceViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return HttpNotFound();

                var student = db.Students.FirstOrDefault(s => s.UserId == user.Id);
                if (student == null || student.ClassId == null)
                {
                    model.ErrorMessage = "Student profile not found or not enrolled in any class.";
                    return View(model);
                }

                var success = _attendanceService.SubmitOTP(model.OTPInput, student.Id);

                if (!success)
                {
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

        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public ActionResult EditAttendance(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return RedirectToAction("Login", "Account");

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return RedirectToAction("Login", "Account");

                var session = db.AttendanceSessions
                    .Include("Class")
                    .Include("Class.Grade")
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null)
                    return HttpNotFound();

                // Check if teacher has access to this class
                bool isClassTeacher = db.Classes.Any(c => c.Id == session.ClassId && c.ClassTeacherId == teacher.Id);
                bool isSubjectTeacher = db.TeacherSubjectAssignments
                    .Any(a => a.TeacherId == teacher.Id && a.ClassId == session.ClassId && a.IsActive);

                if (!isClassTeacher && !isSubjectTeacher)
                {
                    TempData["Error"] = "You are not authorized to edit this attendance session.";
                    return RedirectToAction("ActiveSessions");
                }
            }

            var viewModel = _attendanceService.GetEditViewModel(sessionId);
            if (viewModel == null)
                return HttpNotFound();

            return View(viewModel);
        }

        // ADD THESE ACTIONS TO AttendanceController

        // Teacher views the QR code
        [Authorize(Roles = "Teacher")]
        public ActionResult ShowQRCode(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var session = db.AttendanceSessions
                    .Include("Class")
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null)
                    return HttpNotFound();

                // Generate QR code URL for scanning
                var qrCodeUrl = Url.Action("ScanQR", "Attendance",
                    new { qrCode = session.QRCode }, Request.Url.Scheme);

                ViewBag.QRCodeUrl = qrCodeUrl;
                ViewBag.QRCode = session.QRCode;
                ViewBag.ClassName = session.Class?.FullName;
                ViewBag.Expiry = session.QRCodeExpiry;
                ViewBag.SessionId = sessionId;

                return View();
            }
        }

        // Student scans QR code (GET - redirects to POST)
        [Authorize(Roles = "Student")]
        public ActionResult ScanQR(string qrCode)
        {
            if (string.IsNullOrEmpty(qrCode))
                return RedirectToAction("MarkAttendance");

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = db.Students.FirstOrDefault(s => s.UserId == user.Id);
                if (student == null) return RedirectToAction("Login", "Account");

                var result = _attendanceService.ScanQRCode(qrCode, student.Id);

                if (result.StartsWith("SUCCESS"))
                {
                    TempData["Success"] = result.Replace("SUCCESS|", "");
                }
                else
                {
                    TempData["Error"] = result;
                }

                return RedirectToAction("MarkAttendance");
            }
        }

        // Student submits QR code manually (POST)
        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitQRCode(string qrCode)
        {
            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return Json(new { success = false, message = "Student not found" });

                var student = db.Students.FirstOrDefault(s => s.UserId == user.Id);
                if (student == null) return Json(new { success = false, message = "Student not found" });

                var result = _attendanceService.ScanQRCode(qrCode, student.Id);

                if (result.StartsWith("SUCCESS"))
                {
                    return Json(new { success = true, message = result.Replace("SUCCESS|", "") });
                }
                else
                {
                    return Json(new { success = false, message = result });
                }
            }
        }
        [Authorize(Roles = "Teacher")]
        public ActionResult ActiveSessions()
        {
            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return RedirectToAction("Login", "Account");

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return RedirectToAction("Login", "Account");

                // Get classes where teacher is class teacher OR subject teacher
                var teacherClassIds = new List<int>();

                // Class teacher
                var classTeacherClasses = db.Classes
                    .Where(c => c.ClassTeacherId == teacher.Id)
                    .Select(c => c.Id);
                teacherClassIds.AddRange(classTeacherClasses);

                // Subject teacher
                var subjectClasses = db.TeacherSubjectAssignments
                    .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                    .Select(a => a.ClassId);
                teacherClassIds.AddRange(subjectClasses.Distinct());

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var sessions = db.AttendanceSessions
                    .Include("Class")
                    .Include("AttendanceRecords")
                    .Where(s => teacherClassIds.Contains(s.ClassId)
                        && s.IsActive
                        && s.SessionDate >= today
                        && s.SessionDate < tomorrow)
                    .OrderByDescending(s => s.SessionDate)
                    .ToList();

                var viewModel = sessions.Select(s => new ActiveSessionViewModel
                {
                    SessionId = s.AttendanceSessionId,
                    ClassName = s.Class?.FullName ?? "Unknown",
                    SessionDate = s.SessionDate,
                    OTPCode = s.OTPCode,
                    QRCode = s.QRCode,
                    QRCodeExpiry = s.QRCodeExpiry,
                    TotalStudents = s.AttendanceRecords?.Count ?? 0,
                    PresentCount = s.AttendanceRecords?.Count(r => r.IsPresent) ?? 0,
                    AbsentCount = s.AttendanceRecords?.Count(r => !r.IsPresent) ?? 0,
                    IsExpired = s.QRCodeExpiry.HasValue && s.QRCodeExpiry < DateTime.Now,
                    TimeRemaining = s.QRCodeExpiry.HasValue && s.QRCodeExpiry > DateTime.Now
                        ? $"{(s.QRCodeExpiry.Value - DateTime.Now).Minutes}m {(s.QRCodeExpiry.Value - DateTime.Now).Seconds}s"
                        : "Expired"
                }).ToList();

                return View(viewModel);
            }
        }
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public ActionResult EditAttendance(EditAttendanceViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (user == null) return RedirectToAction("Login", "Account");

                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
                if (teacher == null) return RedirectToAction("Login", "Account");

                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.AttendanceSessionId == model.SessionId);

                if (session == null)
                    return HttpNotFound();

                // Check if teacher has access to this class
                bool isClassTeacher = db.Classes.Any(c => c.Id == session.ClassId && c.ClassTeacherId == teacher.Id);
                bool isSubjectTeacher = db.TeacherSubjectAssignments
                    .Any(a => a.TeacherId == teacher.Id && a.ClassId == session.ClassId && a.IsActive);

                if (!isClassTeacher && !isSubjectTeacher)
                {
                    TempData["Error"] = "You are not authorized to edit this attendance session.";
                    return RedirectToAction("ActiveSessions");
                }
            }

            _attendanceService.SaveManualOverrides(model);

            TempData["Success"] = "Attendance records updated successfully!";
            return RedirectToAction("EditAttendance", new { sessionId = model.SessionId });
        }
    }
}