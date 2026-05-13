using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherExtraClassController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private EmailService _emailService = new EmailService();

        // ============================================
        // DASHBOARD - MY EXTRA CLASSES
        // ============================================

        public ActionResult Index()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var myClasses = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Enrollments)
                .Include(c => c.AttendanceSessions)
                .Include(c => c.Feedbacks)
                .Where(c => c.TeacherId == teacher.Id && c.IsActive)
                .OrderBy(c => c.StartDate)
                .ToList();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var activeSessions = _context.ExtraClassAttendanceSessions
                .Include(s => s.ExtraClass)
                .Where(s => s.TeacherId == teacher.Id
                    && s.SessionDate >= today
                    && s.SessionDate < tomorrow
                    && s.QRCodeExpiry > DateTime.Now)
                .ToList();

            var viewModel = new TeacherExtraClassDashboardViewModel
            {
                MyClasses = myClasses,
                ActiveSessions = activeSessions,
                TotalStudents = myClasses.Sum(c => c.Enrollments?.Count(e => e.IsActive) ?? 0),
                TotalSessions = myClasses.Sum(c => c.AttendanceSessions?.Count ?? 0),
                AverageAttendance = CalculateAverageAttendance(myClasses)
            };

            return View(viewModel);
        }

        private decimal CalculateAverageAttendance(List<ExtraClass> myClasses)
        {
            int totalRecords = 0;
            int presentRecords = 0;

            foreach (var cls in myClasses)
            {
                if (cls.AttendanceSessions != null)
                {
                    foreach (var session in cls.AttendanceSessions)
                    {
                        if (session.AttendanceRecords != null)
                        {
                            totalRecords += session.AttendanceRecords.Count;
                            presentRecords += session.AttendanceRecords.Count(r => r.IsPresent);
                        }
                    }
                }
            }

            if (totalRecords == 0) return 0;
            return Math.Round((decimal)presentRecords / totalRecords * 100, 1);
        }

        // ============================================
        // CLASS DETAILS WITH AI RECOMMENDATIONS
        // ============================================

        public ActionResult ClassDetails(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Enrollments.Select(e => e.Student))
                .Include(c => c.AttendanceSessions)
                .Include(c => c.Feedbacks)
                .Include(c => c.AIRecommendations)
                .FirstOrDefault(c => c.Id == id && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            var aiRecommendation = extraClass.AIRecommendations?
                .OrderByDescending(r => r.GeneratedDate)
                .FirstOrDefault();

            var viewModel = new TeacherClassDetailsViewModel
            {
                ExtraClass = extraClass,
                EnrolledStudents = extraClass.Enrollments?.Where(e => e.IsActive).Select(e => e.Student).ToList() ?? new List<Student>(),
                AttendanceSessions = extraClass.AttendanceSessions?.OrderByDescending(s => s.SessionDate).ToList() ?? new List<ExtraClassAttendanceSession>(),
                Feedbacks = extraClass.Feedbacks?.OrderByDescending(f => f.DateSubmitted).ToList() ?? new List<ExtraClassFeedback>(),
                AIRecommendation = aiRecommendation,
                TotalEnrolled = extraClass.Enrollments?.Count(e => e.IsActive) ?? 0,
                TotalPaid = extraClass.Enrollments?.Count(e => e.IsPaid) ?? 0,
                AverageRating = extraClass.Feedbacks != null && extraClass.Feedbacks.Any()
                    ? Math.Round(extraClass.Feedbacks.Average(f => (decimal)f.Rating), 1)
                    : 0
            };

            // Parse AI recommendation JSON data
            if (aiRecommendation != null)
            {
                try
                {
                    var easyTopics = JsonConvert.DeserializeObject<List<string>>(aiRecommendation.EasyWinTopics ?? "[]");
                    var difficultTopics = JsonConvert.DeserializeObject<List<string>>(aiRecommendation.DifficultTopics ?? "[]");
                    var suggestedOrder = JsonConvert.DeserializeObject<List<string>>(aiRecommendation.SuggestedTeachingOrder ?? "[]");
                    var commonMistakes = JsonConvert.DeserializeObject<List<string>>(aiRecommendation.CommonMistakes ?? "[]");

                    ViewBag.EasyTopics = easyTopics;
                    ViewBag.DifficultTopics = difficultTopics;
                    ViewBag.SuggestedOrder = suggestedOrder;
                    ViewBag.CommonMistakes = commonMistakes;
                }
                catch { }
            }

            return View(viewModel);
        }

        // ============================================
        // START ATTENDANCE SESSION (QR CODE)
        // ============================================

        public ActionResult StartSession(int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Check if there's already an active session for today
            var existingSession = _context.ExtraClassAttendanceSessions
                .FirstOrDefault(s => s.ExtraClassId == classId
                    && s.SessionDate >= today
                    && s.SessionDate < tomorrow
                    && s.QRCodeExpiry > DateTime.Now);

            if (existingSession != null)
            {
                TempData["ErrorMessage"] = "There is already an active session for this class today.";
                return RedirectToAction("ClassDetails", new { id = classId });
            }

            var sessionNumber = (extraClass.AttendanceSessions?.Count ?? 0) + 1;

            var viewModel = new StartAttendanceSessionViewModel
            {
                ExtraClassId = classId,
                SessionNumber = sessionNumber,
                SessionDate = DateTime.Today,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(11, 0, 0)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartSession(StartAttendanceSessionViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Enrollments)
                .FirstOrDefault(c => c.Id == model.ExtraClassId && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            // Create attendance session
            var session = new ExtraClassAttendanceSession
            {
                ExtraClassId = model.ExtraClassId,
                TeacherId = teacher.Id,
                SessionNumber = model.SessionNumber,
                SessionDate = model.SessionDate,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                TopicsCovered = model.TopicsToCover,
                Status = Models.AttendanceStatus.Present,
                CreatedAt = DateTime.Now,
                QRCodeExpiry = DateTime.Now.AddHours(2)
            };

            _context.ExtraClassAttendanceSessions.Add(session);
            _context.SaveChanges();

            // Pre-create attendance records for all enrolled students
            if (extraClass.Enrollments != null)
            {
                foreach (var enrollment in extraClass.Enrollments.Where(e => e.IsActive))
                {
                    var record = new ExtraClassAttendanceRecord
                    {
                        AttendanceSessionId = session.Id,
                        StudentId = enrollment.StudentId,
                        IsPresent = false,
                        Status = Models.AttendanceStatus.Absent
                    };
                    _context.ExtraClassAttendanceRecords.Add(record);
                }
                _context.SaveChanges();
            }

            TempData["SuccessMessage"] = $"Attendance session started! QR Code: {session.QRCode}";
            return RedirectToAction("ActiveSession", new { sessionId = session.Id });
        }

        // ============================================
        // ACTIVE SESSION - SHOW QR CODE
        // ============================================

        public ActionResult ActiveSession(int sessionId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var session = _context.ExtraClassAttendanceSessions
                .Include(s => s.ExtraClass)
                .Include(s => s.AttendanceRecords)
                .FirstOrDefault(s => s.Id == sessionId && s.TeacherId == teacher.Id);

            if (session == null) return HttpNotFound();

            var studentIds = session.AttendanceRecords?.Select(r => r.StudentId).ToList() ?? new List<int>();
            var students = _context.Students
                .Include(s => s.User)
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionary(s => s.Id, s => s);

            var totalEnrolled = session.AttendanceRecords?.Count ?? 0;
            var presentCount = session.AttendanceRecords?.Count(r => r.IsPresent) ?? 0;
            var absentCount = totalEnrolled - presentCount;

            var viewModel = new ActiveAttendanceSessionViewModel
            {
                SessionId = session.Id,
                ClassName = session.ExtraClass?.Name ?? "Unknown",
                QRCode = session.QRCode,
                QRCodeExpiry = session.QRCodeExpiry,
                SessionDate = session.SessionDate,
                TotalStudents = totalEnrolled,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                IsExpired = session.QRCodeExpiry < DateTime.Now
            };

            return View(viewModel);
        }

        // ============================================
        // MANUAL ATTENDANCE MARKING
        // ============================================

        public ActionResult MarkAttendance(int sessionId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var session = _context.ExtraClassAttendanceSessions
                .Include(s => s.ExtraClass)
                .Include(s => s.AttendanceRecords)
                .FirstOrDefault(s => s.Id == sessionId && s.TeacherId == teacher.Id);

            if (session == null) return HttpNotFound();

            var studentIds = session.AttendanceRecords?.Select(r => r.StudentId).ToList() ?? new List<int>();
            var students = _context.Students
                .Include(s => s.User)
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionary(s => s.Id, s => s);

            var viewModel = new ExtraClassAttendanceRecordViewModel
            {
                SessionId = session.Id,
                ClassName = session.ExtraClass?.Name ?? "Unknown",
                SessionDate = session.SessionDate,
                Records = session.AttendanceRecords?.Select(r => new AttendanceRecordItem
                {
                    StudentId = r.StudentId,
                    StudentName = students.ContainsKey(r.StudentId) ? students[r.StudentId].FullName : "Unknown",
                    StudentNumber = students.ContainsKey(r.StudentId) && students[r.StudentId].User != null ? students[r.StudentId].User.StudentNumber : "N/A",
                    IsPresent = r.IsPresent,
                    ScanTime = r.ScanTime
                }).ToList() ?? new List<AttendanceRecordItem>()
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult UpdateAttendance(int sessionId, List<int> presentStudentIds)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var session = _context.ExtraClassAttendanceSessions
                .Include(s => s.AttendanceRecords)
                .FirstOrDefault(s => s.Id == sessionId && s.TeacherId == teacher.Id);

            if (session == null)
                return Json(new { success = false, message = "Session not found" });

            presentStudentIds = presentStudentIds ?? new List<int>();

            foreach (var record in session.AttendanceRecords)
            {
                bool isPresent = presentStudentIds.Contains(record.StudentId);
                record.IsPresent = isPresent;
                record.Status = isPresent ? Models.AttendanceStatus.Present : Models.AttendanceStatus.Absent;
                if (isPresent && record.ScanTime == null)
                {
                    record.ScanTime = DateTime.Now;
                }
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Attendance updated successfully!" });
        }

        // ============================================
        // END SESSION
        // ============================================

        [HttpPost]
        public ActionResult EndSession(int sessionId, string topicsCovered)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var session = _context.ExtraClassAttendanceSessions
                .FirstOrDefault(s => s.Id == sessionId && s.TeacherId == teacher.Id);

            if (session == null)
                return Json(new { success = false, message = "Session not found" });

            session.TopicsCovered = topicsCovered;
            session.Status = Models.AttendanceStatus.Present;
            session.QRCodeExpiry = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, message = "Session ended successfully!" });
        }

        // ============================================
        // END CLASS (Complete the entire class)
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EndClass(int classId)
        {
            try
            {
                var teacher = GetCurrentTeacher();
                if (teacher == null)
                    return Json(new { success = false, message = "Teacher not found" });

                var extraClass = _context.ExtraClasses
                    .FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);

                if (extraClass == null)
                    return Json(new { success = false, message = "Class not found or not assigned to you" });

                if (extraClass.Status == ExtraClassStatus.Completed)
                    return Json(new { success = false, message = "This class is already completed" });

                if (extraClass.Status == ExtraClassStatus.Cancelled)
                    return Json(new { success = false, message = "This class is cancelled" });

                // Check if there are any active attendance sessions (QR code still valid)
                var activeSessions = _context.ExtraClassAttendanceSessions
                    .Any(s => s.ExtraClassId == classId && s.QRCodeExpiry > DateTime.Now);

                if (activeSessions)
                    return Json(new { success = false, message = "Please end all active sessions before ending the class" });

                // Mark the class as completed
                extraClass.Status = ExtraClassStatus.Completed;
                extraClass.EndDate = DateTime.Now;

                _context.SaveChanges();

                return Json(new { success = true, message = "Class has been completed successfully! Students can now rate the class." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EndClass Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = "Error: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        // ============================================
        // APPLY AI RECOMMENDATIONS
        // ============================================

        [HttpPost]
        public ActionResult ApplyAIRecommendation(int classId, int recommendationId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var extraClass = _context.ExtraClasses
                .FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            var recommendation = _context.ExtraClassAIRecommendations
                .FirstOrDefault(r => r.Id == recommendationId && r.ExtraClassId == classId);

            if (recommendation == null)
                return Json(new { success = false, message = "Recommendation not found" });

            recommendation.IsApplied = true;
            _context.SaveChanges();

            return Json(new { success = true, message = "AI recommendation applied successfully!" });
        }

        // ============================================
        // VIEW ENROLLED STUDENTS
        // ============================================

        public ActionResult EnrolledStudents(int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            var enrollments = _context.ExtraClassEnrollments
                .Include(e => e.Student)
                .Include(e => e.Student.User)
                .Where(e => e.ExtraClassId == classId && e.IsActive)
                .ToList();

            ViewBag.ExtraClass = extraClass;
            return View(enrollments);
        }

        // ============================================
        // SESSION HISTORY
        // ============================================

        public ActionResult SessionHistory(int? classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            if (!classId.HasValue)
            {
                var myClasses = _context.ExtraClasses
                    .Include(c => c.Subject)
                    .Include(c => c.Grade)
                    .Where(c => c.TeacherId == teacher.Id && c.IsActive)
                    .OrderBy(c => c.StartDate)
                    .ToList();

                ViewBag.ShowClassSelection = true;
                return View("SessionHistory", myClasses);
            }

            var extraClass = _context.ExtraClasses
                .FirstOrDefault(c => c.Id == classId.Value && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            var sessions = _context.ExtraClassAttendanceSessions
                .Include(s => s.AttendanceRecords)
                .Where(s => s.ExtraClassId == classId.Value)
                .OrderByDescending(s => s.SessionDate)
                .ToList();

            var sessionViewModels = sessions.Select(s => new SessionHistoryViewModel
            {
                SessionId = s.Id,
                SessionNumber = s.SessionNumber,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                TopicsCovered = s.TopicsCovered,
                TotalStudents = s.AttendanceRecords?.Count ?? 0,
                PresentCount = s.AttendanceRecords?.Count(r => r.IsPresent) ?? 0,
                AttendancePercentage = s.AttendanceRecords != null && s.AttendanceRecords.Any()
                    ? Math.Round((double)s.AttendanceRecords.Count(r => r.IsPresent) / s.AttendanceRecords.Count * 100, 1)
                    : 0
            }).ToList();

            ViewBag.ExtraClass = extraClass;
            ViewBag.ShowClassSelection = false;

            return View("SessionHistory", sessionViewModels);
        }

        // ============================================
        // VIEW FEEDBACK
        // ============================================

        public ActionResult ViewFeedback(int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (extraClass == null) return HttpNotFound();

            var feedbacks = _context.ExtraClassFeedbacks
                .Include(f => f.Student)
                .Include(f => f.Student.User)
                .Where(f => f.ExtraClassId == classId)
                .OrderByDescending(f => f.DateSubmitted)
                .ToList();

            ViewBag.ExtraClass = extraClass;
            ViewBag.AverageRating = feedbacks.Any() ? Math.Round(feedbacks.Average(f => (decimal)f.Rating), 1) : 0;
            ViewBag.TotalFeedbacks = feedbacks.Count;

            return View(feedbacks);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private Teacher GetCurrentTeacher()
        {
            var userName = User.Identity.Name;

            // Try to find the user by StudentNumber (teachers might use the same field)
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userName);
            if (user == null) return null;

            return _context.Teachers
                .Include(t => t.User)
                .FirstOrDefault(t => t.UserId == user.Id);
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