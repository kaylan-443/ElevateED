using ElevateED.Models;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize]
    public class MarksController : Controller
    {
        private readonly ElevateEDContext _context = new ElevateEDContext();

        [Authorize(Roles = "Teacher")]
        public ActionResult Index()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            // Show assessments the teacher owns, plus assessments belonging to any class
            // where this teacher is the homeroom (class) teacher — Option A fallback.
            var assessments = _context.Assessments
                .Include(a => a.Class)
                .Include(a => a.Subject)
                .Include(a => a.Teacher)
                .Include(a => a.Marks)
                .Where(a => a.TeacherId == teacher.Id
                    || (a.Class != null && a.Class.ClassTeacherId == teacher.Id))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            var classIds = assessments.Select(a => a.ClassId).Distinct().ToList();
            var learnerCounts = _context.Students
                .Where(s => s.ClassId.HasValue && classIds.Contains(s.ClassId.Value) && s.IsActive)
                .GroupBy(s => s.ClassId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var model = assessments.Select(a => new AssessmentListItemViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Term = a.Term,
                AcademicYear = a.AcademicYear,
                ClassName = a.Class?.FullName,
                SubjectName = a.Subject?.Name,
                AssessmentType = a.AssessmentType.ToString(),
                MaxMark = a.MaxMark,
                Status = a.Status,
                CapturedCount = a.Marks.Count(m => m.Mark.HasValue),
                LearnerCount = learnerCounts.ContainsKey(a.ClassId) ? learnerCounts[a.ClassId] : 0
            }).ToList();

            return View(model);
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult Create()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var model = new CreateAssessmentViewModel
            {
                AcademicYear = DateTime.Now.Year,
                Term = GetCurrentTerm(),
                MaxMark = 100,
                Weight = 1,
                AssessmentDate = DateTime.Today
            };

            PopulateCreateOptions(model, teacher.Id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher")]
        public ActionResult Create(CreateAssessmentViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var assignment = _context.TeacherSubjectAssignments
                .FirstOrDefault(a => a.Id == model.AssignmentId && a.TeacherId == teacher.Id && a.IsActive && a.SubjectId > 0);

            if (assignment == null)
            {
                ModelState.AddModelError("", "Choose one of your assigned classes and subjects.");
            }

            if (!ModelState.IsValid)
            {
                PopulateCreateOptions(model, teacher.Id);
                return View(model);
            }

            var assessment = new Assessment
            {
                Name = model.Name,
                Term = model.Term,
                AcademicYear = model.AcademicYear,
                AssessmentType = model.AssessmentType,
                MaxMark = model.MaxMark,
                Weight = model.Weight,
                AssessmentDate = model.AssessmentDate,
                TeacherId = teacher.Id,
                ClassId = assignment.ClassId,
                SubjectId = assignment.SubjectId
            };

            _context.Assessments.Add(assessment);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Assessment created. You can now capture learner marks.";
            return RedirectToAction("Capture", new { id = assessment.Id });
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult Capture(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var assessment = GetAssessmentForTeacher(id, teacher.Id);
            if (assessment == null) return HttpNotFound();

            return View(BuildCaptureModel(assessment, teacher.Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher")]
        public ActionResult Capture(MarkCaptureViewModel model, string finalizeMarks)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var assessment = GetAssessmentForTeacher(model.AssessmentId, teacher.Id);
            if (assessment == null) return HttpNotFound();
            if (assessment.Status == MarkApprovalStatus.Approved)
            {
                TempData["ErrorMessage"] = "Finalized marks cannot be changed.";
                return RedirectToAction("Capture", new { id = assessment.Id });
            }

            var classStudentIds = _context.Students
                .Where(s => s.ClassId == assessment.ClassId && s.IsActive)
                .Select(s => s.Id)
                .ToList();

            foreach (var entry in model.Marks.Where(m => classStudentIds.Contains(m.StudentId)))
            {
                if (entry.Mark.HasValue && (entry.Mark.Value < 0 || entry.Mark.Value > assessment.MaxMark))
                {
                    ModelState.AddModelError("", "Marks must be between 0 and the assessment max mark.");
                    return View(BuildCaptureModel(assessment, teacher.Id));
                }

                var mark = assessment.Marks.FirstOrDefault(m => m.StudentId == entry.StudentId);
                if (mark == null)
                {
                    mark = new AssessmentMark { AssessmentId = assessment.Id, StudentId = entry.StudentId };
                    _context.AssessmentMarks.Add(mark);
                }

                mark.Mark = entry.Mark;
                mark.Comment = entry.Comment;
                mark.CapturedAt = DateTime.Now;
            }

            var isFinalize = !string.IsNullOrEmpty(finalizeMarks);
            if (isFinalize)
            {
                // Teachers own marks — finalizing approves the assessment directly and
                // triggers automatic report-card regeneration. No principal mark-approval step.
                assessment.Status = MarkApprovalStatus.Approved;
                assessment.SubmittedAt = DateTime.Now;
                assessment.ApprovedAt = DateTime.Now;
            }

            _context.SaveChanges();

            if (isFinalize)
            {
                GenerateReportCards(assessment.ClassId, assessment.Term, assessment.AcademicYear);
                TempData["SuccessMessage"] = "Marks finalized. Report cards have been regenerated for this class and term.";
            }
            else
            {
                TempData["SuccessMessage"] = "Marks saved as draft.";
            }

            return RedirectToAction("Index");
        }

        // ============================================================
        // CLASS TEACHER — adds report-card comments before publication
        // ============================================================
        [Authorize(Roles = "Teacher")]
        public ActionResult ClassTeacherReports()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var classIds = _context.Classes
                .Where(c => c.ClassTeacherId == teacher.Id)
                .Select(c => c.Id)
                .ToList();

            if (!classIds.Any())
            {
                return View(new List<ClassTeacherReportListItemViewModel>());
            }

            var model = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .Where(r => classIds.Contains(r.ClassId))
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.Term)
                .ThenBy(r => r.Class.FullName)
                .ThenBy(r => r.Student.LastName)
                .ToList()
                .Select(r => new ClassTeacherReportListItemViewModel
                {
                    ReportId = r.Id,
                    StudentName = r.Student?.FullName,
                    ClassName = r.Class?.FullName,
                    Term = r.Term,
                    AcademicYear = r.AcademicYear,
                    FinalMark = r.FinalMark,
                    PassFailStatus = r.PassFailStatus,
                    PromotionDecision = r.PromotionDecision,
                    HasClassTeacherComment = !string.IsNullOrWhiteSpace(r.ClassTeacherComment),
                    IsPublished = r.IsPublished
                })
                .ToList();

            return View(model);
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult EditReportComment(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var report = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .FirstOrDefault(r => r.Id == id);
            if (report == null) return HttpNotFound();
            if (report.Class?.ClassTeacherId != teacher.Id) return new HttpUnauthorizedResult();

            return View(ToCommentViewModel(report));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher")]
        public ActionResult EditReportComment(ClassTeacherReportCommentViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var report = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .FirstOrDefault(r => r.Id == model.ReportId);
            if (report == null) return HttpNotFound();
            if (report.Class?.ClassTeacherId != teacher.Id) return new HttpUnauthorizedResult();
            if (report.IsPublished)
            {
                TempData["ErrorMessage"] = "This report has already been published. Comments are locked.";
                return RedirectToAction("ClassTeacherReports");
            }

            report.ClassTeacherComment = model.ClassTeacherComment;
            report.ClassTeacherId = teacher.Id;
            report.ClassTeacherCommentedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Class-teacher comment saved.";
            return RedirectToAction("ClassTeacherReports");
        }

        // ============================================================
        // PRINCIPAL — approves and publishes reports (not individual marks)
        // ============================================================
        [Authorize(Roles = "Admin,Principal")]
        public ActionResult PendingReports()
        {
            var model = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .Include(r => r.ClassTeacher)
                .Where(r => !r.IsPublished)
                .OrderBy(r => r.Class.FullName)
                .ThenBy(r => r.Student.LastName)
                .ToList()
                .Select(r => new PendingReportListItemViewModel
                {
                    ReportId = r.Id,
                    StudentName = r.Student?.FullName,
                    ClassName = r.Class?.FullName,
                    Term = r.Term,
                    AcademicYear = r.AcademicYear,
                    FinalMark = r.FinalMark,
                    PassFailStatus = r.PassFailStatus,
                    PromotionDecision = r.PromotionDecision,
                    ClassTeacherName = r.ClassTeacher?.FullName,
                    HasClassTeacherComment = !string.IsNullOrWhiteSpace(r.ClassTeacherComment),
                    IsPublished = r.IsPublished,
                    GeneratedAt = r.GeneratedAt
                })
                .ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Principal")]
        public ActionResult PublishReport(int id)
        {
            var report = _context.StudentReportCards.Find(id);
            if (report == null) return HttpNotFound();

            report.IsPublished = true;
            report.PublishedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Report published — the student can now view it.";
            return RedirectToAction("PendingReports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Principal")]
        public ActionResult UnpublishReport(int id)
        {
            var report = _context.StudentReportCards.Find(id);
            if (report == null) return HttpNotFound();

            report.IsPublished = false;
            report.PublishedAt = null;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Report unpublished — it is hidden from the student.";
            return RedirectToAction("ReportCards");
        }

        [Authorize(Roles = "Admin,Principal")]
        public ActionResult ReportCards()
        {
            var model = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .Include(r => r.ClassTeacher)
                .Include(r => r.Subjects.Select(s => s.Subject))
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.Term)
                .ThenBy(r => r.Class.FullName)
                .ThenBy(r => r.Student.LastName)
                .ToList()
                .Select(ToReportCardViewModel)
                .ToList();

            return View(model);
        }

        [Authorize(Roles = "Principal")]
        public ActionResult PromotionRules(int? gradeId)
        {
            var selectedGradeId = gradeId ?? _context.Grades.OrderBy(g => g.Level).Select(g => (int?)g.Id).FirstOrDefault();
            var rule = _context.PromotionRules
                .Include(r => r.RequiredSubjects)
                .Where(r => r.IsActive && r.GradeId == selectedGradeId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault() ?? new PromotionRule { GradeId = selectedGradeId };

            var model = new PromotionRuleViewModel
            {
                Id = rule.Id,
                Name = rule.Name,
                GradeId = rule.GradeId,
                PromotionMinimumAverage = rule.PromotionMinimumAverage,
                ProgressionMinimumAverage = rule.ProgressionMinimumAverage,
                MaximumFailedSubjectsForPromotion = rule.MaximumFailedSubjectsForPromotion,
                MaximumFailedSubjectsForProgression = rule.MaximumFailedSubjectsForProgression,
                RequiredSubjectIds = rule.RequiredSubjects?.Select(s => s.SubjectId).ToList() ?? new List<int>(),
                IsActive = rule.IsActive
            };

            PopulatePromotionRuleOptions(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Principal")]
        public ActionResult PromotionRules(PromotionRuleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PopulatePromotionRuleOptions(model);
                return View(model);
            }

            var existingRules = _context.PromotionRules
                .Where(r => r.IsActive && r.GradeId == model.GradeId)
                .ToList();
            foreach (var existingRule in existingRules)
            {
                existingRule.IsActive = false;
            }

            var rule = new PromotionRule
            {
                Name = model.Name,
                GradeId = model.GradeId,
                PromotionMinimumAverage = model.PromotionMinimumAverage,
                ProgressionMinimumAverage = model.ProgressionMinimumAverage,
                MaximumFailedSubjectsForPromotion = model.MaximumFailedSubjectsForPromotion,
                MaximumFailedSubjectsForProgression = model.MaximumFailedSubjectsForProgression,
                IsActive = true,
                RequiredSubjects = (model.RequiredSubjectIds ?? new List<int>())
                    .Select(subjectId => new PromotionRuleRequiredSubject { SubjectId = subjectId })
                    .ToList()
            };

            _context.PromotionRules.Add(rule);
            _context.SaveChanges();

            RegenerateAllReportPromotionDecisions();
            TempData["SuccessMessage"] = "Promotion rules saved and report decisions updated.";
            return RedirectToAction("PromotionRules");
        }

        [Authorize(Roles = "Admin,Principal,Student")]
        public ActionResult PrintReportCard(int id)
        {
            var report = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .Include(r => r.ClassTeacher)
                .Include(r => r.Subjects.Select(s => s.Subject))
                .FirstOrDefault(r => r.Id == id);

            if (report == null) return HttpNotFound();

            if (User.IsInRole("Student"))
            {
                var student = GetCurrentStudent();
                if (student == null || report.StudentId != student.Id) return new HttpUnauthorizedResult();
                if (!report.IsPublished) return new HttpUnauthorizedResult();
            }

            return View(ToReportCardViewModel(report));
        }

        [Authorize(Roles = "Student")]
        public ActionResult MyReportCards()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            // Students only ever see published reports.
            var model = _context.StudentReportCards
                .Include(r => r.Student)
                .Include(r => r.Class)
                .Include(r => r.ClassTeacher)
                .Include(r => r.Subjects.Select(s => s.Subject))
                .Where(r => r.StudentId == student.Id && r.IsPublished)
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.Term)
                .ToList()
                .Select(ToReportCardViewModel)
                .ToList();

            return View(model);
        }

        // The teacher who created the assessment can capture marks. The class teacher of
        // the assessment's class is also allowed as a fallback (Option A) — useful when
        // the subject teacher is absent.
        private Assessment GetAssessmentForTeacher(int assessmentId, int teacherId)
        {
            return _context.Assessments
                .Include(a => a.Class)
                .Include(a => a.Subject)
                .Include(a => a.Teacher)
                .Include(a => a.Marks)
                .FirstOrDefault(a => a.Id == assessmentId
                    && (a.TeacherId == teacherId
                        || (a.Class != null && a.Class.ClassTeacherId == teacherId)));
        }

        private MarkCaptureViewModel BuildCaptureModel(Assessment assessment, int currentTeacherId)
        {
            var students = _context.Students
                .Where(s => s.ClassId == assessment.ClassId && s.IsActive)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList();

            return new MarkCaptureViewModel
            {
                AssessmentId = assessment.Id,
                AssessmentName = assessment.Name,
                Term = assessment.Term,
                AcademicYear = assessment.AcademicYear,
                ClassName = assessment.Class?.FullName,
                SubjectName = assessment.Subject?.Name,
                AssessmentType = assessment.AssessmentType.ToString(),
                MaxMark = assessment.MaxMark,
                Status = assessment.Status,
                OwningTeacherName = assessment.Teacher?.FullName,
                IsActingAsClassTeacher = assessment.TeacherId != currentTeacherId,
                Marks = students.Select(s =>
                {
                    var mark = assessment.Marks.FirstOrDefault(m => m.StudentId == s.Id);
                    return new StudentMarkEntryViewModel
                    {
                        StudentId = s.Id,
                        StudentName = s.FullName,
                        Mark = mark?.Mark,
                        Comment = mark?.Comment
                    };
                }).ToList()
            };
        }

        private void GenerateReportCards(int classId, string term, int academicYear)
        {
            var schoolClass = _context.Classes.FirstOrDefault(c => c.Id == classId);
            var promotionRule = GetPromotionRule(schoolClass?.GradeId);
            var students = _context.Students
                .Where(s => s.ClassId == classId && s.IsActive)
                .ToList();

            var assessments = _context.Assessments
                .Include(a => a.Marks)
                .Where(a => a.ClassId == classId
                    && a.Term == term
                    && a.AcademicYear == academicYear
                    && a.Status == MarkApprovalStatus.Approved)
                .ToList();

            var subjectIds = assessments.Select(a => a.SubjectId).Distinct().ToList();

            foreach (var student in students)
            {
                var report = _context.StudentReportCards.FirstOrDefault(r =>
                    r.StudentId == student.Id &&
                    r.ClassId == classId &&
                    r.Term == term &&
                    r.AcademicYear == academicYear);

                if (report == null)
                {
                    report = new StudentReportCard
                    {
                        StudentId = student.Id,
                        ClassId = classId,
                        Term = term,
                        AcademicYear = academicYear,
                        // Seed the class teacher from the class so the homeroom teacher
                        // shows up immediately on the pending-reports list.
                        ClassTeacherId = schoolClass?.ClassTeacherId
                    };
                    _context.StudentReportCards.Add(report);
                    _context.SaveChanges();
                }
                else if (!report.ClassTeacherId.HasValue && schoolClass?.ClassTeacherId.HasValue == true)
                {
                    report.ClassTeacherId = schoolClass.ClassTeacherId;
                }

                foreach (var subjectId in subjectIds)
                {
                    var subjectAssessments = assessments.Where(a => a.SubjectId == subjectId).ToList();
                    var termMark = CalculateCategoryMark(subjectAssessments, student.Id, false) ?? 0;
                    var examMark = CalculateCategoryMark(subjectAssessments, student.Id, true) ?? 0;
                    var finalMark = CalculateFinalMark(subjectAssessments, student.Id) ?? 0;
                    var previousMark = GetPreviousSubjectFinalMark(student.Id, classId, subjectId, term, academicYear);
                    var subjectStats = CalculateSubjectStats(subjectAssessments, students.Select(s => s.Id).ToList());

                    var subjectReport = _context.StudentReportCardSubjects.FirstOrDefault(s =>
                        s.StudentReportCardId == report.Id &&
                        s.SubjectId == subjectId);

                    if (subjectReport == null)
                    {
                        subjectReport = new StudentReportCardSubject
                        {
                            StudentReportCardId = report.Id,
                            SubjectId = subjectId
                        };
                        _context.StudentReportCardSubjects.Add(subjectReport);
                    }

                    subjectReport.TermMark = Math.Round(termMark, 1);
                    subjectReport.ExamMark = Math.Round(examMark, 1);
                    subjectReport.FinalMark = Math.Round(finalMark, 1);
                    subjectReport.PassFailStatus = finalMark >= 50 ? "Pass" : "Fail";
                    subjectReport.ClassAverage = Math.Round(subjectStats.Average, 1);
                    subjectReport.HighestMark = Math.Round(subjectStats.Highest, 1);
                    subjectReport.LowestMark = Math.Round(subjectStats.Lowest, 1);
                    subjectReport.PerformanceTrend = GetTrend(finalMark, previousMark);
                    subjectReport.ImprovementComment = GetImprovementComment(finalMark, previousMark);
                }

                var studentSubjectFinals = subjectIds
                    .Select(subjectId => CalculateFinalMark(assessments.Where(a => a.SubjectId == subjectId).ToList(), student.Id))
                    .Where(mark => mark.HasValue)
                    .Select(mark => mark.Value)
                    .ToList();

                var overallFinal = studentSubjectFinals.Any() ? studentSubjectFinals.Average() : 0;
                var previousOverall = GetPreviousFinalMark(student.Id, classId, term, academicYear);
                var failedSubjectCount = studentSubjectFinals.Count(mark => mark < 50);
                var failedRequiredSubject = HasFailedRequiredSubject(report.Id, promotionRule);

                report.TermMark = Math.Round(subjectIds
                    .Select(subjectId => CalculateCategoryMark(assessments.Where(a => a.SubjectId == subjectId).ToList(), student.Id, false))
                    .Where(mark => mark.HasValue)
                    .Select(mark => mark.Value)
                    .DefaultIfEmpty(0)
                    .Average(), 1);
                report.ExamMark = Math.Round(subjectIds
                    .Select(subjectId => CalculateCategoryMark(assessments.Where(a => a.SubjectId == subjectId).ToList(), student.Id, true))
                    .Where(mark => mark.HasValue)
                    .Select(mark => mark.Value)
                    .DefaultIfEmpty(0)
                    .Average(), 1);
                report.FinalMark = Math.Round(overallFinal, 1);
                report.PassFailStatus = overallFinal >= 50 ? "Pass" : "Fail";
                report.ClassAverage = Math.Round(CalculateClassOverallAverage(assessments, students.Select(s => s.Id).ToList()), 1);
                report.HighestMark = Math.Round(CalculateClassOverallHighest(assessments, students.Select(s => s.Id).ToList()), 1);
                report.LowestMark = Math.Round(CalculateClassOverallLowest(assessments, students.Select(s => s.Id).ToList()), 1);
                report.PerformanceTrend = GetTrend(overallFinal, previousOverall);
                report.PromotionDecision = DeterminePromotionDecision(overallFinal, failedSubjectCount, failedRequiredSubject, promotionRule);
                report.ImprovementComment = GetImprovementComment(overallFinal, previousOverall);
                report.GeneratedAt = DateTime.Now;
            }

            _context.SaveChanges();
        }

        private (decimal Average, decimal Highest, decimal Lowest) CalculateSubjectStats(List<Assessment> assessments, List<int> studentIds)
        {
            var marks = studentIds
                .Select(studentId => CalculateFinalMark(assessments, studentId))
                .Where(mark => mark.HasValue)
                .Select(mark => mark.Value)
                .ToList();

            if (!marks.Any()) return (0, 0, 0);
            return (marks.Average(), marks.Max(), marks.Min());
        }

        private decimal CalculateClassOverallAverage(List<Assessment> assessments, List<int> studentIds)
        {
            var marks = CalculateClassOverallMarks(assessments, studentIds);
            return marks.Any() ? marks.Average() : 0;
        }

        private decimal CalculateClassOverallHighest(List<Assessment> assessments, List<int> studentIds)
        {
            var marks = CalculateClassOverallMarks(assessments, studentIds);
            return marks.Any() ? marks.Max() : 0;
        }

        private decimal CalculateClassOverallLowest(List<Assessment> assessments, List<int> studentIds)
        {
            var marks = CalculateClassOverallMarks(assessments, studentIds);
            return marks.Any() ? marks.Min() : 0;
        }

        private List<decimal> CalculateClassOverallMarks(List<Assessment> assessments, List<int> studentIds)
        {
            var subjectIds = assessments.Select(a => a.SubjectId).Distinct().ToList();
            return studentIds
                .Select(studentId =>
                {
                    var marks = subjectIds
                        .Select(subjectId => CalculateFinalMark(assessments.Where(a => a.SubjectId == subjectId).ToList(), studentId))
                        .Where(mark => mark.HasValue)
                        .Select(mark => mark.Value)
                        .ToList();

                    return marks.Any() ? (decimal?)marks.Average() : null;
                })
                .Where(mark => mark.HasValue)
                .Select(mark => mark.Value)
                .ToList();
        }

        private decimal? CalculateFinalMark(List<Assessment> assessments, int studentId)
        {
            var termMark = CalculateCategoryMark(assessments, studentId, false);
            var examMark = CalculateCategoryMark(assessments, studentId, true);

            if (termMark.HasValue && examMark.HasValue) return (termMark.Value * 0.4m) + (examMark.Value * 0.6m);
            if (termMark.HasValue) return termMark.Value;
            if (examMark.HasValue) return examMark.Value;
            return null;
        }

        private decimal? CalculateCategoryMark(List<Assessment> assessments, int studentId, bool examOnly)
        {
            var entries = assessments
                .Where(a => examOnly ? a.AssessmentType == AssessmentType.Exam : a.AssessmentType != AssessmentType.Exam)
                .Select(a => new
                {
                    Assessment = a,
                    Mark = a.Marks.FirstOrDefault(m => m.StudentId == studentId && m.Mark.HasValue)
                })
                .Where(x => x.Mark != null)
                .ToList();

            if (!entries.Any()) return null;

            var totalWeight = entries.Sum(x => x.Assessment.Weight <= 0 ? 1 : x.Assessment.Weight);
            return entries.Sum(x => ((x.Mark.Mark.Value / x.Assessment.MaxMark) * 100) * (x.Assessment.Weight <= 0 ? 1 : x.Assessment.Weight)) / totalWeight;
        }

        private decimal? GetPreviousFinalMark(int studentId, int classId, string term, int academicYear)
        {
            var terms = new[] { "Term 1", "Term 2", "Term 3", "Term 4" };
            var currentIndex = Array.IndexOf(terms, term);
            var previousTerm = currentIndex > 0 ? terms[currentIndex - 1] : "Term 4";
            var previousYear = currentIndex > 0 ? academicYear : academicYear - 1;

            var report = _context.StudentReportCards.FirstOrDefault(r =>
                r.StudentId == studentId &&
                r.ClassId == classId &&
                r.Term == previousTerm &&
                r.AcademicYear == previousYear);

            return report?.FinalMark;
        }

        private decimal? GetPreviousSubjectFinalMark(int studentId, int classId, int subjectId, string term, int academicYear)
        {
            var terms = new[] { "Term 1", "Term 2", "Term 3", "Term 4" };
            var currentIndex = Array.IndexOf(terms, term);
            var previousTerm = currentIndex > 0 ? terms[currentIndex - 1] : "Term 4";
            var previousYear = currentIndex > 0 ? academicYear : academicYear - 1;

            var report = _context.StudentReportCards
                .Include(r => r.Subjects)
                .FirstOrDefault(r =>
                    r.StudentId == studentId &&
                    r.ClassId == classId &&
                    r.Term == previousTerm &&
                    r.AcademicYear == previousYear);

            return report?.Subjects.FirstOrDefault(s => s.SubjectId == subjectId)?.FinalMark;
        }

        private PromotionRule GetPromotionRule(int? gradeId)
        {
            return _context.PromotionRules
                .Include(r => r.RequiredSubjects)
                .Where(r => r.IsActive && (r.GradeId == gradeId || r.GradeId == null))
                .OrderByDescending(r => r.GradeId.HasValue)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefault() ?? new PromotionRule();
        }

        private bool HasFailedRequiredSubject(int reportId, PromotionRule rule)
        {
            var requiredSubjectIds = (rule.RequiredSubjects ?? new List<PromotionRuleRequiredSubject>())
                .Select(s => s.SubjectId)
                .ToList();

            if (!requiredSubjectIds.Any()) return false;

            return _context.StudentReportCardSubjects.Any(s =>
                s.StudentReportCardId == reportId &&
                requiredSubjectIds.Contains(s.SubjectId) &&
                s.FinalMark < 50);
        }

        private PromotionDecision DeterminePromotionDecision(decimal finalMark, int failedSubjectCount, bool failedRequiredSubject, PromotionRule rule)
        {
            if (failedRequiredSubject)
            {
                return PromotionDecision.NotPromoted;
            }

            if (finalMark >= rule.PromotionMinimumAverage &&
                failedSubjectCount <= rule.MaximumFailedSubjectsForPromotion)
            {
                return PromotionDecision.Promoted;
            }

            if (finalMark >= rule.ProgressionMinimumAverage &&
                failedSubjectCount <= rule.MaximumFailedSubjectsForProgression)
            {
                return PromotionDecision.Progressed;
            }

            return PromotionDecision.NotPromoted;
        }

        private void RegenerateAllReportPromotionDecisions()
        {
            var reports = _context.StudentReportCards
                .Include(r => r.Class)
                .Include(r => r.Subjects)
                .ToList();

            foreach (var report in reports)
            {
                var rule = GetPromotionRule(report.Class?.GradeId);
                var failedSubjects = report.Subjects.Count(s => s.FinalMark < 50);
                var failedRequiredSubject = report.Subjects.Any(s =>
                    rule.RequiredSubjects.Any(r => r.SubjectId == s.SubjectId) &&
                    s.FinalMark < 50);
                report.PromotionDecision = DeterminePromotionDecision(report.FinalMark, failedSubjects, failedRequiredSubject, rule);
            }

            _context.SaveChanges();
        }

        private string GetTrend(decimal current, decimal? previous)
        {
            if (!previous.HasValue) return "New report";
            if (current > previous.Value + 2) return "Improving";
            if (current < previous.Value - 2) return "Dropping";
            return "Stable";
        }

        private string GetImprovementComment(decimal current, decimal? previous)
        {
            if (!previous.HasValue) return "First approved report for this learner in the system.";
            if (current > previous.Value + 2) return $"Improved by {Math.Round(current - previous.Value, 1)}%. Keep building on this progress.";
            if (current < previous.Value - 2) return $"Dropped by {Math.Round(previous.Value - current, 1)}%. Extra support is recommended.";
            return "Performance is stable compared with the previous report.";
        }

        private ReportCardViewModel ToReportCardViewModel(StudentReportCard report)
        {
            return new ReportCardViewModel
            {
                Id = report.Id,
                StudentName = report.Student?.FullName,
                ClassName = report.Class?.FullName,
                Term = report.Term,
                AcademicYear = report.AcademicYear,
                TermMark = report.TermMark,
                ExamMark = report.ExamMark,
                FinalMark = report.FinalMark,
                PassFailStatus = report.PassFailStatus,
                ClassAverage = report.ClassAverage,
                HighestMark = report.HighestMark,
                LowestMark = report.LowestMark,
                PerformanceTrend = report.PerformanceTrend,
                PromotionDecision = report.PromotionDecision,
                ImprovementComment = report.ImprovementComment,
                GeneratedAt = report.GeneratedAt,
                ClassTeacherComment = report.ClassTeacherComment,
                ClassTeacherName = report.ClassTeacher?.FullName,
                ClassTeacherCommentedAt = report.ClassTeacherCommentedAt,
                IsPublished = report.IsPublished,
                PublishedAt = report.PublishedAt,
                Subjects = report.Subjects
                    .OrderBy(s => s.Subject.Name)
                    .Select(s => new ReportCardSubjectViewModel
                    {
                        SubjectName = s.Subject?.Name,
                        TermMark = s.TermMark,
                        ExamMark = s.ExamMark,
                        FinalMark = s.FinalMark,
                        PassFailStatus = s.PassFailStatus,
                        ClassAverage = s.ClassAverage,
                        HighestMark = s.HighestMark,
                        LowestMark = s.LowestMark,
                        PerformanceTrend = s.PerformanceTrend,
                        ImprovementComment = s.ImprovementComment
                    })
                    .ToList()
            };
        }

        private ClassTeacherReportCommentViewModel ToCommentViewModel(StudentReportCard report)
        {
            return new ClassTeacherReportCommentViewModel
            {
                ReportId = report.Id,
                StudentName = report.Student?.FullName,
                ClassName = report.Class?.FullName,
                Term = report.Term,
                AcademicYear = report.AcademicYear,
                FinalMark = report.FinalMark,
                PassFailStatus = report.PassFailStatus,
                PromotionDecision = report.PromotionDecision,
                IsPublished = report.IsPublished,
                ClassTeacherComment = report.ClassTeacherComment
            };
        }

        private void PopulatePromotionRuleOptions(PromotionRuleViewModel model)
        {
            var options = _context.Grades
                .OrderBy(g => g.Level)
                .ToList()
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name,
                    Selected = model.GradeId == g.Id
                })
                .ToList();

            model.GradeOptions = options;
            model.SubjectOptions = _context.Subjects
                .OrderBy(s => s.Name)
                .ToList()
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                });
        }

        private void PopulateCreateOptions(CreateAssessmentViewModel model, int teacherId)
        {
            model.AssignmentOptions = _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Include(a => a.Subject)
                .Where(a => a.TeacherId == teacherId && a.IsActive && a.SubjectId > 0)
                .OrderBy(a => a.Class.FullName)
                .ThenBy(a => a.Subject.Name)
                .ToList()
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Class.FullName} - {a.Subject.Name}"
                });

            model.TermOptions = new[]
            {
                new SelectListItem { Value = "Term 1", Text = "Term 1" },
                new SelectListItem { Value = "Term 2", Text = "Term 2" },
                new SelectListItem { Value = "Term 3", Text = "Term 3" },
                new SelectListItem { Value = "Term 4", Text = "Term 4" }
            };

            model.AssessmentTypeOptions = Enum.GetValues(typeof(AssessmentType))
                .Cast<AssessmentType>()
                .Select(t => new SelectListItem { Value = t.ToString(), Text = t.ToString() });
        }

        private string GetCurrentTerm()
        {
            var month = DateTime.Now.Month;
            if (month <= 3) return "Term 1";
            if (month <= 6) return "Term 2";
            if (month <= 9) return "Term 3";
            return "Term 4";
        }

        private Teacher GetCurrentTeacher()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);
            if (user == null) return null;

            return _context.Teachers.FirstOrDefault(t => t.UserId == user.Id);
        }

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students.FirstOrDefault(s => s.UserId == user.Id);
        }
    }
}
