using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminExtraClassController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private EmailService _emailService = new EmailService();

        // ============================================
        // DASHBOARD WITH AI ANALYTICS
        // ============================================

        public ActionResult Index()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var extraClasses = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Teacher)
                .Include(c => c.Enrollments)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            var strugglingSubjects = GetStrugglingSubjects();
            var aiRecommendation = GenerateAIRecommendation(strugglingSubjects);

            var viewModel = new AdminExtraClassDashboardViewModel
            {
                ExtraClasses = extraClasses,
                StrugglingSubjects = strugglingSubjects,
                AIRecommendation = aiRecommendation,
                TotalClasses = extraClasses.Count,
                TotalEnrollments = extraClasses.Sum(c => c.Enrollments?.Count ?? 0),
                TotalRevenue = extraClasses.Sum(c => (c.Enrollments?.Count(e => e.IsPaid) ?? 0) * c.Price),
                AverageRating = CalculateAverageRating()
            };

            return View(viewModel);
        }

        // ============================================
        // GET STRUGGLING SUBJECTS (AI ANALYSIS)
        // ============================================

        private List<StrugglingSubjectViewModel> GetStrugglingSubjects()
        {
            var strugglingSubjects = new List<StrugglingSubjectViewModel>();

            var grades = _context.Grades.ToList();

            foreach (var grade in grades)
            {
                var subjects = GetSubjectsForGradeLevel(grade.Level);

                foreach (var subject in subjects)
                {
                    var students = _context.Students
                        .Include(s => s.Class)
                        .Where(s => s.IsActive && s.Class.GradeId == grade.Id)
                        .ToList();

                    if (!students.Any()) continue;

                    var assessments = _context.Assessments
                        .Include(a => a.Marks)
                        .Include(a => a.Class)
                        .Where(a => a.SubjectId == subject.Id
                            && a.Class.GradeId == grade.Id
                            && a.Status == MarkApprovalStatus.Approved)
                        .ToList();

                    if (!assessments.Any()) continue;

                    var allMarks = new List<decimal>();
                    foreach (var assessment in assessments)
                    {
                        var marks = assessment.Marks
                            .Where(m => m.Mark.HasValue)
                            .Select(m => (decimal)((m.Mark.Value / assessment.MaxMark) * 100))
                            .ToList();
                        allMarks.AddRange(marks);
                    }

                    decimal averageMark = 0;
                    if (allMarks.Any())
                    {
                        averageMark = Math.Round(allMarks.Average(), 1);
                    }

                    if (averageMark < 55)
                    {
                        decimal passRate = 0;
                        if (allMarks.Any())
                        {
                            passRate = Math.Round((decimal)allMarks.Count(m => m >= 50) * 100 / allMarks.Count, 1);
                        }

                        strugglingSubjects.Add(new StrugglingSubjectViewModel
                        {
                            SubjectId = subject.Id,
                            SubjectName = subject.Name,
                            GradeId = grade.Id,
                            GradeName = grade.Name,
                            AverageMark = averageMark,
                            PassRate = passRate,
                            StudentCount = students.Count,
                            Recommendation = GetRecommendationMessage(averageMark)
                        });
                    }
                }
            }

            return strugglingSubjects.OrderBy(s => s.AverageMark).Take(5).ToList();
        }

        private string GetRecommendationMessage(decimal averageMark)
        {
            if (averageMark < 40)
                return "CRITICAL: Immediate intervention required. Students are severely underperforming.";
            if (averageMark < 50)
                return "URGENT: Extra classes strongly recommended to improve pass rates.";
            if (averageMark < 55)
                return "Moderate: Extra classes would help boost learner confidence and performance.";
            return "On track: Continue monitoring progress.";
        }

        private AIRecommendationViewModel GenerateAIRecommendation(List<StrugglingSubjectViewModel> strugglingSubjects)
        {
            if (!strugglingSubjects.Any())
                return null;

            var worstPerforming = strugglingSubjects.OrderBy(s => s.AverageMark).First();

            return new AIRecommendationViewModel
            {
                SubjectName = worstPerforming.SubjectName,
                GradeName = worstPerforming.GradeName,
                AverageMark = worstPerforming.AverageMark,
                RecommendedAction = $"Create an extra class for {worstPerforming.GradeName} {worstPerforming.SubjectName}",
                EstimatedTopics = GetRecommendedTopics(worstPerforming.SubjectName, worstPerforming.GradeName),
                PredictedImprovement = Math.Round((75 - worstPerforming.AverageMark) * 0.6m, 1)
            };
        }

        private List<string> GetRecommendedTopics(string subjectName, string gradeName)
        {
            return new List<string>
            {
                "Basic concepts review",
                "Problem-solving techniques",
                "Common mistakes analysis",
                "Practice with past papers",
                "Interactive group activities"
            };
        }

        private decimal CalculateAverageRating()
        {
            var feedbacks = _context.ExtraClassFeedbacks.ToList();
            if (!feedbacks.Any()) return 0;
            return Math.Round(feedbacks.Average(f => (decimal)f.Rating), 1);
        }

        private List<Subject> GetSubjectsForGradeLevel(int gradeLevel)
        {
            if (gradeLevel <= 9)
            {
                return _context.Subjects
                    .Where(s => s.Category == SubjectCategory.Core)
                    .ToList();
            }
            else
            {
                var grade8And9Only = new[] { "Natural Science", "Social Science", "Creative Arts", "Economic Management Science", "Technology" };
                return _context.Subjects
                    .Where(s => !grade8And9Only.Contains(s.Name))
                    .ToList();
            }
        }

        // ============================================
        // CREATE EXTRA CLASS (MANUAL + AI-ASSISTED)
        // ============================================

        public ActionResult Create(int? strugglingSubjectId = null, int? strugglingGradeId = null)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var viewModel = new CreateExtraClassViewModel();

            if (strugglingSubjectId.HasValue && strugglingGradeId.HasValue)
            {
                var subject = _context.Subjects.Find(strugglingSubjectId.Value);
                var grade = _context.Grades.Find(strugglingGradeId.Value);

                if (subject != null && grade != null)
                {
                    viewModel.SubjectId = subject.Id;
                    viewModel.GradeId = grade.Id;
                    viewModel.Name = $"{grade.Name} {subject.Name} Intervention";
                    viewModel.Description = $"Extra class to help {grade.Name} students improve their {subject.Name} performance.";
                    viewModel.Schedule = "Saturdays 09:00-11:00";
                    viewModel.StartDate = DateTime.Now.AddDays(7);
                    viewModel.EndDate = DateTime.Now.AddDays(7 + 42);
                    viewModel.Price = 350;
                    viewModel.Capacity = 25;
                    viewModel.IsAIGenerated = true;
                }
            }

            PopulateCreateDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateExtraClassViewModel model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var extraClass = new ExtraClass
                {
                    Name = model.Name,
                    Description = model.Description,
                    SubjectId = model.SubjectId,
                    GradeId = model.GradeId,
                    TeacherId = model.TeacherId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Schedule = model.Schedule,
                    Venue = model.Venue,
                    Price = model.Price,
                    Capacity = model.Capacity,
                    CurrentEnrollment = 0,
                    Status = ExtraClassStatus.Upcoming,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.ExtraClasses.Add(extraClass);
                _context.SaveChanges();

                GenerateAndSaveAIRecommendations(extraClass.Id);

                TempData["SuccessMessage"] = $"Extra class '{model.Name}' created successfully!";
                return RedirectToAction("Index");
            }

            PopulateCreateDropdowns(model);
            return View(model);
        }

        private void GenerateAndSaveAIRecommendations(int extraClassId)
        {
            var extraClass = _context.ExtraClasses.Find(extraClassId);
            if (extraClass == null) return;

            var strugglingTopics = GetStrugglingTopics(extraClass.SubjectId, extraClass.GradeId);

            var recommendation = new ExtraClassAIRecommendation
            {
                ExtraClassId = extraClassId,
                RecommendedTopics = JsonConvert.SerializeObject(strugglingTopics.EasyTopics),
                DifficultTopics = JsonConvert.SerializeObject(strugglingTopics.DifficultTopics),
                SuggestedTeachingOrder = JsonConvert.SerializeObject(GetSuggestedTeachingOrder(strugglingTopics)),
                EasyWinTopics = JsonConvert.SerializeObject(strugglingTopics.EasyTopics.Take(3)),
                CommonMistakes = JsonConvert.SerializeObject(GetCommonMistakes(extraClass.SubjectId)),
                PredictedImprovement = 15,
                IsApplied = false
            };

            _context.ExtraClassAIRecommendations.Add(recommendation);
            _context.SaveChanges();
        }

        private StrugglingTopicsResult GetStrugglingTopics(int subjectId, int gradeId)
        {
            return new StrugglingTopicsResult
            {
                EasyTopics = new List<string> { "Basic concepts", "Simple calculations", "Definitions" },
                DifficultTopics = new List<string> { "Complex problem solving", "Application questions", "Analysis" }
            };
        }

        private List<string> GetSuggestedTeachingOrder(StrugglingTopicsResult topics)
        {
            var order = new List<string>();
            order.AddRange(topics.EasyTopics);
            order.AddRange(topics.DifficultTopics);
            return order;
        }

        private List<string> GetCommonMistakes(int subjectId)
        {
            return new List<string> { "Misreading questions", "Calculation errors", "Missing steps" };
        }

        private void PopulateCreateDropdowns(CreateExtraClassViewModel model)
        {
            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Teachers = _context.Teachers
                .Include(t => t.User)
                .Where(t => t.IsActive)
                .OrderBy(t => t.LastName)
                .ToList();
        }

        // ============================================
        // EXTRA CLASS DETAILS
        // ============================================

        public ActionResult Details(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .Include(c => c.Teacher)
                .Include(c => c.Enrollments.Select(e => e.Student))
                .Include(c => c.AttendanceSessions)
                .Include(c => c.Feedbacks)
                .Include(c => c.AIRecommendations)
                .FirstOrDefault(c => c.Id == id);

            if (extraClass == null) return HttpNotFound();

            var totalEnrolled = extraClass.Enrollments?.Count(e => e.IsActive) ?? 0;
            var totalPaid = extraClass.Enrollments?.Count(e => e.IsPaid) ?? 0;

            var viewModel = new ExtraClassDetailsViewModel
            {
                ExtraClass = extraClass,
                Enrollments = extraClass.Enrollments?.ToList() ?? new List<ExtraClassEnrollment>(),
                AttendanceSessions = extraClass.AttendanceSessions?.OrderByDescending(s => s.SessionDate).ToList() ?? new List<ExtraClassAttendanceSession>(),
                Feedbacks = extraClass.Feedbacks?.OrderByDescending(f => f.DateSubmitted).ToList() ?? new List<ExtraClassFeedback>(),
                AIRecommendation = extraClass.AIRecommendations?.OrderByDescending(r => r.GeneratedDate).FirstOrDefault(),
                AttendanceRate = CalculateAttendanceRate(extraClass.Id),
                AverageRating = extraClass.Feedbacks != null && extraClass.Feedbacks.Any() ? Math.Round(extraClass.Feedbacks.Average(f => (decimal)f.Rating), 1) : 0,
                TotalEnrolled = totalEnrolled,
                TotalPaid = totalPaid
            };

            ViewBag.Teachers = _context.Teachers
                .Include(t => t.User)
                .Where(t => t.IsActive)
                .OrderBy(t => t.LastName)
                .ToList();

            return View(viewModel);
        }

        private decimal CalculateAttendanceRate(int extraClassId)
        {
            var sessions = _context.ExtraClassAttendanceSessions
                .Include(s => s.AttendanceRecords)
                .Where(s => s.ExtraClassId == extraClassId)
                .ToList();

            if (!sessions.Any()) return 0;

            int totalRecords = 0;
            int presentRecords = 0;

            foreach (var session in sessions)
            {
                if (session.AttendanceRecords != null)
                {
                    totalRecords += session.AttendanceRecords.Count;
                    presentRecords += session.AttendanceRecords.Count(r => r.IsPresent);
                }
            }

            if (totalRecords == 0) return 0;

            decimal result = ((decimal)presentRecords / totalRecords) * 100;
            return Math.Round(result, 1);
        }

        // ============================================
        // ASSIGN TEACHER
        // ============================================

        [HttpPost]
        public ActionResult AssignTeacher(int classId, int teacherId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var extraClass = _context.ExtraClasses.Find(classId);
            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            var teacher = _context.Teachers
                .Include(t => t.User)
                .FirstOrDefault(t => t.Id == teacherId);

            if (teacher == null)
                return Json(new { success = false, message = "Teacher not found" });

            extraClass.TeacherId = teacherId;
            _context.SaveChanges();

            try
            {
                _emailService.SendCustomEmail(teacher.User.Email, "Extra Class Assignment - ElevateED",
                    $@"<h3>Extra Class Assigned</h3>
                       <p>Dear {teacher.FullName},</p>
                       <p>You have been assigned to teach:</p>
                       <p><strong>{extraClass.Name}</strong></p>
                       <p>Schedule: {extraClass.Schedule}</p>
                       <p>Venue: {extraClass.Venue ?? "To be confirmed"}</p>
                       <p>Start Date: {extraClass.StartDate:dd MMM yyyy}</p>
                       <p>Please log in to your teacher dashboard to view the class details and AI recommendations.</p>");
            }
            catch { }

            return Json(new { success = true, message = $"Teacher {teacher.FullName} assigned successfully!" });
        }

        // ============================================
        // ENROLLMENTS MANAGEMENT
        // ============================================

        public ActionResult Enrollments(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var extraClass = _context.ExtraClasses
                .Include(c => c.Subject)
                .Include(c => c.Grade)
                .FirstOrDefault(c => c.Id == id);

            if (extraClass == null) return HttpNotFound();

            var enrollments = _context.ExtraClassEnrollments
                .Include(e => e.Student)
                .Include(e => e.Student.User)
                .Where(e => e.ExtraClassId == id)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            ViewBag.ExtraClass = extraClass;
            return View(enrollments);
        }

        [HttpPost]
        public ActionResult DeleteEnrollment(int enrollmentId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var enrollment = _context.ExtraClassEnrollments
                .Include(e => e.ExtraClass)
                .FirstOrDefault(e => e.Id == enrollmentId);

            if (enrollment == null)
                return Json(new { success = false, message = "Enrollment not found" });

            var className = enrollment.ExtraClass.Name;

            _context.ExtraClassEnrollments.Remove(enrollment);
            _context.SaveChanges();

            return Json(new { success = true, message = $"Student removed from {className}" });
        }

        // ============================================
        // MARK PAYMENT
        // ============================================

        [HttpPost]
        public ActionResult MarkPayment(int enrollmentId, string paymentReference)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var enrollment = _context.ExtraClassEnrollments.Find(enrollmentId);
            if (enrollment == null)
                return Json(new { success = false, message = "Enrollment not found" });

            enrollment.IsPaid = true;
            enrollment.PaymentDate = DateTime.Now;
            enrollment.PaymentReference = paymentReference;

            _context.SaveChanges();

            return Json(new { success = true, message = "Payment marked as complete" });
        }

        // ============================================
        // ANALYTICS & REPORTS
        // ============================================

        public ActionResult Analytics()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var extraClasses = _context.ExtraClasses
                .Include(c => c.Enrollments)
                .Include(c => c.AttendanceSessions)
                .Include(c => c.Feedbacks)
                .Where(c => c.IsActive)
                .ToList();

            var analytics = new ExtraClassAnalyticsViewModel
            {
                TotalClasses = extraClasses.Count,
                TotalEnrollments = extraClasses.Sum(c => c.Enrollments?.Count ?? 0),
                TotalPaidEnrollments = extraClasses.Sum(c => c.Enrollments?.Count(e => e.IsPaid) ?? 0),
                TotalRevenue = extraClasses.Sum(c => (c.Enrollments?.Count(e => e.IsPaid) ?? 0) * c.Price),
                AverageAttendanceRate = CalculateOverallAttendanceRate(extraClasses),
                AverageRating = CalculateOverallAverageRating(extraClasses),
                ClassPerformance = extraClasses.Select(c => new ClassPerformanceViewModel
                {
                    ClassName = c.Name,
                    EnrollmentCount = c.Enrollments?.Count ?? 0,
                    AttendanceRate = CalculateAttendanceRate(c.Id),
                    AverageRating = c.Feedbacks != null && c.Feedbacks.Any() ? Math.Round((decimal)c.Feedbacks.Average(f => f.Rating), 1) : 0,
                    Revenue = (c.Enrollments?.Count(e => e.IsPaid) ?? 0) * c.Price
                }).ToList()
            };

            return View(analytics);
        }

        private decimal CalculateOverallAttendanceRate(List<ExtraClass> extraClasses)
        {
            int totalRecords = 0;
            int presentRecords = 0;

            foreach (var extraClass in extraClasses)
            {
                if (extraClass.AttendanceSessions != null)
                {
                    foreach (var session in extraClass.AttendanceSessions)
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

            decimal result = ((decimal)presentRecords / totalRecords) * 100;
            return Math.Round(result, 1);
        }

        private decimal CalculateOverallAverageRating(List<ExtraClass> extraClasses)
        {
            var allFeedback = new List<decimal>();
            foreach (var extraClass in extraClasses)
            {
                if (extraClass.Feedbacks != null)
                {
                    foreach (var feedback in extraClass.Feedbacks)
                    {
                        allFeedback.Add((decimal)feedback.Rating);
                    }
                }
            }

            if (!allFeedback.Any()) return 0;
            return Math.Round(allFeedback.Average(), 1);
        }

        // ============================================
        // DELETE EXTRA CLASS
        // ============================================

        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var extraClass = _context.ExtraClasses.Find(id);
            if (extraClass == null)
                return Json(new { success = false, message = "Class not found" });

            extraClass.IsActive = false;
            extraClass.Status = ExtraClassStatus.Cancelled;
            _context.SaveChanges();

            return Json(new { success = true, message = "Extra class deleted successfully" });
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
    }
}