using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.ViewModels;
using OfficeOpenXml;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

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
                PendingTasks = 3,
                UnreadMessages = 2,
                RecentAnnouncements = GetRecentAnnouncements(),
                TodaySchedule = GetSampleSchedule()
            };

            return View(viewModel);
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
        // ANNOUNCEMENTS
        // ============================================

        public ActionResult SendAnnouncement()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var teacherGrades = _context.TeacherSubjectAssignments
                .Include(a => a.Class.Grade)
                .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                .Select(a => a.Class.Grade)
                .Distinct()
                .OrderBy(g => g.Level)
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.TeacherGrades = teacherGrades;
            ViewBag.AnnouncementTypes = new List<string> { "General", "Important", "Urgent", "Event" };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendAnnouncement(SendAnnouncementViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                var announcement = new Announcement
                {
                    Title = model.Title,
                    Content = model.Content,
                    TargetAudience = "Students Only",
                    TargetGrade = model.TargetGrade,
                    TargetClass = model.TargetClass,
                    AnnouncementType = model.AnnouncementType,
                    CreatedBy = teacher.UserId,
                    CreatedAt = DateTime.Now,
                    ExpiryDate = model.ExpiryDate,
                    IsActive = true
                };

                _context.Announcements.Add(announcement);
                _context.SaveChanges();

                return Json(new { success = true, message = "Announcement sent to students successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewAnnouncements()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var currentDate = DateTime.Now;

            var announcements = _context.Announcements
                .Where(a => a.IsActive &&
                    (a.TargetAudience == "All Users" || a.TargetAudience == "Teachers Only") &&
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

            ViewBag.TeacherName = teacher.FullName;
            return View(announcements);
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
    }
}