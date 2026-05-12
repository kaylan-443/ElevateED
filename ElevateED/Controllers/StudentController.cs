using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity.Validation;

namespace ElevateED.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private IExamTimetableService _examTimetableService;
        public StudentController()
        {
            _context = new ElevateEDContext();
            _examTimetableService = new ExamTimetableService();

        }


        // ============================================
        // DASHBOARD
        // ============================================

        public ActionResult Dashboard()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
                return RedirectToAction("Login", "Account");

            if (user.Role == UserRole.Applicant)
                return RedirectToAction("PendingApproval", "Application");

            if (!user.HasChangedPassword)
                return RedirectToAction("ChangePassword", "Account", new { firstLogin = true });

            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .Include(s => s.Class.ClassTeacher)
                .Include(s => s.Stream)
                .Include(s => s.GradeApplyingFor)
                .FirstOrDefault(s => s.UserId == user.Id);

            var viewModel = new StudentDashboardViewModel
            {
                StudentNumber = studentNumber,
                Email = user.Email,
                Status = "Active"
            };

            if (student != null)
            {
                viewModel.FullName = student.FullName;
                viewModel.Grade = student.Grade ?? "Not assigned";
                viewModel.ClassName = student.ClassName ?? "Not assigned";
                viewModel.ClassTeacher = student.Class?.ClassTeacher?.FullName;
                viewModel.Status = student.IsActive ? "Active" : "Inactive";

                // Attendance data
                var attendanceRecords = _context.AttendanceRecords
                    .Where(r => r.StudentId == student.Id)
                    .ToList();

                viewModel.PresentDays = attendanceRecords.Count(r => r.IsPresent);
                viewModel.AbsentDays = attendanceRecords.Count(r => !r.IsPresent);
                viewModel.LateDays = 0;
                viewModel.TotalSchoolDays = viewModel.PresentDays + viewModel.AbsentDays;
                viewModel.AttendancePercentage = viewModel.TotalSchoolDays > 0
                    ? Math.Round((double)viewModel.PresentDays / viewModel.TotalSchoolDays * 100, 1)
                    : 100;

                // Homework data
                if (student.ClassId.HasValue)
                {
                    var now = DateTime.Now;
                    var homework = _context.Homeworks
                        .Where(h => h.ClassId == student.ClassId && h.IsActive)
                        .ToList();

                    viewModel.PendingHomeworkCount = homework.Count(h => h.DueDate >= now);
                    viewModel.OverdueHomeworkCount = homework.Count(h => h.DueDate < now);

                    viewModel.UpcomingHomework = homework
                        .Where(h => h.DueDate >= now)
                        .OrderBy(h => h.DueDate)
                        .Take(3)
                        .Select(h => new UpcomingHomeworkItem
                        {
                            Title = h.Title,
                            SubjectName = h.SubjectDisplay ?? "Unknown",
                            DaysUntil = Math.Max(0, (int)(h.DueDate - now).TotalDays)
                        })
                        .ToList();
                }

                // Quiz data
                var quizAttempts = _context.QuizAttempts
                    .Where(q => q.StudentId == student.Id)
                    .ToList();

                viewModel.TotalQuizAttempts = quizAttempts.Count;
                viewModel.QuizAverageScore = quizAttempts.Any()
                    ? Math.Round(quizAttempts.Average(q => (double)q.Score), 1)
                    : 0;

                viewModel.RecentQuizzes = quizAttempts
                    .OrderByDescending(q => q.AttemptedAt)
                    .Take(3)
                    .Select(q => new RecentQuizItem
                    {
                        SubjectName = q.Subject?.Name ?? "Unknown",
                        Score = (double)q.Score,
                        AttemptedAt = q.AttemptedAt
                    })
                    .ToList();

                // Today's schedule
                if (student.Class != null)
                {
                    viewModel.TodayClasses = _context.TimeTables
                        .Where(t => t.ClassName == student.Class.FullName
                            && t.Type == "Learners"
                            && t.IsActive)
                        .OrderBy(t => t.UploadedAt)
                        .Take(5)
                        .ToList()
                        .Select((t, index) => new TodayClassItem
                        {
                            SubjectName = t.Title ?? $"Period {index + 1}",
                            TeacherName = "Assigned Teacher",
                            RoomNumber = $"Room {100 + index}",
                            StartTime = DateTime.Today.AddHours(8 + index)
                        })
                        .ToList();
                }
            }
            else
            {
                var applicant = _context.Applicants
                    .Include(a => a.GradeApplyingFor)
                    .FirstOrDefault(a => a.UserId == user.Id);

                if (applicant != null)
                {
                    viewModel.FullName = applicant.FullName;
                    viewModel.Grade = applicant.GradeApplyingForDisplay ?? "Not assigned";
                    viewModel.ClassName = "Not yet assigned";
                    viewModel.Status = applicant.Status.ToString();
                }
                else
                {
                    viewModel.FullName = "Student";
                    viewModel.Grade = "Not assigned";
                    viewModel.ClassName = "Not assigned";
                }
            }

            return View(viewModel);
        }

        // ============================================
        // TIMETABLE
        // ============================================

        public ActionResult Timetable()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (user.Role != UserRole.Student)
            {
                TempData["ErrorMessage"] = "Access denied. Students only.";
                return RedirectToAction("Dashboard");
            }

            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found. Please contact the administrator.";
                return RedirectToAction("Dashboard");
            }

            var studentGrade = student.Grade;
            var studentClass = student.ClassName;

            TimeTable timetable = null;

            if (!string.IsNullOrEmpty(studentClass))
            {
                timetable = _context.TimeTables
                    .FirstOrDefault(t => t.Type == "Learners" && t.ClassName == studentClass && t.IsActive);
            }

            if (timetable == null && !string.IsNullOrEmpty(studentGrade))
            {
                timetable = _context.TimeTables
                    .FirstOrDefault(t => t.Type == "Learners" && t.Grade == studentGrade && string.IsNullOrEmpty(t.ClassName) && t.IsActive);
            }

            if (timetable == null && !string.IsNullOrEmpty(studentGrade))
            {
                timetable = _context.TimeTables
                    .FirstOrDefault(t => t.Type == "Learners" && t.Grade.ToLower() == studentGrade.ToLower() && t.IsActive);
            }

            var viewModel = new StudentTimetableViewModel
            {
                StudentName = student.FullName,
                StudentNumber = studentNumber,
                Grade = studentGrade,
                ClassName = studentClass,
                HasTimetable = timetable != null,
                TimetableTitle = timetable?.Title,
                TimetableDescription = timetable?.Description,
                TimetableFilePath = timetable?.FilePath,
                UploadedDate = timetable?.UploadedAt
            };

            return View(viewModel);
        }

        // ============================================
        // PAST PAPERS
        // ============================================

        public ActionResult PastPapers()
        {
            var grades = _context.Grades.OrderBy(g => g.Level).ToList();
            var gradeCards = new List<GradeCardViewModel>();

            foreach (var grade in grades)
            {
                var paperCount = _context.PastPapers.Count(p => p.Grade == grade.Name && p.IsPublished);

                string icon = "";
                string color = "";

                switch (grade.Name)
                {
                    case "Grade 8": icon = "fas fa-star"; color = "primary"; break;
                    case "Grade 9": icon = "fas fa-star-of-life"; color = "info"; break;
                    case "Grade 10": icon = "fas fa-rocket"; color = "success"; break;
                    case "Grade 11": icon = "fas fa-space-shuttle"; color = "warning"; break;
                    case "Grade 12": icon = "fas fa-crown"; color = "danger"; break;
                }

                gradeCards.Add(new GradeCardViewModel
                {
                    Grade = grade.Name,
                    PaperCount = paperCount,
                    Icon = icon,
                    Color = color
                });
            }

            return View(gradeCards);
        }

        public ActionResult ViewPastPapersByGrade(string grade, string subject = "", string year = "", string term = "")
        {
            var query = _context.PastPapers.Where(p => p.Grade == grade && p.IsPublished);

            if (!string.IsNullOrEmpty(subject)) query = query.Where(p => p.Subject == subject);
            if (!string.IsNullOrEmpty(year)) query = query.Where(p => p.Year == year);
            if (!string.IsNullOrEmpty(term)) query = query.Where(p => p.Term == term);

            var papers = query.OrderByDescending(p => p.Year).ThenBy(p => p.Subject).ToList();

            var subjects = _context.PastPapers
                .Where(p => p.Grade == grade && p.IsPublished)
                .Select(p => p.Subject)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var viewModel = new PastPaperListViewModel
            {
                Grade = grade,
                Papers = papers.Select(p => new PastPaperDisplayViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Subject = p.Subject,
                    Year = int.TryParse(p.Year, out int yearNum) ? yearNum : 0,
                    Term = p.Term,
                    ExamType = p.ExamType,
                    Description = p.Description,
                    FilePath = p.FilePath,
                    MemoPath = p.MemoPath
                }).ToList(),
                Subjects = subjects,
                SelectedSubject = subject,
                SelectedYear = year,
                SelectedTerm = term
            };

            return View(viewModel);
        }

        public ActionResult ViewAllPastPapers()
        {
            var papers = _context.PastPapers
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.Year)
                .ThenBy(p => p.Grade)
                .ThenBy(p => p.Subject)
                .ToList();

            var viewModel = new PastPaperListViewModel
            {
                Grade = "All Grades",
                Papers = papers.Select(p => new PastPaperDisplayViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Subject = p.Subject,
                    Year = int.TryParse(p.Year, out int yearNum) ? yearNum : 0,
                    Term = p.Term,
                    ExamType = p.ExamType,
                    Description = p.Description,
                    FilePath = p.FilePath,
                    MemoPath = p.MemoPath
                }).ToList(),
                Subjects = papers.Select(p => p.Subject).Distinct().OrderBy(s => s).ToList(),
                SelectedSubject = "",
                SelectedYear = "",
                SelectedTerm = ""
            };

            return View(viewModel);
        }

        // ============================================
        // STUDY MATERIALS
        // ============================================

        public ActionResult StudyMaterials()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            var subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
            var subjectMaterials = new List<SubjectMaterialViewModel>();

            foreach (var subject in subjects)
            {
                var materialCount = _context.StudyMaterials.Count(m => m.Subject == subject.Name && m.IsActive);

                subjectMaterials.Add(new SubjectMaterialViewModel
                {
                    SubjectName = subject.Name,
                    MaterialCount = materialCount,
                    Icon = GetSubjectIcon(subject.Name),
                    Color = GetSubjectColor(subject.Name)
                });
            }

            subjectMaterials = subjectMaterials.OrderBy(s => s.SubjectName).ToList();

            ViewBag.StudentName = student?.FullName ?? "Student";
            ViewBag.StudentGrade = student?.Grade ?? "Not assigned";
            ViewBag.ClassName = student?.ClassName ?? "Not assigned";

            return View(subjectMaterials);
        }

        public ActionResult SelectGrade(string subject)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            var availableGrades = _context.Grades.OrderBy(g => g.Level).Select(g => g.Name).ToList();
            var materialCounts = new Dictionary<string, int>();

            foreach (var grade in availableGrades)
            {
                var count = _context.StudyMaterials.Count(m => m.Subject == subject && m.GradeLevel == grade && m.IsActive);
                materialCounts[grade] = count;
            }

            ViewBag.Subject = subject;
            ViewBag.StudentGrade = student?.Grade ?? "Not assigned";
            ViewBag.AvailableGrades = availableGrades;
            ViewBag.MaterialCounts = materialCounts;
            ViewBag.StudentName = student?.FullName ?? "Student";

            return View();
        }

        public ActionResult StudyMaterialsBySubjectAndGrade(string subject, string grade)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            var materials = _context.StudyMaterials
                .Where(m => m.Subject == subject && m.GradeLevel == grade && m.IsActive)
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
                    UploadedByName = _context.Teachers.Where(t => t.Id == m.UploadedBy).Select(t => t.FirstName + " " + t.LastName).FirstOrDefault(),
                    UploadedDate = m.UploadedDate,
                    DownloadCount = m.DownloadCount
                })
                .ToList();

            ViewBag.Subject = subject;
            ViewBag.Grade = grade;
            ViewBag.StudentName = student?.FullName ?? "Student";
            ViewBag.StudentGrade = student?.Grade ?? "Not assigned";

            return View(materials);
        }

        public ActionResult DownloadMaterial(int id)
        {
            var material = _context.StudyMaterials.Find(id);
            if (material == null || !material.IsActive)
            {
                return HttpNotFound();
            }

            material.DownloadCount++;
            _context.SaveChanges();

            string filePath = Server.MapPath(material.FilePath);
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound();
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, material.FileType, material.FileName);
        }

        // ============================================
        // HOMEWORK
        // ============================================

        public ActionResult Homework()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found.";
                return RedirectToAction("Dashboard");
            }

            if (student.Class == null)
            {
                TempData["InfoMessage"] = "You have not been assigned to a class yet.";
                return View(new List<HomeworkListViewModel>());
            }

            // Get homework for student's class
            var homework = _context.Homeworks
                .Include(h => h.Teacher)
                .Where(h => h.IsActive)
                .Where(h => h.ClassNameValue == student.Class.FullName || h.ClassId == student.ClassId)
                .OrderByDescending(h => h.DueDate)
                .ToList();

            var homeworkList = homework.Select(h => new HomeworkListViewModel
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
                UploadedByName = h.Teacher?.FullName ?? "Unknown",
                UploadedAt = h.UploadedAt
            }).ToList();

            ViewBag.StudentName = student.FullName;
            ViewBag.StudentGrade = student.Grade;
            ViewBag.ClassName = student.ClassName;

            return View(homeworkList);
        }

        public ActionResult DownloadHomework(int id)
        {
            var homework = _context.Homeworks.Find(id);
            if (homework == null || !homework.IsActive)
            {
                return HttpNotFound();
            }

            string filePath = Server.MapPath(homework.FilePath);
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound();
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, homework.FileType, homework.FileName);
        }

        // ============================================
        // CLASSWORK
        // ============================================

        public ActionResult Classwork()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found.";
                return RedirectToAction("Dashboard");
            }

            if (student.Class == null)
            {
                TempData["InfoMessage"] = "You have not been assigned to a class yet.";
                return View(new List<ClassworkListViewModel>());
            }

            // Get classwork for student's class
            var classwork = _context.Classworks
                .Include(c => c.Teacher)
                .Where(c => c.IsActive)
                .Where(c => c.ClassNameValue == student.Class.FullName || c.ClassId == student.ClassId)
                .OrderByDescending(c => c.UploadedAt)
                .ToList();

            var classworkList = classwork.Select(c => new ClassworkListViewModel
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
                UploadedByName = c.Teacher?.FullName ?? "Unknown",
                UploadedAt = c.UploadedAt
            }).ToList();

            ViewBag.StudentName = student.FullName;
            ViewBag.StudentGrade = student.Grade;
            ViewBag.ClassName = student.ClassName;

            return View(classworkList);
        }

        public ActionResult DownloadClasswork(int id)
        {
            var classwork = _context.Classworks.Find(id);
            if (classwork == null || !classwork.IsActive)
            {
                return HttpNotFound();
            }

            string filePath = Server.MapPath(classwork.FilePath);
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound();
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, classwork.FileType, classwork.FileName);
        }

        // ============================================
        // SETTINGS
        // ============================================

        public ActionResult Settings()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                var applicant = _context.Applicants.FirstOrDefault(a => a.UserId == user.Id);
                if (applicant != null)
                {
                    var settingsViewModel = new StudentSettingsViewModel
                    {
                        StudentNumber = studentNumber,
                        FullName = applicant.FullName,
                        Email = user.Email,
                        CellPhone = applicant.CellPhone,
                        PhysicalAddress = applicant.PhysicalAddress,
                        ParentName = applicant.ParentName,
                        ParentCellPhone = applicant.ParentCellPhone,
                        ParentEmail = applicant.ParentEmail
                    };
                    return View(settingsViewModel);
                }

                return RedirectToAction("Dashboard");
            }

            var viewModel = new StudentSettingsViewModel
            {
                StudentNumber = studentNumber,
                FullName = student.FullName,
                Email = user.Email,
                CellPhone = student.CellPhone,
                PhysicalAddress = student.PhysicalAddress,
                ParentName = student.ParentName,
                ParentCellPhone = student.ParentCellPhone,
                ParentEmail = student.ParentEmail
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(StudentSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Settings", model);
            }

            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student != null)
            {
                student.CellPhone = model.CellPhone;
                student.PhysicalAddress = model.PhysicalAddress;
                student.ParentCellPhone = model.ParentCellPhone;
                student.ParentEmail = model.ParentEmail;
                student.UpdatedAt = DateTime.Now;

                _context.SaveChanges();
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                var applicant = _context.Applicants.FirstOrDefault(a => a.UserId == user.Id);
                if (applicant != null)
                {
                    applicant.CellPhone = model.CellPhone;
                    applicant.PhysicalAddress = model.PhysicalAddress;
                    applicant.ParentCellPhone = model.ParentCellPhone;
                    applicant.ParentEmail = model.ParentEmail;
                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "Profile updated successfully!";
                }
            }

            return RedirectToAction("Settings");
        }

        // ============================================
        // VIEW ANNOUNCEMENTS FOR STUDENTS
        // ============================================

        public ActionResult ViewAnnouncements()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found.";
                return RedirectToAction("Dashboard");
            }

            var currentDate = DateTime.Now;

            var announcements = _context.Announcements
                .Where(a => a.IsActive &&
                    (a.TargetAudience == "All Users" ||
                     a.TargetAudience == "Students Only") &&
                    (!a.ExpiryDate.HasValue || a.ExpiryDate >= currentDate))
                .OrderByDescending(a => a.CreatedAt)
                .ToList()
                .Select(a => new AnnouncementListViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    TargetAudience = a.TargetAudience,
                    TargetGrade = a.TargetGrade ?? "All Grades",
                    TargetClass = a.TargetClass ?? "All Classes",
                    AnnouncementType = a.AnnouncementType,
                    CreatedByName = "Admin",
                    CreatedAt = a.CreatedAt,
                    ExpiryDate = a.ExpiryDate,
                    IsActive = a.IsActive,
                    TypeBadgeClass = GetAnnouncementTypeBadgeClass(a.AnnouncementType),
                    AudienceBadgeClass = GetAnnouncementAudienceBadgeClass(a.TargetAudience)
                })
                .ToList();

            ViewBag.StudentName = student.FullName;
            ViewBag.StudentGrade = student.Grade;
            ViewBag.ClassName = student.ClassName;

            return View(announcements);
        }

        // ============================================
        // ISSUE REPORTING
        // ============================================

        public ActionResult ReportIssue()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportIssue(CreateIssueViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please fill all required fields" });
            }

            try
            {
                var studentNumber = User.Identity.Name;
                var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                string attachmentPath = null;
                if (model.Attachment != null && model.Attachment.ContentLength > 0)
                {
                    if (model.Attachment.ContentLength > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "File size exceeds 5MB limit" });
                    }

                    string uploadPath = Server.MapPath("~/Uploads/Issues/");
                    if (!System.IO.Directory.Exists(uploadPath))
                        System.IO.Directory.CreateDirectory(uploadPath);

                    string extension = System.IO.Path.GetExtension(model.Attachment.FileName);
                    string fileName = $"Issue_{student.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.Attachment.SaveAs(fullPath);
                    attachmentPath = "/Uploads/Issues/" + fileName;
                }

                var issue = new Issue
                {
                    StudentId = student.Id,
                    Title = model.Title,
                    Description = model.Description,
                    Category = model.Category,
                    Priority = IssuePriority.Medium,
                    Status = IssueStatus.Pending,
                    CreatedAt = DateTime.Now,
                    IsAnonymous = model.IsAnonymous,
                    AttachmentsPath = attachmentPath
                };

                _context.Issues.Add(issue);
                _context.SaveChanges();

                return Json(new { success = true, message = "Issue reported successfully! Admin will review it shortly." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult MyIssues()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var issues = _context.Issues
                .Where(i => i.StudentId == student.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToList()
                .Select(i => new MyIssueViewModel
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description.Length > 100 ? i.Description.Substring(0, 100) + "..." : i.Description,
                    Category = i.Category,
                    Priority = i.Priority,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt,
                    IsAnonymous = i.IsAnonymous
                })
                .ToList();

            return View(issues);
        }

        public ActionResult IssueDetail(int id)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var issue = _context.Issues.Find(id);
            if (issue == null || issue.StudentId != student.Id)
            {
                return HttpNotFound();
            }

            var viewModel = new IssueDetailViewModel
            {
                Id = issue.Id,
                Title = issue.Title,
                Description = issue.Description,
                Category = issue.Category,
                Priority = issue.Priority,
                Status = issue.Status,
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt,
                ResolvedAt = issue.ResolvedAt,
                IsAnonymous = issue.IsAnonymous,
                ResolutionNotes = issue.ResolutionNotes,
                AdminResponse = issue.AdminResponse,
                AttachmentsPath = issue.AttachmentsPath
            };

            return View(viewModel);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

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

        private string GetSubjectIcon(string subject)
        {
            switch (subject.ToLower())
            {
                case "english": return "fas fa-language";
                case "isizulu": return "fas fa-comments";
                case "mathematics": return "fas fa-calculator";
                case "mathematical literacy": return "fas fa-calculator";
                case "life orientation": return "fas fa-heart";
                case "physical sciences": return "fas fa-atom";
                case "life sciences": return "fas fa-dna";
                case "natural science": return "fas fa-flask";
                case "agricultural sciences": return "fas fa-tractor";
                case "history": return "fas fa-landmark";
                case "geography": return "fas fa-map";
                case "social science": return "fas fa-globe";
                case "information technology": return "fas fa-laptop-code";
                case "computer applications technology": return "fas fa-desktop";
                case "technology": return "fas fa-microchip";
                case "creative arts": return "fas fa-palette";
                default: return "fas fa-book";
            }
        }

        private string GetSubjectColor(string subject)
        {
            var colors = new[] { "primary", "info", "success", "warning", "danger", "secondary" };
            var hash = subject.GetHashCode();
            return colors[Math.Abs(hash) % colors.Length];
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
        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .Include(s => s.Stream)
                .FirstOrDefault(s => s.UserId == user.Id);
        }
        // ============================================
        // PLACEHOLDER ACTIONS
        // ============================================

        public ActionResult Assignments()
        {
            return View();
        }

        public ActionResult GroupChat()
        {
            return View();
        }
        // ============================================
        // HOMEWORK SUBMISSION
        // ============================================

        [HttpGet]
        public ActionResult SubmitHomework(int id)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found.";
                return RedirectToAction("Homework");
            }

            var homework = _context.Homeworks
                .Include(h => h.Subject)
                .Include(h => h.Class)
                .FirstOrDefault(h => h.Id == id && h.IsActive);

            if (homework == null)
            {
                return HttpNotFound();
            }

            // Verify student is in the correct class
            if (student.ClassId != homework.ClassId)
            {
                TempData["ErrorMessage"] = "You are not authorized to submit to this homework.";
                return RedirectToAction("Homework");
            }

            bool isLate = homework.DueDate < DateTime.Now;

            var existingSubmission = _context.HomeworkSubmissions
                .FirstOrDefault(s => s.HomeworkId == id && s.StudentId == student.Id);

            var viewModel = new SubmitHomeworkViewModel
            {
                HomeworkId = homework.Id,
                Title = homework.Title,
                Instructions = homework.Instructions,
                Subject = homework.SubjectDisplay,
                DueDate = homework.DueDate,
                IsLate = isLate,
                ExistingSubmission = existingSubmission,
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitHomework(SubmitHomeworkViewModel model)
        {
            try
            {
                var studentNumber = User.Identity.Name;
                var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var homework = _context.Homeworks.Find(model.HomeworkId);
                if (homework == null)
                {
                    return Json(new { success = false, message = "Homework not found" });
                }

                if (student.ClassId != homework.ClassId)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                if (model.SubmissionFile == null || model.SubmissionFile.ContentLength == 0)
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                bool isLate = homework.DueDate < DateTime.Now;

                // Create upload directory
                string uploadPath = Server.MapPath("~/Uploads/Submissions/Homework/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                // Validate file size (10MB max)
                if (model.SubmissionFile.ContentLength > 10 * 1024 * 1024)
                    return Json(new { success = false, message = "File size exceeds 10MB limit" });

                // Validate file extension
                string extension = System.IO.Path.GetExtension(model.SubmissionFile.FileName).ToLower();
                string[] allowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".jpeg", ".png" };

                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Invalid file type. Please upload PDF, Word, Excel, PowerPoint, or image files." });
                }

                // Save file
                string fileName = $"Homework_{homework.Id}_{student.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                model.SubmissionFile.SaveAs(fullPath);
                string filePath = "/Uploads/Submissions/Homework/" + fileName;

                // Check for existing submission
                var existingSubmission = _context.HomeworkSubmissions
                    .FirstOrDefault(s => s.HomeworkId == model.HomeworkId && s.StudentId == student.Id);

                if (existingSubmission == null)
                {
                    var submission = new HomeworkSubmission
                    {
                        HomeworkId = model.HomeworkId,
                        StudentId = student.Id,
                        Content = ".",
                        FilePath = filePath,
                        FileName = model.SubmissionFile.FileName,
                        FileType = model.SubmissionFile.ContentType,
                        FileSize = model.SubmissionFile.ContentLength,
                        SubmittedAt = DateTime.Now,
                        Status = isLate ? SubmissionStatus.Late : SubmissionStatus.Submitted,
                        IsLate = isLate
                    };
                    _context.HomeworkSubmissions.Add(submission);
                }
                else
                {
                    // Delete old file
                    if (!string.IsNullOrEmpty(existingSubmission.FilePath))
                    {
                        string oldFilePath = Server.MapPath(existingSubmission.FilePath);
                        if (System.IO.File.Exists(oldFilePath))
                            System.IO.File.Delete(oldFilePath);
                    }

                    existingSubmission.Content = ".";
                    existingSubmission.FilePath = filePath;
                    existingSubmission.FileName = model.SubmissionFile.FileName;
                    existingSubmission.FileType = model.SubmissionFile.ContentType;
                    existingSubmission.FileSize = model.SubmissionFile.ContentLength;
                    existingSubmission.SubmittedAt = DateTime.Now;
                    existingSubmission.Status = isLate ? SubmissionStatus.Late : SubmissionStatus.Submitted;
                    existingSubmission.IsLate = isLate;
                    existingSubmission.Grade = null;
                    existingSubmission.TeacherFeedback = null;
                    existingSubmission.GradedBy = null;
                    existingSubmission.GradedAt = null;

                    _context.Entry(existingSubmission).State = EntityState.Modified;
                }

                _context.SaveChanges();

                return Json(new { success = true, message = "Homework submitted successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // CLASSWORK SUBMISSION
        // ============================================

        [HttpGet]
        public ActionResult SubmitClasswork(int id)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student record not found.";
                return RedirectToAction("Classwork");
            }

            var classwork = _context.Classworks
                .Include(c => c.Subject)
                .Include(c => c.Class)
                .FirstOrDefault(c => c.Id == id && c.IsActive);

            if (classwork == null)
            {
                return HttpNotFound();
            }

            if (student.ClassId != classwork.ClassId)
            {
                TempData["ErrorMessage"] = "You are not authorized to submit to this classwork.";
                return RedirectToAction("Classwork");
            }

            var existingSubmission = _context.ClassworkSubmissions
                .FirstOrDefault(s => s.ClassworkId == id && s.StudentId == student.Id);

            var viewModel = new SubmitClassworkViewModel
            {
                ClassworkId = classwork.Id,
                Title = classwork.Title,
                Instructions = classwork.Instructions,
                Subject = classwork.SubjectDisplay,
                ExistingSubmission = existingSubmission,
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitClasswork(SubmitClassworkViewModel model)
        {
            try
            {
                var studentNumber = User.Identity.Name;
                var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var classwork = _context.Classworks.Find(model.ClassworkId);
                if (classwork == null)
                {
                    return Json(new { success = false, message = "Classwork not found" });
                }

                if (student.ClassId != classwork.ClassId)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                if (model.SubmissionFile == null || model.SubmissionFile.ContentLength == 0)
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                string uploadPath = Server.MapPath("~/Uploads/Submissions/Classwork/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                if (model.SubmissionFile.ContentLength > 10 * 1024 * 1024)
                    return Json(new { success = false, message = "File size exceeds 10MB limit" });

                string extension = System.IO.Path.GetExtension(model.SubmissionFile.FileName).ToLower();
                string[] allowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".jpeg", ".png" };

                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Invalid file type" });
                }

                string fileName = $"Classwork_{classwork.Id}_{student.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                model.SubmissionFile.SaveAs(fullPath);
                string filePath = "/Uploads/Submissions/Classwork/" + fileName;

                var existingSubmission = _context.ClassworkSubmissions
                    .FirstOrDefault(s => s.ClassworkId == model.ClassworkId && s.StudentId == student.Id);

                if (existingSubmission == null)
                {
                    var submission = new ClassworkSubmission
                    {
                        ClassworkId = model.ClassworkId,
                        StudentId = student.Id,
                        Content = ".",
                        FilePath = filePath,
                        FileName = model.SubmissionFile.FileName,
                        FileType = model.SubmissionFile.ContentType,
                        FileSize = model.SubmissionFile.ContentLength,
                        SubmittedAt = DateTime.Now,
                        Status = SubmissionStatus.Submitted,
                        IsLate = false
                    };
                    _context.ClassworkSubmissions.Add(submission);
                }
                else
                {
                    if (!string.IsNullOrEmpty(existingSubmission.FilePath))
                    {
                        string oldFilePath = Server.MapPath(existingSubmission.FilePath);
                        if (System.IO.File.Exists(oldFilePath))
                            System.IO.File.Delete(oldFilePath);
                    }

                    existingSubmission.Content = ".";
                    existingSubmission.FilePath = filePath;
                    existingSubmission.FileName = model.SubmissionFile.FileName;
                    existingSubmission.FileType = model.SubmissionFile.ContentType;
                    existingSubmission.FileSize = model.SubmissionFile.ContentLength;
                    existingSubmission.SubmittedAt = DateTime.Now;
                    existingSubmission.Status = SubmissionStatus.Submitted;
                    existingSubmission.Grade = null;
                    existingSubmission.TeacherFeedback = null;
                    existingSubmission.GradedBy = null;
                    existingSubmission.GradedAt = null;

                    _context.Entry(existingSubmission).State = EntityState.Modified;
                }

                _context.SaveChanges();

                return Json(new { success = true, message = "Classwork submitted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // VIEW MY SUBMISSIONS
        // ============================================

        public ActionResult MyHomeworkSubmissions()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var submissions = _context.HomeworkSubmissions
                .Include(s => s.Homework)
                .Include(s => s.Homework.Subject)
                .Where(s => s.StudentId == student.Id)
                .OrderByDescending(s => s.SubmittedAt)
                .ToList()
                .Select(s => new StudentHomeworkSubmissionViewModel
                {
                    Id = s.Id,
                    HomeworkTitle = s.Homework?.Title ?? "N/A",
                    Subject = s.Homework?.SubjectDisplay ?? "N/A",
                    SubmittedAt = s.SubmittedAt,
                    DueDate = s.Homework?.DueDate ?? DateTime.Now,
                    Grade = s.Grade,
                    TeacherFeedback = s.TeacherFeedback,
                    Status = s.Status,
                    IsLate = s.IsLate,
                    FilePath = s.FilePath
                })
                .ToList();

            return View(submissions);
        }

        public ActionResult MyClassworkSubmissions()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var submissions = _context.ClassworkSubmissions
                .Include(s => s.Classwork)
                .Include(s => s.Classwork.Subject)
                .Where(s => s.StudentId == student.Id)
                .OrderByDescending(s => s.SubmittedAt)
                .ToList()
                .Select(s => new StudentClassworkSubmissionViewModel
                {
                    Id = s.Id,
                    ClassworkTitle = s.Classwork?.Title ?? "N/A",
                    Subject = s.Classwork?.SubjectDisplay ?? "N/A",
                    SubmittedAt = s.SubmittedAt,
                    Grade = s.Grade,
                    TeacherFeedback = s.TeacherFeedback,
                    Status = s.Status,
                    FilePath = s.FilePath
                })
                .ToList();

            return View(submissions);
        }

        [HttpGet]
        public ActionResult DownloadSubmissionFile(int id, string type)
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (type == "homework")
            {
                var submission = _context.HomeworkSubmissions
                    .FirstOrDefault(s => s.Id == id && s.StudentId == student.Id);

                if (submission == null || string.IsNullOrEmpty(submission.FilePath))
                {
                    return HttpNotFound();
                }

                string filePath = Server.MapPath(submission.FilePath);
                if (!System.IO.File.Exists(filePath))
                {
                    return HttpNotFound();
                }

                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, submission.FileType ?? "application/octet-stream", submission.FileName ?? "submission");
            }
            else if (type == "classwork")
            {
                var submission = _context.ClassworkSubmissions
                    .FirstOrDefault(s => s.Id == id && s.StudentId == student.Id);

                if (submission == null || string.IsNullOrEmpty(submission.FilePath))
                {
                    return HttpNotFound();
                }

                string filePath = Server.MapPath(submission.FilePath);
                if (!System.IO.File.Exists(filePath))
                {
                    return HttpNotFound();
                }

                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, submission.FileType ?? "application/octet-stream", submission.FileName ?? "submission");
            }

            return HttpNotFound();
        }

        // Add to StudentController.cs
        // ============================================
        // EXAM TIMETABLE - STUDENT VIEW
        // ============================================

        public ActionResult ExamTimetable()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            // Find the active distributed timetable
            var activeTimetable = _context.ExamTimetables
                .FirstOrDefault(t => t.Status == ExamTimetableStatus.Distributed && t.IsActive && t.EndDate >= DateTime.Now);

            if (activeTimetable == null)
            {
                ViewBag.NoTimetable = true;
                return View(new StudentExamTimetableViewModel());
            }

            // Get the student's grade
            var grade = _context.Grades.FirstOrDefault(g => g.Name == student.Grade);
            if (grade == null)
            {
                ViewBag.NoTimetable = true;
                return View(new StudentExamTimetableViewModel());
            }

            var sessions = _examTimetableService.GetExamSessionsForStudent(
                activeTimetable.Id,
                grade.Id,
                student.StreamId,
                student.ClassId);

            var viewModel = new StudentExamTimetableViewModel
            {
                Id = activeTimetable.Id,
                Name = activeTimetable.Name,
                AcademicYear = activeTimetable.AcademicYear,
                NumberOfWeeks = activeTimetable.NumberOfWeeks,
                StartDate = activeTimetable.StartDate,
                EndDate = activeTimetable.EndDate,
                StudentName = student.FullName,
                GradeName = student.Grade ?? "Not Assigned",
                StreamName = student.StreamChoice ?? "General",
                ExamSessions = sessions.Select(s => new ExamSessionViewModel
                {
                    Id = s.Id,
                    SubjectName = s.Subject?.Name ?? "Unknown",
                    GradeName = s.Grade?.Name ?? "Unknown",
                    ClassNames = s.ExamSessionClasses != null && s.ExamSessionClasses.Any()
                        ? string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(c => !string.IsNullOrEmpty(c)))
                        : student.ClassName,
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
                            : student.ClassName,
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
    }
}
