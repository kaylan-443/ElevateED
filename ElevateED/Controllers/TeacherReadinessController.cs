using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherReadinessController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private ExamReadinessService _readiness;
        private StudyPlannerService _plannerService;

        public TeacherReadinessController()
        {
            _readiness = new ExamReadinessService(_context);
            _plannerService = new StudyPlannerService(_context);
        }

        // ============================================
        // PICK A CLASS AND EXAM
        // ============================================
        public ActionResult Index()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");
            SetTeacherViewBag(teacher);

            var timetable = _plannerService.GetActiveTimetable();
            var vm = new ReadinessIndexViewModel
            {
                TeacherName = teacher.FullName,
                HasActiveTimetable = timetable != null,
                TimetableName = timetable?.Name
            };

            foreach (var a in GetTeachingAssignments(teacher.Id))
            {
                int examCount = 0;
                if (timetable != null && a.Class?.GradeId != null)
                {
                    examCount = UpcomingExams(timetable.Id, a.Class.GradeId, a.SubjectId, a.ClassId).Count;
                }

                vm.Classes.Add(new TeachableClassOption
                {
                    ClassId = a.ClassId,
                    ClassName = a.Class?.FullName ?? "Class",
                    SubjectId = a.SubjectId,
                    SubjectName = a.Subject?.Name ?? "Subject",
                    UpcomingExamCount = examCount
                });
            }

            vm.Classes = vm.Classes
                .OrderByDescending(c => c.UpcomingExamCount)
                .ThenBy(c => c.ClassName)
                .ToList();

            return View(vm);
        }

        // ============================================
        // CLASS READINESS FOR ONE EXAM
        // ============================================
        public ActionResult Class(int classId, int subjectId, int? examId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");
            SetTeacherViewBag(teacher);

            // A teacher may only review a class they are actually assigned to.
            if (!TeachesClass(teacher.Id, classId, subjectId))
                return new HttpStatusCodeResult(403, "You are not assigned to this class.");

            var cls = _context.Classes
                .Include(c => c.Grade)
                .FirstOrDefault(c => c.Id == classId);
            if (cls == null) return HttpNotFound();

            var timetable = _plannerService.GetActiveTimetable();
            if (timetable == null)
            {
                TempData["ErrorMessage"] = "There is no active exam timetable to review against.";
                return RedirectToAction("Index");
            }

            var exams = UpcomingExams(timetable.Id, cls.GradeId, subjectId, classId);
            if (!exams.Any())
            {
                TempData["ErrorMessage"] = "No upcoming exams found for this class and subject.";
                return RedirectToAction("Index");
            }

            var exam = examId.HasValue
                ? exams.FirstOrDefault(e => e.Id == examId.Value) ?? exams.First()
                : exams.First();

            var vm = new ClassReadinessViewModel
            {
                ClassId = classId,
                ClassName = cls.FullName,
                Exam = ToOption(exam),
                OtherExams = exams.Select(ToOption).ToList(),
                Results = _readiness.ForClass(classId, exam)
            };

            ViewBag.SubjectId = subjectId;
            return View(vm);
        }

        // ============================================
        // ONE LEARNER'S DETAIL BEHIND THEIR READINESS
        // ============================================
        public ActionResult Learner(int studentId, int examId, int classId, int subjectId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");
            SetTeacherViewBag(teacher);

            if (!TeachesClass(teacher.Id, classId, subjectId))
                return new HttpStatusCodeResult(403, "You are not assigned to this class.");

            var student = _context.Students
                .Include(s => s.Class)
                .FirstOrDefault(s => s.Id == studentId && s.ClassId == classId);
            if (student == null) return HttpNotFound();

            var exam = _context.ExamSessions
                .Include(e => e.Subject)
                .FirstOrDefault(e => e.Id == examId);
            if (exam == null) return HttpNotFound();

            var result = _readiness.ForStudent(student, exam);

            var activePlan = _context.StudyPlans
                .Where(p => p.StudentId == student.Id && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            var sessions = new List<StudySession>();
            if (activePlan != null)
            {
                sessions = _context.StudySessions
                    .Include(s => s.Subject)
                    .Where(s => s.StudyPlanId == activePlan.Id
                        && s.SubjectId == exam.SubjectId
                        && s.SessionDate <= exam.ExamDate)
                    .OrderBy(s => s.SessionDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();
            }

            var vm = new LearnerReadinessDetailViewModel
            {
                Result = result,
                ClassId = classId,
                ClassName = student.Class?.FullName ?? "Class",
                ExamSessionId = examId,
                Sessions = sessions
            };

            ViewBag.SubjectId = subjectId;
            return View(vm);
        }

        // ============================================
        // HELPERS
        // ============================================

        // Subject-teaching assignments only — a class-teacher row (SubjectId 0)
        // is pastoral, not a subject to assess readiness for.
        private List<TeacherSubjectAssignment> GetTeachingAssignments(int teacherId)
        {
            return _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Include(a => a.Class.Grade)
                .Include(a => a.Subject)
                .Where(a => a.TeacherId == teacherId && a.IsActive && a.SubjectId != 0)
                .ToList();
        }

        private bool TeachesClass(int teacherId, int classId, int subjectId)
        {
            return _context.TeacherSubjectAssignments.Any(a =>
                a.TeacherId == teacherId
                && a.ClassId == classId
                && a.SubjectId == subjectId
                && a.IsActive);
        }

        // Exams still ahead for this grade/subject, restricted to sessions that
        // actually apply to this class where a class mapping exists.
        private List<ExamSession> UpcomingExams(int timetableId, int gradeId, int subjectId, int classId)
        {
            var today = DateTime.Today;
            var sessions = _context.ExamSessions
                .Include(e => e.Subject)
                .Include(e => e.ExamSessionClasses)
                .Where(e => e.ExamTimetableId == timetableId
                    && e.GradeId == gradeId
                    && e.SubjectId == subjectId
                    && e.IsActive
                    && e.ExamDate >= today)
                .ToList();

            return sessions
                .Where(e => !e.ExamSessionClasses.Any()
                    || e.ExamSessionClasses.Any(c => c.ClassId == classId))
                .OrderBy(e => e.ExamDate)
                .ThenBy(e => e.StartTime)
                .ToList();
        }

        private static ReviewableExamOption ToOption(ExamSession e)
        {
            return new ReviewableExamOption
            {
                ExamSessionId = e.Id,
                SubjectName = e.Subject?.Name ?? "Subject",
                PaperNumber = e.PaperNumber,
                ExamDate = e.ExamDate,
                DaysUntil = (e.ExamDate.Date - DateTime.Today).Days
            };
        }

        private void SetTeacherViewBag(Teacher teacher)
        {
            ViewBag.ActivePage = "readiness";
            ViewBag.TeacherFirstName = teacher.FirstName;
            ViewBag.TeacherFullName = teacher.FullName;
        }

        private Teacher GetCurrentTeacher()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);
            if (user == null) return null;

            return _context.Teachers.FirstOrDefault(t => t.UserId == user.Id);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}
