using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private IExamTimetableService _examTimetableService;

        public TeacherController()
        {
            _context = new ElevateEDContext();
            _examTimetableService = new ExamTimetableService();

        }

        // ============================================
        // DASHBOARD
        // ============================================

        public ActionResult Dashboard()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!user.HasChangedPassword)
            {
                return RedirectToAction("ChangePassword", "Account", new { firstLogin = true });
            }

            var teacher = _context.Teachers
                .Include(t => t.SubjectQualifications)
                .Include(t => t.GradeAssignments)
                .Include(t => t.SubjectAssignments)
                .FirstOrDefault(t => t.UserId == user.Id);

            var viewModel = new TeacherDashboardViewModel
            {
                StaffNumber = staffNumber,
                FullName = teacher?.FullName ?? "Teacher",
                Email = user.Email,
                PhoneNumber = teacher?.PhoneNumber ?? "",
                Qualification = teacher?.Qualification ?? "",
                YearsOfExperience = teacher?.YearsOfExperience ?? 0,
                TotalStudents = CalculateTotalStudents(teacher?.Id ?? 0),
                TotalClasses = CalculateTotalClasses(teacher?.Id ?? 0),
                DraftAssessments = CalculateAssessmentCount(teacher?.Id ?? 0, MarkApprovalStatus.Draft),
                SubmittedAssessments = CalculateAssessmentCount(teacher?.Id ?? 0, MarkApprovalStatus.Submitted),
                ApprovedAssessments = CalculateAssessmentCount(teacher?.Id ?? 0, MarkApprovalStatus.Approved),
                ReturnedAssessments = CalculateAssessmentCount(teacher?.Id ?? 0, MarkApprovalStatus.Rejected),
                PendingExamInputs = CalculatePendingExamInputs(teacher?.Id ?? 0),
                UpcomingExamSessions = CalculateUpcomingExamSessions(teacher?.Id ?? 0),
                AverageLearnerMark = CalculateAverageLearnerMark(teacher?.Id ?? 0),
                LearnerPassRate = CalculateLearnerPassRate(teacher?.Id ?? 0),
                RecentAnnouncements = GetRecentAnnouncements(),
                TodaySchedule = GetSampleSchedule(),
                SubjectPerformance = GetSubjectPerformance(teacher?.Id ?? 0)
            };
            viewModel.PendingTasks = viewModel.DraftAssessments + viewModel.ReturnedAssessments + viewModel.PendingExamInputs;
            viewModel.UnreadMessages = viewModel.SubmittedAssessments;

            return View(viewModel);
        }

        private int CalculateAssessmentCount(int teacherId, MarkApprovalStatus status)
        {
            if (teacherId == 0) return 0;
            try
            {
                return _context.Assessments.Count(a => a.TeacherId == teacherId && a.Status == status);
            }
            catch (EntityCommandExecutionException)
            {
                return 0;
            }
        }

        private int CalculatePendingExamInputs(int teacherId)
        {
            if (teacherId == 0) return 0;
            try
            {
                return _context.TeacherExamNotifications.Count(n => n.TeacherId == teacherId && !n.IsSubmitted);
            }
            catch (EntityCommandExecutionException)
            {
                return 0;
            }
        }

        private int CalculateUpcomingExamSessions(int teacherId)
        {
            if (teacherId == 0) return 0;

            try
            {
                var assignments = _context.TeacherSubjectAssignments
                    .Where(a => a.TeacherId == teacherId && a.IsActive && a.SubjectId > 0)
                    .Select(a => new { a.SubjectId, a.ClassId })
                    .ToList();

                var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
                var classIds = assignments.Select(a => a.ClassId).Distinct().ToList();

                return _context.ExamSessions
                    .Include(e => e.ExamSessionClasses)
                    .Count(e => e.IsActive
                        && e.ExamDate >= DateTime.Today
                        && (e.CreatedByTeacherId == teacherId
                            || (subjectIds.Contains(e.SubjectId)
                                && (!e.ExamSessionClasses.Any()
                                    || e.ExamSessionClasses.Any(c => classIds.Contains(c.ClassId))))));
            }
            catch (EntityCommandExecutionException)
            {
                return 0;
            }
        }

        private decimal CalculateAverageLearnerMark(int teacherId)
        {
            var marks = GetTeacherMarkPercentages(teacherId);
            return marks.Any() ? Math.Round(marks.Average(), 1) : 0;
        }

        private decimal CalculateLearnerPassRate(int teacherId)
        {
            var marks = GetTeacherMarkPercentages(teacherId);
            return marks.Any() ? Math.Round((decimal)marks.Count(m => m >= 50) * 100 / marks.Count, 1) : 0;
        }

        private List<decimal> GetTeacherMarkPercentages(int teacherId)
        {
            if (teacherId == 0) return new List<decimal>();

            try
            {
                return _context.Assessments
                    .Include(a => a.Marks)
                    .Where(a => a.TeacherId == teacherId
                        && a.Status == MarkApprovalStatus.Approved
                        && a.MaxMark > 0)
                    .ToList()
                    .SelectMany(a => a.Marks
                        .Where(m => m.Mark.HasValue)
                        .Select(m => (m.Mark.Value / a.MaxMark) * 100))
                    .ToList();
            }
            catch (EntityCommandExecutionException)
            {
                return new List<decimal>();
            }
        }

        private List<TeacherSubjectPerformanceViewModel> GetSubjectPerformance(int teacherId)
        {
            if (teacherId == 0) return new List<TeacherSubjectPerformanceViewModel>();

            try
            {
                return _context.Assessments
                    .Include(a => a.Subject)
                    .Include(a => a.Class)
                    .Include(a => a.Marks)
                    .Where(a => a.TeacherId == teacherId && a.Status == MarkApprovalStatus.Approved && a.MaxMark > 0)
                    .ToList()
                    .GroupBy(a => new { Subject = a.Subject?.Name, ClassName = a.Class?.FullName })
                    .Select(g =>
                    {
                        var marks = g.SelectMany(a => a.Marks
                            .Where(m => m.Mark.HasValue)
                            .Select(m => (m.Mark.Value / a.MaxMark) * 100))
                            .ToList();

                        return new TeacherSubjectPerformanceViewModel
                        {
                            SubjectName = g.Key.Subject,
                            ClassName = g.Key.ClassName,
                            AssessmentCount = g.Count(),
                            AverageMark = marks.Any() ? Math.Round(marks.Average(), 1) : 0,
                            PassRate = marks.Any() ? Math.Round((decimal)marks.Count(m => m >= 50) * 100 / marks.Count, 1) : 0
                        };
                    })
                    .OrderBy(x => x.AverageMark)
                    .ToList();
            }
            catch (EntityCommandExecutionException)
            {
                return new List<TeacherSubjectPerformanceViewModel>();
            }
        }

        private int CalculateTotalStudents(int teacherId)
        {
            if (teacherId == 0) return 0;

            var classIds = _context.TeacherSubjectAssignments
                .Where(a => a.TeacherId == teacherId && a.IsActive)
                .Select(a => a.ClassId)
                .Distinct()
                .ToList();

            return _context.Students
                .Count(s => classIds.Contains(s.ClassId.Value) && s.IsActive);
        }

        private int CalculateTotalClasses(int teacherId)
        {
            if (teacherId == 0) return 0;

            return _context.TeacherSubjectAssignments
                .Where(a => a.TeacherId == teacherId && a.IsActive)
                .Select(a => a.ClassId)
                .Distinct()
                .Count();
        }

        private List<AnnouncementViewModel> GetRecentAnnouncements()
        {
            var currentDate = DateTime.Now;

            return _context.Announcements
                .Where(a => a.IsActive &&
                    (a.TargetAudience == "All Users" || a.TargetAudience == "Teachers Only") &&
                    (!a.ExpiryDate.HasValue || a.ExpiryDate >= currentDate))
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToList()
                .Select(a => new AnnouncementViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Summary = a.Content.Length > 100 ? a.Content.Substring(0, 100) + "..." : a.Content,
                    Type = a.AnnouncementType,
                    PublishedBy = "Admin",
                    PublishedAt = a.CreatedAt
                })
                .ToList();
        }

        private List<TimetableViewModel> GetSampleSchedule()
        {
            return new List<TimetableViewModel>
            {
                new TimetableViewModel
                {
                    Id = 1,
                    SubjectName = "Mathematics",
                    GradeName = "Grade 10",
                    ClassStream = "A",
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(9, 0, 0),
                    Venue = "Room 101",
                    Day = DateTime.Now.DayOfWeek
                }
            };
        }

        // ============================================
        // HOMEWORK MANAGEMENT
        // ============================================

        public ActionResult Homework()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = teacher.SubjectQualifications
                .Select(sq => sq.Subject)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            return View(subjects);
        }

        public ActionResult SelectGradeForHomework(int subjectId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            if (subject == null) return RedirectToAction("Homework");

            var grades = _context.TeacherSubjectAssignments
                .Include(a => a.Class.Grade)
                .Where(a => a.TeacherId == teacher.Id && a.SubjectId == subjectId && a.IsActive)
                .Select(a => a.Class.Grade)
                .Distinct()
                .OrderBy(g => g.Level)
                .ToList();

            ViewBag.Subject = subject;
            ViewBag.TeacherName = teacher.FullName;
            return View(grades);
        }

        public ActionResult SelectClassForHomework(int subjectId, int gradeId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            var grade = _context.Grades.Find(gradeId);
            if (subject == null || grade == null) return RedirectToAction("Homework");

            var classes = _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Where(a => a.TeacherId == teacher.Id
                    && a.SubjectId == subjectId
                    && a.Class.GradeId == gradeId
                    && a.IsActive)
                .Select(a => a.Class)
                .Distinct()
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Subject = subject;
            ViewBag.Grade = grade;
            ViewBag.TeacherName = teacher.FullName;
            return View(classes);
        }

        public ActionResult UploadHomework(int subjectId, int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            var classEntity = _context.Classes.Include(c => c.Grade).FirstOrDefault(c => c.Id == classId);

            if (subject == null || classEntity == null) return RedirectToAction("Homework");

            ViewBag.Subject = subject;
            ViewBag.Class = classEntity;
            ViewBag.TeacherName = teacher.FullName;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadHomework(UploadHomeworkViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                var subject = _context.Subjects.Find(model.SubjectId);
                var classEntity = _context.Classes.Include(c => c.Grade).FirstOrDefault(c => c.Id == model.ClassId);

                string uploadPath = Server.MapPath("~/Uploads/Homework/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                string fileName = null;
                if (model.File != null && model.File.ContentLength > 0)
                {
                    if (model.File.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.File.FileName);
                    fileName = $"Homework_{subject?.Code}_{classEntity?.FullName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.File.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                var homework = new Homework
                {
                    Title = model.Title,
                    Instructions = model.Instructions,
                    SubjectId = model.SubjectId,
                    ClassId = model.ClassId,
                    SubjectName = subject?.Name,
                    GradeName = classEntity?.Grade?.Name,
                    ClassNameValue = classEntity?.FullName,
                    FilePath = "/Uploads/Homework/" + fileName,
                    FileName = model.File.FileName,
                    FileType = model.File.ContentType,
                    FileSize = model.File.ContentLength,
                    DueDate = model.DueDate,
                    UploadedBy = teacher.Id,
                    UploadedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Homeworks.Add(homework);
                _context.SaveChanges();

                return Json(new { success = true, message = "Homework uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewMyHomework()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var homework = _context.Homeworks
                .Include(h => h.Subject)
                .Include(h => h.Class)
                .Where(h => h.UploadedBy == teacher.Id && h.IsActive)
                .OrderByDescending(h => h.UploadedAt)
                .ToList()
                .Select(h => new HomeworkListViewModel
                {
                    Id = h.Id,
                    Title = h.Title,
                    Instructions = h.Instructions,
                    Subject = h.SubjectDisplay,
                    Grade = h.GradeDisplay,
                    ClassName = h.ClassNameDisplay,
                    FilePath = h.FilePath,
                    FileName = h.FileName,
                    FileType = h.FileType,
                    FileSize = h.FileSize,
                    FileSizeFormatted = FormatFileSize(h.FileSize),
                    DueDate = h.DueDate,
                    DueDateFormatted = h.DueDate.ToString("dd MMM yyyy, hh:mm tt"),
                    UploadedByName = teacher.FullName,
                    UploadedAt = h.UploadedAt
                })
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            return View(homework);
        }

        public ActionResult DownloadHomework(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var homework = _context.Homeworks.Find(id);
            if (homework == null || homework.UploadedBy != teacher.Id)
                return HttpNotFound();

            string filePath = Server.MapPath(homework.FilePath);
            if (!System.IO.File.Exists(filePath))
                return HttpNotFound();

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, homework.FileType, homework.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteHomework(int id)
        {
            try
            {
                var teacher = GetCurrentTeacher();
                if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

                var homework = _context.Homeworks.Find(id);
                if (homework == null) return Json(new { success = false, message = "Homework not found" });
                if (homework.UploadedBy != teacher.Id) return Json(new { success = false, message = "You can only delete your own homework" });

                homework.IsActive = false;
                _context.SaveChanges();

                return Json(new { success = true, message = "Homework deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // CLASSWORK MANAGEMENT
        // ============================================

        public ActionResult Classwork()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = teacher.SubjectQualifications
                .Select(sq => sq.Subject)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            return View(subjects);
        }

        public ActionResult SelectGradeForClasswork(int subjectId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            if (subject == null) return RedirectToAction("Classwork");

            var grades = _context.TeacherSubjectAssignments
                .Include(a => a.Class.Grade)
                .Where(a => a.TeacherId == teacher.Id && a.SubjectId == subjectId && a.IsActive)
                .Select(a => a.Class.Grade)
                .Distinct()
                .OrderBy(g => g.Level)
                .ToList();

            ViewBag.Subject = subject;
            ViewBag.TeacherName = teacher.FullName;
            return View(grades);
        }

        public ActionResult SelectClassForClasswork(int subjectId, int gradeId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            var grade = _context.Grades.Find(gradeId);
            if (subject == null || grade == null) return RedirectToAction("Classwork");

            var classes = _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Where(a => a.TeacherId == teacher.Id
                    && a.SubjectId == subjectId
                    && a.Class.GradeId == gradeId
                    && a.IsActive)
                .Select(a => a.Class)
                .Distinct()
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Subject = subject;
            ViewBag.Grade = grade;
            ViewBag.TeacherName = teacher.FullName;
            return View(classes);
        }

        public ActionResult UploadClasswork(int subjectId, int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            var classEntity = _context.Classes.Include(c => c.Grade).FirstOrDefault(c => c.Id == classId);

            if (subject == null || classEntity == null) return RedirectToAction("Classwork");

            ViewBag.Subject = subject;
            ViewBag.Class = classEntity;
            ViewBag.TeacherName = teacher.FullName;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadClasswork(UploadClassworkViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                var subject = _context.Subjects.Find(model.SubjectId);
                var classEntity = _context.Classes.Include(c => c.Grade).FirstOrDefault(c => c.Id == model.ClassId);

                string uploadPath = Server.MapPath("~/Uploads/Classwork/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                string fileName = null;
                if (model.File != null && model.File.ContentLength > 0)
                {
                    if (model.File.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.File.FileName);
                    fileName = $"Classwork_{subject?.Code}_{classEntity?.FullName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.File.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                var classwork = new Classwork
                {
                    Title = model.Title,
                    Instructions = model.Instructions,
                    SubjectId = model.SubjectId,
                    ClassId = model.ClassId,
                    SubjectName = subject?.Name,
                    GradeName = classEntity?.Grade?.Name,
                    ClassNameValue = classEntity?.FullName,
                    FilePath = "/Uploads/Classwork/" + fileName,
                    FileName = model.File.FileName,
                    FileType = model.File.ContentType,
                    FileSize = model.File.ContentLength,
                    UploadedBy = teacher.Id,
                    UploadedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Classworks.Add(classwork);
                _context.SaveChanges();

                return Json(new { success = true, message = "Classwork uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewMyClasswork()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var classwork = _context.Classworks
                .Include(c => c.Subject)
                .Include(c => c.Class)
                .Where(c => c.UploadedBy == teacher.Id && c.IsActive)
                .OrderByDescending(c => c.UploadedAt)
                .ToList()
                .Select(c => new ClassworkListViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    Instructions = c.Instructions,
                    Subject = c.SubjectDisplay,
                    Grade = c.GradeDisplay,
                    ClassName = c.ClassNameDisplay,
                    FilePath = c.FilePath,
                    FileName = c.FileName,
                    FileType = c.FileType,
                    FileSize = c.FileSize,
                    FileSizeFormatted = FormatFileSize(c.FileSize),
                    UploadedByName = teacher.FullName,
                    UploadedAt = c.UploadedAt
                })
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            return View(classwork);
        }

        public ActionResult DownloadClasswork(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var classwork = _context.Classworks.Find(id);
            if (classwork == null || classwork.UploadedBy != teacher.Id)
                return HttpNotFound();

            string filePath = Server.MapPath(classwork.FilePath);
            if (!System.IO.File.Exists(filePath))
                return HttpNotFound();

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, classwork.FileType, classwork.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteClasswork(int id)
        {
            try
            {
                var teacher = GetCurrentTeacher();
                if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

                var classwork = _context.Classworks.Find(id);
                if (classwork == null) return Json(new { success = false, message = "Classwork not found" });
                if (classwork.UploadedBy != teacher.Id) return Json(new { success = false, message = "You can only delete your own classwork" });

                classwork.IsActive = false;
                _context.SaveChanges();

                return Json(new { success = true, message = "Classwork deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // PAST PAPERS
        // ============================================

        public ActionResult PastPapers()
        {
            var grades = _context.Grades.OrderBy(g => g.Level).ToList();
            var gradeCards = grades.Select(g => new GradeCardViewModel
            {
                Grade = g.Name,
                Icon = GetGradeIcon(g.Name),
                Color = GetGradeColor(g.Name),
                PaperCount = _context.PastPapers.Count(p => p.Grade == g.Name && p.IsPublished)
            }).ToList();

            return View(gradeCards);
        }

        public ActionResult UploadPastPaper()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = teacher.SubjectQualifications
                .Select(sq => sq.Subject)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Subjects = subjects;
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Years = GetYearsList();
            ViewBag.Terms = new List<string> { "Term 1", "Term 2", "Term 3", "Term 4" };
            ViewBag.ExamTypes = new List<string> { "Test", "Mid-Year", "Trial", "Final" };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadPastPaper(UploadPastPaperViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                string uploadPath = Server.MapPath("~/Uploads/PastPapers/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                string paperFileName = null;
                if (model.PaperFile != null && model.PaperFile.ContentLength > 0)
                {
                    if (model.PaperFile.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.PaperFile.FileName);
                    paperFileName = $"Paper_{DateTime.Now:yyyyMMddHHmmss}_{model.Subject}_{model.Grade}_{model.Year}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, paperFileName);
                    model.PaperFile.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                string memoFileName = null;
                if (model.MemoFile != null && model.MemoFile.ContentLength > 0)
                {
                    if (model.MemoFile.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "Memo file size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.MemoFile.FileName);
                    memoFileName = $"Memo_{DateTime.Now:yyyyMMddHHmmss}_{model.Subject}_{model.Grade}_{model.Year}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, memoFileName);
                    model.MemoFile.SaveAs(fullPath);
                }

                var pastPaper = new PastPaper
                {
                    Title = model.Title,
                    Subject = model.Subject,
                    Grade = model.Grade,
                    Year = model.Year,
                    Term = model.Term,
                    ExamType = model.ExamType,
                    Description = model.Description,
                    FilePath = "/Uploads/PastPapers/" + paperFileName,
                    MemoPath = string.IsNullOrEmpty(memoFileName) ? null : "/Uploads/PastPapers/" + memoFileName,
                    UploadedBy = teacher.Id,
                    UploadedAt = DateTime.Now,
                    IsPublished = true,
                    DownloadCount = 0
                };

                _context.PastPapers.Add(pastPaper);
                _context.SaveChanges();

                return Json(new { success = true, message = "Past paper uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewPastPapers(string grade = null, string subject = null, string year = null, string term = null)
        {
            if (string.IsNullOrEmpty(grade))
                return RedirectToAction("PastPapers");

            var query = _context.PastPapers.Where(p => p.Grade == grade && p.IsPublished);

            if (!string.IsNullOrEmpty(subject)) query = query.Where(p => p.Subject == subject);
            if (!string.IsNullOrEmpty(year)) query = query.Where(p => p.Year == year);
            if (!string.IsNullOrEmpty(term)) query = query.Where(p => p.Term == term);

            var pastPapers = query
                .OrderByDescending(p => p.Year)
                .ThenBy(p => p.Subject)
                .ToList()
                .Select(p => new PastPaperViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Subject = p.Subject,
                    Grade = p.Grade,
                    Year = p.Year,
                    Term = p.Term,
                    ExamType = p.ExamType,
                    Description = p.Description,
                    FilePath = p.FilePath,
                    MemoPath = p.MemoPath,
                    UploadedByName = _context.Teachers.Where(t => t.Id == p.UploadedBy).Select(t => t.FirstName + " " + t.LastName).FirstOrDefault(),
                    UploadedAt = p.UploadedAt,
                    DownloadCount = p.DownloadCount
                })
                .ToList();

            ViewBag.Subjects = _context.PastPapers.Where(p => p.Grade == grade).Select(p => p.Subject).Distinct().ToList();
            ViewBag.Years = _context.PastPapers.Where(p => p.Grade == grade).Select(p => p.Year).Distinct().OrderByDescending(y => y).ToList();
            ViewBag.Terms = new List<string> { "Term 1", "Term 2", "Term 3", "Term 4" };
            ViewBag.SelectedGrade = grade;
            ViewBag.SelectedSubject = subject;
            ViewBag.SelectedYear = year;
            ViewBag.SelectedTerm = term;

            return View(pastPapers);
        }

        public ActionResult DownloadPastPaper(int id)
        {
            var pastPaper = _context.PastPapers.Find(id);
            if (pastPaper == null) return HttpNotFound();

            pastPaper.DownloadCount++;
            _context.SaveChanges();

            string filePath = Server.MapPath(pastPaper.FilePath);
            if (!System.IO.File.Exists(filePath)) return HttpNotFound();

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/pdf", System.IO.Path.GetFileName(filePath));
        }

        [HttpPost]
        public ActionResult DeletePastPaper(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var pastPaper = _context.PastPapers.Find(id);
            if (pastPaper == null) return Json(new { success = false, message = "Past paper not found" });
            if (pastPaper.UploadedBy != teacher.Id) return Json(new { success = false, message = "You can only delete papers you uploaded" });

            string paperPath = Server.MapPath(pastPaper.FilePath);
            if (System.IO.File.Exists(paperPath)) System.IO.File.Delete(paperPath);
            if (!string.IsNullOrEmpty(pastPaper.MemoPath))
            {
                string memoPath = Server.MapPath(pastPaper.MemoPath);
                if (System.IO.File.Exists(memoPath)) System.IO.File.Delete(memoPath);
            }

            _context.PastPapers.Remove(pastPaper);
            _context.SaveChanges();

            return Json(new { success = true, message = "Past paper deleted successfully" });
        }

        // ============================================
        // STUDY MATERIALS
        // ============================================

        public ActionResult StudyMaterials()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var materials = _context.StudyMaterials
                .Where(m => m.UploadedBy == teacher.Id && m.IsActive)
                .OrderByDescending(m => m.UploadedDate)
                .ToList()
                .Select(m => new StudyMaterialListViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    Subject = m.Subject,
                    GradeLevel = m.GradeLevel,
                    FilePath = m.FilePath,
                    FileName = m.FileName,
                    FileType = m.FileType,
                    FileSize = m.FileSize,
                    FileSizeFormatted = FormatFileSize(m.FileSize),
                    UploadedByName = teacher.FullName,
                    UploadedDate = m.UploadedDate,
                    DownloadCount = m.DownloadCount
                })
                .ToList();

            return View(materials);
        }

        public ActionResult UploadStudyMaterial()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = teacher.SubjectQualifications
                .Select(sq => sq.Subject)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            ViewBag.Grades = new SelectList(_context.Grades.OrderBy(g => g.Level), "Id", "Name");

            return View();
        }

        [HttpPost]
        public ActionResult UploadStudyMaterial(StudyMaterialUploadViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (!ModelState.IsValid) return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                var subject = _context.Subjects.Find(model.SubjectId);
                var grade = _context.Grades.Find(model.GradeId);

                string uploadPath = Server.MapPath("~/Uploads/StudyMaterials/");
                if (!System.IO.Directory.Exists(uploadPath)) System.IO.Directory.CreateDirectory(uploadPath);

                string fileName = null;
                if (model.File != null && model.File.ContentLength > 0)
                {
                    if (model.File.ContentLength > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 50MB limit" });

                    string extension = System.IO.Path.GetExtension(model.File.FileName);
                    fileName = $"StudyMaterial_{DateTime.Now:yyyyMMddHHmmss}_{teacher.Id}_{subject?.Code}_{grade?.Name}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.File.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                var studyMaterial = new StudyMaterial
                {
                    Title = model.Title,
                    Description = model.Description,
                    Subject = subject?.Name,
                    GradeLevel = grade?.Name,
                    FilePath = "/Uploads/StudyMaterials/" + fileName,
                    FileName = model.File.FileName,
                    FileType = model.File.ContentType,
                    FileSize = model.File.ContentLength,
                    UploadedBy = teacher.Id,
                    UploadedDate = DateTime.Now,
                    DownloadCount = 0,
                    IsActive = true
                };

                _context.StudyMaterials.Add(studyMaterial);
                _context.SaveChanges();

                return Json(new { success = true, message = "Study material uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DeleteStudyMaterialConfirmed(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            var material = _context.StudyMaterials.FirstOrDefault(m => m.Id == id && m.UploadedBy == teacher.Id);
            if (material == null) return Json(new { success = false, message = "Material not found" });

            string filePath = Server.MapPath(material.FilePath);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            _context.StudyMaterials.Remove(material);
            _context.SaveChanges();

            return Json(new { success = true, message = "Study material deleted successfully" });
        }

        // ============================================
        // TIMETABLE
        // ============================================

        public ActionResult Timetable()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            ViewBag.TeacherName = teacher.FullName;
            return View();
        }

        public ActionResult ViewTeacherTimetable()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var teacherTimetable = _context.TimeTables
                .Where(t => t.Type == "Teachers" && t.IsActive)
                .OrderByDescending(t => t.UploadedAt)
                .FirstOrDefault();

            if (teacherTimetable == null)
            {
                ViewBag.NoTimetable = true;
                ViewBag.Message = "No teachers timetable has been uploaded yet.";
                return View();
            }

            ViewBag.Timetable = teacherTimetable;
            ViewBag.TeacherName = teacher.FullName;
            return View();
        }

        public ActionResult ViewLearnersTimetable(int? gradeId = null, int? classId = null)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            ViewBag.TeacherName = teacher.FullName;

            var teacherClasses = _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Include(a => a.Class.Grade)
                .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                .Select(a => a.Class)
                .Distinct()
                .ToList();

            if (!teacherClasses.Any())
            {
                ViewBag.NoClasses = true;
                ViewBag.Message = "You have not been assigned to any classes yet.";
                return View();
            }

            if (!gradeId.HasValue)
            {
                ViewBag.Grades = teacherClasses.Select(c => c.Grade).Distinct().OrderBy(g => g.Level).ToList();
                ViewBag.ShowGradeSelection = true;
                return View();
            }

            var selectedGrade = _context.Grades.Find(gradeId);
            if (selectedGrade == null) return RedirectToAction("ViewLearnersTimetable");

            if (!classId.HasValue)
            {
                ViewBag.Grade = selectedGrade;
                ViewBag.Classes = teacherClasses.Where(c => c.GradeId == gradeId).OrderBy(c => c.Name).ToList();
                ViewBag.ShowClassSelection = true;
                return View();
            }

            var selectedClass = _context.Classes.Find(classId);
            if (selectedClass == null) return RedirectToAction("ViewLearnersTimetable", new { gradeId = gradeId });

            var timetable = _context.TimeTables
                .Where(t => t.Type == "Learners" && t.Grade == selectedGrade.Name && t.ClassName == selectedClass.FullName && t.IsActive)
                .OrderByDescending(t => t.UploadedAt)
                .FirstOrDefault();

            if (timetable == null)
            {
                ViewBag.NoTimetable = true;
                ViewBag.Message = $"No timetable has been uploaded for {selectedClass.FullName} yet.";
                ViewBag.Grade = selectedGrade;
                ViewBag.ClassName = selectedClass;
                return View();
            }

            ViewBag.Timetable = timetable;
            ViewBag.Grade = selectedGrade;
            ViewBag.ClassName = selectedClass;
            ViewBag.ShowTimetable = true;

            return View();
        }

        // ============================================
        // CLASS REGISTER
        // ============================================

        public ActionResult ClassRegister()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.ShowGradeSelection = true;
            ViewBag.ShowClassSelection = false;
            ViewBag.ShowRegisters = false;

            return View();
        }

        public ActionResult SelectClassRegisterClass(int gradeId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var grade = _context.Grades.Find(gradeId);
            if (grade == null) return RedirectToAction("ClassRegister");

            var classes = _context.Classes
                .Where(c => c.GradeId == gradeId)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.Grade = grade;
            ViewBag.Classes = classes;
            ViewBag.ShowGradeSelection = false;
            ViewBag.ShowClassSelection = true;
            ViewBag.ShowRegisters = false;

            return View("ClassRegister");
        }

        public ActionResult ViewClassRegisters(int classId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var classEntity = _context.Classes.Include(c => c.Grade).FirstOrDefault(c => c.Id == classId);
            if (classEntity == null) return RedirectToAction("ClassRegister");

            var registers = _context.ClassRegisters
                .Where(r => r.Grade == classEntity.Grade.Name && r.ClassName == classEntity.FullName && r.IsActive)
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Term)
                .ToList()
                .Select(r => new ClassRegisterViewModel
                {
                    Id = r.Id,
                    Grade = r.Grade,
                    ClassName = r.ClassName,
                    Term = r.Term,
                    Year = r.Year,
                    FilePath = r.FilePath,
                    Description = r.Description,
                    UploadedByName = "Admin",
                    UploadedAt = r.UploadedAt
                }).ToList();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.Grade = classEntity.Grade;
            ViewBag.ClassName = classEntity;
            ViewBag.ShowGradeSelection = false;
            ViewBag.ShowClassSelection = false;
            ViewBag.ShowRegisters = true;

            return View("ClassRegister", registers);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private Teacher GetCurrentTeacher()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);
            if (user == null) return null;

            return _context.Teachers
                .Include(t => t.SubjectQualifications.Select(sq => sq.Subject))
                .Include(t => t.GradeAssignments.Select(ga => ga.Grade))
                .FirstOrDefault(t => t.UserId == user.Id);
        }

        private List<string> GetYearsList()
        {
            var years = new List<string>();
            int currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 10; i--)
                years.Add(i.ToString());
            return years;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetGradeIcon(string grade)
        {
            switch (grade)
            {
                case "Grade 8": return "fas fa-star";
                case "Grade 9": return "fas fa-star-of-life";
                case "Grade 10": return "fas fa-rocket";
                case "Grade 11": return "fas fa-space-shuttle";
                case "Grade 12": return "fas fa-crown";
                default: return "fas fa-layer-group";
            }
        }

        private string GetGradeColor(string grade)
        {
            switch (grade)
            {
                case "Grade 8": return "primary";
                case "Grade 9": return "info";
                case "Grade 10": return "success";
                case "Grade 11": return "warning";
                case "Grade 12": return "danger";
                default: return "secondary";
            }
        }

        private string GetAnnouncementTypeBadgeClass(string type)
        {
            switch (type)
            {
                case "Important": return "badge-warning";
                case "Urgent": return "badge-danger";
                case "Event": return "badge-info";
                default: return "badge-secondary";
            }
        }

        private string GetAnnouncementAudienceBadgeClass(string audience)
        {
            switch (audience)
            {
                case "Students Only": return "badge-primary";
                case "Teachers Only": return "badge-success";
                case "Parents Only": return "badge-info";
                default: return "badge-secondary";
            }
        }

        // ============================================
        // PLACEHOLDER ACTIONS
        // ============================================

        public ActionResult CaptureMarks() { ViewBag.FeatureName = "Capture Marks"; return View("ComingSoon"); }
        public ActionResult Assignments() { ViewBag.FeatureName = "Assignments"; return View("ComingSoon"); }
        public ActionResult ClassAnalytics() { ViewBag.FeatureName = "Class Analytics"; return View("ComingSoon"); }
        public ActionResult ViewReports() { ViewBag.FeatureName = "View Reports"; return View("ComingSoon"); }
        public ActionResult GroupChat() { ViewBag.FeatureName = "Group Chat"; return View("ComingSoon"); }

        // ============================================
        // GRADE HOMEWORK - TEACHER
        // ============================================

        public ActionResult GradeHomework(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var homework = _context.Homeworks
                .Include(h => h.Subject)
                .Include(h => h.Class)
                .FirstOrDefault(h => h.Id == id && h.UploadedBy == teacher.Id);

            if (homework == null)
            {
                TempData["ErrorMessage"] = "Homework not found or you don't have permission.";
                return RedirectToAction("ViewMyHomework");
            }

            var submissions = _context.HomeworkSubmissions
                .Include(s => s.Student)
                .Include(s => s.Student.User)
                .Where(s => s.HomeworkId == id)
                .OrderBy(s => s.Student.LastName)
                .ToList();

            var viewModel = new GradeHomeworkViewModel
            {
                HomeworkId = homework.Id,
                HomeworkTitle = homework.Title,
                Subject = homework.SubjectDisplay,
                ClassName = homework.ClassNameDisplay,
                DueDate = homework.DueDate,
                Submissions = submissions.Select(s => new HomeworkSubmissionViewModel
                {
                    Id = s.Id,
                    StudentId = s.StudentId,
                    StudentName = s.Student?.FullName ?? "Unknown",
                    StudentNumber = s.Student?.User?.StudentNumber ?? "N/A",
                    Content = s.Content,
                    FilePath = s.FilePath,
                    FileName = s.FileName,
                    SubmittedAt = s.SubmittedAt,
                    Grade = s.Grade,
                    TeacherFeedback = s.TeacherFeedback,
                    Status = s.Status,
                    IsLate = s.IsLate
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveHomeworkGrade(int submissionId, decimal? grade, string feedback, int homeworkId)
        {
            try
            {
                var teacher = GetCurrentTeacher();
                if (teacher == null)
                {
                    return Json(new { success = false, message = "Teacher not found. Please log in again." });
                }

                var submission = _context.HomeworkSubmissions
                    .Include(s => s.Homework)
                    .FirstOrDefault(s => s.Id == submissionId);

                if (submission == null)
                {
                    return Json(new { success = false, message = "Submission not found." });
                }

                // Verify teacher owns this homework
                if (submission.Homework.UploadedBy != teacher.Id)
                {
                    return Json(new { success = false, message = "You are not authorized to grade this submission." });
                }

                // Validate grade range
                if (grade.HasValue && (grade.Value < 0 || grade.Value > 100))
                {
                    return Json(new { success = false, message = "Grade must be between 0 and 100." });
                }

                // Save the grade and feedback
                submission.Grade = grade;
                submission.TeacherFeedback = feedback ?? string.Empty;
                submission.GradedBy = teacher.Id;
                submission.GradedAt = DateTime.Now;
                submission.Status = grade.HasValue ? SubmissionStatus.Graded : SubmissionStatus.Submitted;

                _context.SaveChanges();

                return Json(new { success = true, message = "Grade saved successfully!", grade = grade, feedback = feedback });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving grade: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
        // ============================================
        // GRADE CLASSWORK - TEACHER
        // ============================================

        public ActionResult GradeClasswork(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var classwork = _context.Classworks
                .Include(c => c.Subject)
                .Include(c => c.Class)
                .FirstOrDefault(c => c.Id == id && c.UploadedBy == teacher.Id);

            if (classwork == null)
            {
                TempData["ErrorMessage"] = "Classwork not found or you don't have permission.";
                return RedirectToAction("ViewMyClasswork");
            }

            var submissions = _context.ClassworkSubmissions
                .Include(s => s.Student)
                .Include(s => s.Student.User)
                .Where(s => s.ClassworkId == id)
                .OrderBy(s => s.Student.LastName)
                .ToList();

            var viewModel = new GradeClassworkViewModel
            {
                ClassworkId = classwork.Id,
                ClassworkTitle = classwork.Title,
                Subject = classwork.SubjectDisplay,
                ClassName = classwork.ClassNameDisplay,
                Submissions = submissions.Select(s => new ClassworkSubmissionViewModel
                {
                    Id = s.Id,
                    StudentId = s.StudentId,
                    StudentName = s.Student?.FullName ?? "Unknown",
                    StudentNumber = s.Student?.User?.StudentNumber ?? "N/A",
                    Content = s.Content,
                    FilePath = s.FilePath,
                    FileName = s.FileName,
                    SubmittedAt = s.SubmittedAt,
                    Grade = s.Grade,
                    TeacherFeedback = s.TeacherFeedback,
                    Status = s.Status
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveClassworkGrade(int submissionId, decimal? grade, string feedback, int classworkId)
        {
            try
            {
                var teacher = GetCurrentTeacher();
                if (teacher == null)
                {
                    return Json(new { success = false, message = "Teacher not found. Please log in again." });
                }

                var submission = _context.ClassworkSubmissions
                    .Include(s => s.Classwork)
                    .FirstOrDefault(s => s.Id == submissionId);

                if (submission == null)
                {
                    return Json(new { success = false, message = "Submission not found." });
                }

                // Verify teacher owns this classwork
                if (submission.Classwork.UploadedBy != teacher.Id)
                {
                    return Json(new { success = false, message = "You are not authorized to grade this submission." });
                }

                // Validate grade range
                if (grade.HasValue && (grade.Value < 0 || grade.Value > 100))
                {
                    return Json(new { success = false, message = "Grade must be between 0 and 100." });
                }

                // Save the grade and feedback
                submission.Grade = grade;
                submission.TeacherFeedback = feedback ?? string.Empty;
                submission.GradedBy = teacher.Id;
                submission.GradedAt = DateTime.Now;
                submission.Status = grade.HasValue ? SubmissionStatus.Graded : SubmissionStatus.Submitted;

                _context.SaveChanges();

                return Json(new { success = true, message = "Grade saved successfully!", grade = grade, feedback = feedback });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving classwork grade: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        // ============================================
        // SUBMISSION ANALYTICS
        // ============================================

        public ActionResult SubmissionAnalytics(int id, string type)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var analytics = new SubmissionAnalyticsViewModel();
            analytics.AssignmentId = id;
            analytics.AssignmentType = type;

            if (type == "homework")
            {
                var homework = _context.Homeworks
                    .Include(h => h.Subject)
                    .Include(h => h.Class)
                    .FirstOrDefault(h => h.Id == id && h.UploadedBy == teacher.Id);

                if (homework == null) return HttpNotFound();

                analytics.AssignmentTitle = homework.Title;
                analytics.Subject = homework.SubjectDisplay;
                analytics.ClassName = homework.ClassNameDisplay;

                // Get all students in the class
                var classStudents = _context.Students
                    .Where(s => s.ClassId == homework.ClassId && s.IsActive)
                    .ToList();
                analytics.TotalStudents = classStudents.Count;

                // Get submissions
                var submissions = _context.HomeworkSubmissions
                    .Where(s => s.HomeworkId == id)
                    .ToList();

                analytics.SubmissionsReceived = submissions.Count;
                analytics.NotSubmitted = analytics.TotalStudents - analytics.SubmissionsReceived;
                analytics.SubmissionRate = analytics.TotalStudents > 0
                    ? Math.Round((double)analytics.SubmissionsReceived / analytics.TotalStudents * 100, 1)
                    : 0;

                // Calculate pass/fail based on saved marks
                var gradedSubmissions = submissions.Where(s => s.Grade.HasValue).ToList();
                analytics.PassCount = gradedSubmissions.Count(s => s.Grade.Value >= 50);
                analytics.FailCount = gradedSubmissions.Count(s => s.Grade.Value < 50);
                analytics.UngradedCount = submissions.Count(s => !s.Grade.HasValue);
                analytics.PassRate = analytics.SubmissionsReceived > 0
                    ? Math.Round((double)analytics.PassCount / analytics.SubmissionsReceived * 100, 1)
                    : 0;

                // Calculate statistics
                var grades = gradedSubmissions.Select(s => s.Grade.Value).ToList();
                if (grades.Any())
                {
                    analytics.AverageGrade = Math.Round(grades.Average(), 1);
                    analytics.HighestGrade = grades.Max();
                    analytics.LowestGrade = grades.Min();
                }

                // Grade distribution
                analytics.GradeDistribution = GetGradeDistribution(gradedSubmissions.Select(s => s.Grade.Value).ToList());

                // Student grade list
                analytics.StudentGrades = classStudents.Select(s => new StudentGradeItem
                {
                    StudentId = s.Id,
                    StudentName = s.FullName,
                    StudentNumber = s.User?.StudentNumber ?? "N/A",
                    Grade = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.Grade,
                    Status = GetStudentStatus(submissions.FirstOrDefault(sub => sub.StudentId == s.Id)),
                    Feedback = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.TeacherFeedback,
                    SubmittedAt = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.SubmittedAt
                }).ToList();
            }
            else if (type == "classwork")
            {
                var classwork = _context.Classworks
                    .Include(c => c.Subject)
                    .Include(c => c.Class)
                    .FirstOrDefault(c => c.Id == id && c.UploadedBy == teacher.Id);

                if (classwork == null) return HttpNotFound();

                analytics.AssignmentTitle = classwork.Title;
                analytics.Subject = classwork.SubjectDisplay;
                analytics.ClassName = classwork.ClassNameDisplay;

                // Get all students in the class
                var classStudents = _context.Students
                    .Where(s => s.ClassId == classwork.ClassId && s.IsActive)
                    .ToList();
                analytics.TotalStudents = classStudents.Count;

                // Get submissions
                var submissions = _context.ClassworkSubmissions
                    .Where(s => s.ClassworkId == id)
                    .ToList();

                analytics.SubmissionsReceived = submissions.Count;
                analytics.NotSubmitted = analytics.TotalStudents - analytics.SubmissionsReceived;
                analytics.SubmissionRate = analytics.TotalStudents > 0
                    ? Math.Round((double)analytics.SubmissionsReceived / analytics.TotalStudents * 100, 1)
                    : 0;

                // Calculate pass/fail based on saved marks
                var gradedSubmissions = submissions.Where(s => s.Grade.HasValue).ToList();
                analytics.PassCount = gradedSubmissions.Count(s => s.Grade.Value >= 50);
                analytics.FailCount = gradedSubmissions.Count(s => s.Grade.Value < 50);
                analytics.UngradedCount = submissions.Count(s => !s.Grade.HasValue);
                analytics.PassRate = analytics.SubmissionsReceived > 0
                    ? Math.Round((double)analytics.PassCount / analytics.SubmissionsReceived * 100, 1)
                    : 0;

                // Calculate statistics
                var grades = gradedSubmissions.Select(s => s.Grade.Value).ToList();
                if (grades.Any())
                {
                    analytics.AverageGrade = Math.Round(grades.Average(), 1);
                    analytics.HighestGrade = grades.Max();
                    analytics.LowestGrade = grades.Min();
                }

                // Grade distribution
                analytics.GradeDistribution = GetGradeDistribution(gradedSubmissions.Select(s => s.Grade.Value).ToList());

                // Student grade list
                analytics.StudentGrades = classStudents.Select(s => new StudentGradeItem
                {
                    StudentId = s.Id,
                    StudentName = s.FullName,
                    StudentNumber = s.User?.StudentNumber ?? "N/A",
                    Grade = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.Grade,
                    Status = GetClassworkStudentStatus(submissions.FirstOrDefault(sub => sub.StudentId == s.Id)),
                    Feedback = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.TeacherFeedback,
                    SubmittedAt = submissions.FirstOrDefault(sub => sub.StudentId == s.Id)?.SubmittedAt
                }).ToList();
            }

            return View(analytics);
        }

        private string GetStudentStatus(HomeworkSubmission submission)
        {
            if (submission == null) return "Not Submitted";
            if (!submission.Grade.HasValue) return "Pending Grade";
            return submission.Grade.Value >= 50 ? "Passed" : "Failed";
        }

        private string GetClassworkStudentStatus(ClassworkSubmission submission)
        {
            if (submission == null) return "Not Submitted";
            if (!submission.Grade.HasValue) return "Pending Grade";
            return submission.Grade.Value >= 50 ? "Passed" : "Failed";
        }

        private List<GradeDistributionItem> GetGradeDistribution(List<decimal> grades)
        {
            var distribution = new List<GradeDistributionItem>();

            var ranges = new[]
            {
   new { Range = "0-49% (Fail)", Min = 0m, Max = 49m, Color = "#dc3545" },
   new { Range = "50-59%", Min = 50m, Max = 59m, Color = "#ffc107" },
   new { Range = "60-69%", Min = 60m, Max = 69m, Color = "#17a2b8" },
   new { Range = "70-79%", Min = 70m, Max = 79m, Color = "#fd7e14" },
   new { Range = "80-89%", Min = 80m, Max = 89m, Color = "#28a745" },
   new { Range = "90-100%", Min = 90m, Max = 100m, Color = "#20c997" }
};

            foreach (var range in ranges)
            {
                distribution.Add(new GradeDistributionItem
                {
                    Range = range.Range,
                    Count = grades.Count(g => g >= range.Min && g <= range.Max),
                    Color = range.Color
                });
            }

            return distribution;
        }
        // ============================================
        // EXAM TIMETABLE DURATION REQUESTS
        // ============================================

        /// <summary>
        /// Display all pending exam duration requests for the teacher
        /// </summary>
        public ActionResult ExamDurationRequests()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var pendingTimetables = _context.ExamTimetables
                .Where(t => t.Status == ExamTimetableStatus.AwaitingTeacherInput && t.IsActive)
                .ToList();

            var viewModels = new List<TeacherExamNotificationViewModel>();

            foreach (var timetable in pendingTimetables)
            {
                var pendingNotifications = _examTimetableService.GetPendingNotificationsForTeacher(teacher.Id, timetable.Id);

                if (pendingNotifications.Any())
                {
                    var viewModel = new TeacherExamNotificationViewModel
                    {
                        PendingNotifications = pendingNotifications.Select(n => new TeacherPaperDurationViewModel
                        {
                            NotificationId = n.Id,
                            SubjectName = n.Subject?.Name ?? "Unknown",
                            GradeName = n.Grade?.Name ?? "Unknown",
                            HasPaper1 = n.HasPaper1,
                            Paper1Duration = n.Paper1Duration,
                            HasPaper2 = n.HasPaper2,
                            Paper2Duration = n.Paper2Duration,
                            HasPaper3 = n.HasPaper3,
                            Paper3Duration = n.Paper3Duration
                        }).ToList(),
                        ExamTimetableId = timetable.Id,
                        TimetableName = timetable.Name,
                        ResponseDeadline = timetable.CreatedAt.AddDays(7)
                    };
                    viewModels.Add(viewModel);
                }
            }

            return View(viewModels);
        }
        public ActionResult ViewTeacherExamTimetable(int? id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            // If no ID provided, get the most recent distributed or generated timetable
            if (!id.HasValue || id.Value == 0)
            {
                var latestTimetable = _context.ExamTimetables
                    .Where(t => (t.Status == ExamTimetableStatus.Generated || t.Status == ExamTimetableStatus.Distributed) && t.IsActive)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault();

                if (latestTimetable == null)
                {
                    TempData["ErrorMessage"] = "No exam timetable available yet.";
                    return RedirectToAction("ExamDurationRequests");
                }

                id = latestTimetable.Id;
            }

            var timetable = _context.ExamTimetables.Find(id.Value);
            if (timetable == null)
            {
                TempData["ErrorMessage"] = "Timetable not found.";
                return RedirectToAction("ExamDurationRequests");
            }

            if (timetable.Status < ExamTimetableStatus.Generated)
            {
                TempData["ErrorMessage"] = "Timetable has not been generated yet.";
                return RedirectToAction("ExamDurationRequests");
            }

            // Get subjects taught by this teacher
            var teacherSubjects = _context.TeacherSubjectAssignments
                .Where(t => t.TeacherId == teacher.Id && t.IsActive)
                .Select(t => t.SubjectId)
                .ToList();

            var sessions = _context.ExamSessions
                .Include(s => s.Subject)
                .Include(s => s.Grade)
                .Where(s => s.ExamTimetableId == id.Value
                    && teacherSubjects.Contains(s.SubjectId)
                    && s.IsActive)
                .OrderBy(s => s.ExamDate)
                .ThenBy(s => s.StartTime)
                .ToList();

            var viewModel = new ExamTimetableDetailViewModel
            {
                Id = timetable.Id,
                Name = timetable.Name,
                AcademicYear = timetable.AcademicYear,
                NumberOfWeeks = timetable.NumberOfWeeks,
                StartDate = timetable.StartDate,
                EndDate = timetable.EndDate,
                Status = timetable.Status,
                ExamSessions = sessions.Select(s => new ExamSessionViewModel
                {
                    Id = s.Id,
                    SubjectName = s.Subject?.Name ?? "Unknown",
                    GradeName = s.Grade?.Name ?? "Unknown",
                    ClassNames = s.ExamSessionClasses != null && s.ExamSessionClasses.Any()
                        ? string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(c => !string.IsNullOrEmpty(c)))
                        : s.Grade?.Name ?? "Unknown",
                    PaperNumber = s.PaperNumber.ToString(),
                    ExamDate = s.ExamDate,
                    ExamDateDisplay = s.ExamDate.ToString("dd MMM yyyy"),
                    DayOfWeek = s.ExamDate.DayOfWeek.ToString(),
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    DurationHours = s.DurationHours,
                    WeekNumber = s.WeekNumber,
                    Venue = s.Venue,
                    Invigilator = s.Invigilator
                }).ToList()
            };

            // Group by Week for better display
            if (sessions.Any())
            {
                viewModel.SessionsByWeek = sessions
                    .GroupBy(s => $"Week {s.WeekNumber}")
                    .ToDictionary(g => g.Key, g => g.Select(s => new ExamSessionViewModel
                    {
                        Id = s.Id,
                        SubjectName = s.Subject?.Name ?? "Unknown",
                        GradeName = s.Grade?.Name ?? "Unknown",
                        ClassNames = s.ExamSessionClasses != null && s.ExamSessionClasses.Any()
                            ? string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(c => !string.IsNullOrEmpty(c)))
                            : s.Grade?.Name ?? "Unknown",
                        PaperNumber = s.PaperNumber.ToString(),
                        ExamDate = s.ExamDate,
                        ExamDateDisplay = s.ExamDate.ToString("dd MMM yyyy"),
                        DayOfWeek = s.ExamDate.DayOfWeek.ToString(),
                        StartTime = s.StartTime.ToString(@"hh\:mm"),
                        EndTime = s.EndTime.ToString(@"hh\:mm"),
                        DurationHours = s.DurationHours,
                        WeekNumber = s.WeekNumber,
                        Venue = s.Venue,
                        Invigilator = s.Invigilator
                    }).ToList());
            }

            return View(viewModel);
        }

        /// <summary>
        /// Submit exam durations for all pending notifications
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitExamDurations()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== SubmitExamDurations Called ===");

                var teacher = GetCurrentTeacher();
                if (teacher == null)
                {
                    return Json(new { success = false, message = "Teacher not found" });
                }

                // Get the notifications JSON from FormData
                string notificationsJson = Request.Form["notifications"];

                if (string.IsNullOrEmpty(notificationsJson))
                {
                    return Json(new { success = false, message = "No data submitted" });
                }

                System.Diagnostics.Debug.WriteLine($"Notifications JSON: {notificationsJson}");

                // Deserialize
                var notifications = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TeacherPaperDurationViewModel>>(notificationsJson);

                if (notifications == null || !notifications.Any())
                {
                    return Json(new { success = false, message = "No valid data submitted" });
                }

                int successCount = 0;
                var errors = new List<string>();

                foreach (var notification in notifications)
                {
                    var submitted = _examTimetableService.SubmitTeacherDurations(
                        notification.NotificationId,
                        notification.HasPaper1, notification.Paper1Duration,
                        notification.HasPaper2, notification.Paper2Duration,
                        notification.HasPaper3, notification.Paper3Duration);

                    if (submitted)
                    {
                        successCount++;
                    }
                    else
                    {
                        errors.Add($"{notification.SubjectName}: Failed to submit");
                    }
                }

                return Json(new { success = true, message = $"Submitted {successCount} of {notifications.Count}", hasErrors = errors.Any(), errors = errors });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Add this class inside TeacherController.cs (at the bottom)
        public class ExamDurationRequest
        {
            public List<TeacherPaperDurationViewModel> notifications { get; set; }
        }

        /// <summary>
        /// View the generated exam timetable for the teacher (only subjects they teach)
        /// </summary>
        public ActionResult ViewTeacherExamTimetable(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var timetable = _examTimetableService.GetTimetable(id);
            if (timetable == null)
            {
                TempData["ErrorMessage"] = "Timetable not found.";
                return RedirectToAction("ExamDurationRequests");
            }

            if (timetable.Status < ExamTimetableStatus.Generated)
            {
                TempData["ErrorMessage"] = "Timetable has not been generated yet. Please wait for the admin to generate it.";
                return RedirectToAction("ExamDurationRequests");
            }

            var sessions = _examTimetableService.GetExamSessionsForTeacher(id, teacher.Id);

            var viewModel = new ExamTimetableDetailViewModel
            {
                Id = timetable.Id,
                Name = timetable.Name,
                AcademicYear = timetable.AcademicYear,
                NumberOfWeeks = timetable.NumberOfWeeks,
                StartDate = timetable.StartDate,
                EndDate = timetable.EndDate,
                Status = timetable.Status,
                ExamSessions = sessions.Select(s => new ExamSessionViewModel
                {
                    Id = s.Id,
                    SubjectName = s.Subject?.Name ?? "Unknown",
                    GradeName = s.Grade?.Name ?? "Unknown",
                    ClassNames = s.ExamSessionClasses != null && s.ExamSessionClasses.Any()
                        ? string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(c => !string.IsNullOrEmpty(c)))
                        : s.Grade?.Name ?? "Unknown",
                    PaperNumber = s.PaperNumber.ToString(),
                    ExamDate = s.ExamDate,
                    ExamDateDisplay = s.ExamDate.ToString("dd MMM yyyy"),
                    DayOfWeek = s.ExamDate.DayOfWeek.ToString(),
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    DurationHours = s.DurationHours,
                    WeekNumber = s.WeekNumber,
                    Venue = s.Venue,
                    Invigilator = s.Invigilator,
                    IsEditable = false
                }).ToList()
            };

            // Group by Week for better display
            if (sessions.Any())
            {
                viewModel.SessionsByWeek = sessions
                    .GroupBy(s => $"Week {s.WeekNumber}")
                    .ToDictionary(g => g.Key, g => g.Select(s => new ExamSessionViewModel
                    {
                        Id = s.Id,
                        SubjectName = s.Subject?.Name ?? "Unknown",
                        GradeName = s.Grade?.Name ?? "Unknown",
                        ClassNames = s.ExamSessionClasses != null && s.ExamSessionClasses.Any()
                            ? string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(c => !string.IsNullOrEmpty(c)))
                            : s.Grade?.Name ?? "Unknown",
                        PaperNumber = s.PaperNumber.ToString(),
                        ExamDate = s.ExamDate,
                        ExamDateDisplay = s.ExamDate.ToString("dd MMM yyyy"),
                        DayOfWeek = s.ExamDate.DayOfWeek.ToString(),
                        StartTime = s.StartTime.ToString(@"hh\:mm"),
                        EndTime = s.EndTime.ToString(@"hh\:mm"),
                        DurationHours = s.DurationHours,
                        WeekNumber = s.WeekNumber,
                        Venue = s.Venue,
                        Invigilator = s.Invigilator,
                        IsEditable = false
                    }).ToList());
            }

            ViewBag.TeacherName = teacher.FullName;
            return View(viewModel);
        }
        // Add this class INSIDE TeacherController.cs (at the bottom of the file, before the closing brace)
        public class DurationSubmissionModel
        {
            public List<TeacherPaperDurationViewModel> Notifications { get; set; }
        }
        [HttpGet]
        public ActionResult TestApi()
        {
            return Json(new { success = true, message = "API is working" }, JsonRequestBehavior.AllowGet);
        }
    }
}
