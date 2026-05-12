using System.Data.Entity;
using System.Data.Entity.Core;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;

namespace ElevateED.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly IAttendanceService _attendanceService;

        public AnalyticsController()
        {
            _attendanceService = new AttendanceService();
        }

        [HttpGet]
        [Authorize(Roles = "Teacher,Admin")]
        public ActionResult Index(string filter = "weekly", int? classId = null)
        {
            string teacherId = null;

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;

                if (User.IsInRole("Teacher"))
                {
                    var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                    if (user != null)
                    {
                        teacherId = user.StudentNumber;
                    }
                }
            }

            var viewModel = _attendanceService.GetAnalytics(filter, classId, teacherId);
            PopulateAcademicAnalytics(viewModel);
            return View(viewModel);
        }

        private void PopulateAcademicAnalytics(AnalyticsViewModel viewModel)
        {
            try
            {
                using (var db = new ElevateEDContext())
                {
                    if (User.IsInRole("Teacher"))
                    {
                        PopulateTeacherAcademicAnalytics(db, viewModel);
                    }
                    else
                    {
                        PopulateAdminAcademicAnalytics(db, viewModel);
                    }
                }
            }
            catch (EntityCommandExecutionException)
            {
                viewModel.SubjectPerformance.Clear();
                viewModel.ClassPerformance.Clear();
            }
        }

        private void PopulateTeacherAcademicAnalytics(ElevateEDContext db, AnalyticsViewModel viewModel)
        {
            var user = db.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
            var teacher = user == null ? null : db.Teachers.FirstOrDefault(t => t.UserId == user.Id);
            if (teacher == null) return;

            var assessments = db.Assessments
                .Include(a => a.Subject)
                .Include(a => a.Class)
                .Include(a => a.Marks)
                .Where(a => a.TeacherId == teacher.Id)
                .ToList();

            viewModel.TotalAssessments = assessments.Count;
            viewModel.SubmittedAssessments = assessments.Count(a => a.Status == MarkApprovalStatus.Submitted);
            viewModel.ApprovedAssessments = assessments.Count(a => a.Status == MarkApprovalStatus.Approved);

            var approved = assessments.Where(a => a.Status == MarkApprovalStatus.Approved && a.MaxMark > 0).ToList();
            var marks = approved
                .SelectMany(a => a.Marks
                    .Where(m => m.Mark.HasValue)
                    .Select(m => (m.Mark.Value / a.MaxMark) * 100))
                .ToList();

            viewModel.AcademicAverage = marks.Any() ? System.Math.Round(marks.Average(), 1) : 0;
            viewModel.AcademicPassRate = marks.Any() ? System.Math.Round((decimal)marks.Count(m => m >= 50) * 100 / marks.Count, 1) : 0;

            viewModel.SubjectPerformance = approved
                .Where(a => a.Subject != null)
                .GroupBy(a => a.Subject.Name)
                .Select(g =>
                {
                    var groupedMarks = g.SelectMany(a => a.Marks
                        .Where(m => m.Mark.HasValue)
                        .Select(m => (m.Mark.Value / a.MaxMark) * 100))
                        .ToList();

                    return new AnalyticsAcademicStat
                    {
                        Name = g.Key,
                        Average = groupedMarks.Any() ? System.Math.Round(groupedMarks.Average(), 1) : 0,
                        PassRate = groupedMarks.Any() ? System.Math.Round((decimal)groupedMarks.Count(m => m >= 50) * 100 / groupedMarks.Count, 1) : 0,
                        Count = groupedMarks.Count
                    };
                })
                .OrderBy(s => s.Average)
                .ToList();

            viewModel.ClassPerformance = approved
                .Where(a => a.Class != null)
                .GroupBy(a => a.Class.FullName)
                .Select(g =>
                {
                    var groupedMarks = g.SelectMany(a => a.Marks
                        .Where(m => m.Mark.HasValue)
                        .Select(m => (m.Mark.Value / a.MaxMark) * 100))
                        .ToList();

                    return new AnalyticsAcademicStat
                    {
                        Name = g.Key,
                        Average = groupedMarks.Any() ? System.Math.Round(groupedMarks.Average(), 1) : 0,
                        PassRate = groupedMarks.Any() ? System.Math.Round((decimal)groupedMarks.Count(m => m >= 50) * 100 / groupedMarks.Count, 1) : 0,
                        Count = groupedMarks.Count
                    };
                })
                .OrderBy(c => c.Average)
                .ToList();
        }

        private void PopulateAdminAcademicAnalytics(ElevateEDContext db, AnalyticsViewModel viewModel)
        {
            var reports = db.StudentReportCards
                .Include(r => r.Class)
                .Include(r => r.Subjects.Select(s => s.Subject))
                .ToList();

            viewModel.TotalAssessments = db.Assessments.Count();
            viewModel.SubmittedAssessments = db.Assessments.Count(a => a.Status == MarkApprovalStatus.Submitted);
            viewModel.ApprovedAssessments = db.Assessments.Count(a => a.Status == MarkApprovalStatus.Approved);
            viewModel.GeneratedReportCards = reports.Count;
            viewModel.AcademicAverage = reports.Any() ? System.Math.Round(reports.Average(r => r.FinalMark), 1) : 0;
            viewModel.AcademicPassRate = reports.Any() ? System.Math.Round((decimal)reports.Count(r => r.FinalMark >= 50) * 100 / reports.Count, 1) : 0;

            var subjectRows = reports.SelectMany(r => r.Subjects).ToList();
            viewModel.SubjectPerformance = subjectRows
                .Where(s => s.Subject != null)
                .GroupBy(s => s.Subject.Name)
                .Select(g => new AnalyticsAcademicStat
                {
                    Name = g.Key,
                    Average = System.Math.Round(g.Average(s => s.FinalMark), 1),
                    PassRate = System.Math.Round((decimal)g.Count(s => s.FinalMark >= 50) * 100 / g.Count(), 1),
                    Count = g.Count()
                })
                .OrderBy(s => s.Average)
                .ToList();

            viewModel.ClassPerformance = reports
                .Where(r => r.Class != null)
                .GroupBy(r => r.Class.FullName)
                .Select(g => new AnalyticsAcademicStat
                {
                    Name = g.Key,
                    Average = System.Math.Round(g.Average(r => r.FinalMark), 1),
                    PassRate = System.Math.Round((decimal)g.Count(r => r.FinalMark >= 50) * 100 / g.Count(), 1),
                    Count = g.Count()
                })
                .OrderBy(c => c.Average)
                .ToList();
        }

        [HttpGet]
        [Authorize(Roles = "Principal")]
        public ActionResult Principal()
        {
            using (var db = new ElevateEDContext())
            {
                var reports = db.StudentReportCards
                    .Include(r => r.Student)
                    .Include(r => r.Class)
                    .Include(r => r.Subjects.Select(s => s.Subject))
                    .ToList();

                var subjectRows = reports.SelectMany(r => r.Subjects).ToList();
                var approvedAssessments = db.Assessments
                    .Include(a => a.Teacher)
                    .Include(a => a.Marks)
                    .Where(a => a.Status == MarkApprovalStatus.Approved)
                    .ToList();

                var model = new PrincipalAnalyticsViewModel
                {
                    TotalStudents = db.Students.Count(s => s.IsActive),
                    TotalTeachers = db.Teachers.Count(t => t.IsActive),
                    TotalClasses = db.Classes.Count(),
                    TotalAssessments = db.Assessments.Count(),
                    PendingMarkApprovals = db.Assessments.Count(a => a.Status == MarkApprovalStatus.Submitted),
                    GeneratedReportCards = reports.Count,
                    PublishedExamSessions = db.ExamSessions.Count(e => e.IsActive && (e.Status == ExamSessionStatus.Published || e.Status == ExamSessionStatus.Approved)),
                    SchoolAverage = reports.Any() ? System.Math.Round(reports.Average(r => r.FinalMark), 1) : 0,
                    PassRate = reports.Any() ? System.Math.Round((decimal)reports.Count(r => r.FinalMark >= 50) * 100 / reports.Count, 1) : 0,
                    PromotionRate = reports.Any() ? System.Math.Round((decimal)reports.Count(r => r.PromotionDecision == PromotionDecision.Promoted) * 100 / reports.Count, 1) : 0,
                    ProgressionRate = reports.Any() ? System.Math.Round((decimal)reports.Count(r => r.PromotionDecision == PromotionDecision.Progressed) * 100 / reports.Count, 1) : 0,
                    NotPromotedRate = reports.Any() ? System.Math.Round((decimal)reports.Count(r => r.PromotionDecision == PromotionDecision.NotPromoted) * 100 / reports.Count, 1) : 0
                };

                model.SubjectPerformance = subjectRows
                    .Where(s => s.Subject != null)
                    .GroupBy(s => s.Subject.Name)
                    .Select(g => new PrincipalSubjectAnalyticsItem
                    {
                        SubjectName = g.Key,
                        Average = System.Math.Round(g.Average(x => x.FinalMark), 1),
                        PassRate = System.Math.Round((decimal)g.Count(x => x.FinalMark >= 50) * 100 / g.Count(), 1),
                        LearnerCount = g.Count()
                    })
                    .OrderBy(s => s.Average)
                    .ToList();

                model.ClassPerformance = reports
                    .Where(r => r.Class != null)
                    .GroupBy(r => r.Class.FullName)
                    .Select(g => new PrincipalClassAnalyticsItem
                    {
                        ClassName = g.Key,
                        Average = System.Math.Round(g.Average(x => x.FinalMark), 1),
                        PassRate = System.Math.Round((decimal)g.Count(x => x.FinalMark >= 50) * 100 / g.Count(), 1),
                        PromotedCount = g.Count(x => x.PromotionDecision == PromotionDecision.Promoted),
                        ProgressedCount = g.Count(x => x.PromotionDecision == PromotionDecision.Progressed),
                        NotPromotedCount = g.Count(x => x.PromotionDecision == PromotionDecision.NotPromoted)
                    })
                    .OrderBy(c => c.Average)
                    .ToList();

                model.TeacherPerformance = approvedAssessments
                    .Where(a => a.Teacher != null)
                    .GroupBy(a => a.Teacher.FullName)
                    .Select(g =>
                    {
                        var marks = g.SelectMany(a => a.Marks
                            .Where(m => m.Mark.HasValue && a.MaxMark > 0)
                            .Select(m => new { Mark = m.Mark.Value, a.MaxMark }))
                            .ToList();

                        return new PrincipalTeacherAnalyticsItem
                        {
                            TeacherName = g.Key,
                            AssessmentCount = g.Count(),
                            ApprovedAssessmentCount = g.Count(),
                            AverageLearnerMark = marks.Any()
                                ? System.Math.Round(marks.Average(m => (m.Mark / m.MaxMark) * 100), 1)
                                : 0,
                            PassRate = marks.Any()
                                ? System.Math.Round((decimal)marks.Count(m => ((m.Mark / m.MaxMark) * 100) >= 50) * 100 / marks.Count, 1)
                                : 0
                        };
                    })
                    .OrderBy(t => t.AverageLearnerMark)
                    .ToList();

                model.AtRiskLearners = reports
                    .Where(r => r.FinalMark < 50 || r.PromotionDecision == PromotionDecision.NotPromoted)
                    .OrderBy(r => r.FinalMark)
                    .Take(20)
                    .Select(r => new PrincipalAtRiskLearnerItem
                    {
                        StudentName = r.Student?.FullName,
                        ClassName = r.Class?.FullName,
                        FinalMark = r.FinalMark,
                        PromotionDecision = r.PromotionDecision.ToString(),
                        Reason = r.FinalMark < 50 ? "Final mark below pass level" : "Promotion rule blocked"
                    })
                    .ToList();

                model.ExamTimetables = db.ExamTimetables
                    .Include(t => t.ExamSessions)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToList()
                    .Select(t => new PrincipalExamAnalyticsItem
                    {
                        Name = t.Name,
                        Status = t.Status.ToString(),
                        TotalSessions = t.ExamSessions.Count(s => s.IsActive),
                        ProposedSessions = t.ExamSessions.Count(s => s.IsActive && s.Status == ExamSessionStatus.Proposed),
                        PublishedSessions = t.ExamSessions.Count(s => s.IsActive && (s.Status == ExamSessionStatus.Published || s.Status == ExamSessionStatus.Approved))
                    })
                    .ToList();

                return View(model);
            }
        }
    }
}
