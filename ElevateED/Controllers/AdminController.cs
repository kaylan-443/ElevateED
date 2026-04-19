using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using static ElevateED.ViewModels.TeacherRegistrationViewModel;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private EmailService _emailService = new EmailService();

        // ============================================
        // DASHBOARD
        // ============================================

        public ActionResult Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var activeCycle = _context.ApplicationCycles.FirstOrDefault(c => c.IsActive);

            var pendingApplications = _context.Applicants
                .Where(a => a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.UnderReview)
                .OrderBy(a => a.ApplicationDate)
                .ToList();

            var stats = new DashboardStats
            {
                Total = _context.Applicants.Count(),
                Pending = _context.Applicants.Count(a => a.Status == ApplicationStatus.Pending),
                UnderReview = _context.Applicants.Count(a => a.Status == ApplicationStatus.UnderReview),
                Approved = _context.Applicants.Count(a => a.Status == ApplicationStatus.Approved),
                Rejected = _context.Applicants.Count(a => a.Status == ApplicationStatus.Rejected)
            };

            var viewModel = new AdminDashboardViewModel
            {
                PendingApplications = pendingApplications,
                Stats = stats,
                ActiveCycle = activeCycle
            };

            return View(viewModel);
        }

        // ============================================
        // APPLICATION CYCLE MANAGEMENT (NEW)
        // ============================================

        public ActionResult ApplicationCycles()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var cycles = _context.ApplicationCycles
                .OrderByDescending(c => c.AcademicYear)
                .ToList();

            return View(cycles);
        }

        public ActionResult CreateApplicationCycle()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateApplicationCycle(ApplicationCycle model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (ModelState.IsValid)
            {
                try
                {
                    if (model.IsActive)
                    {
                        var activeCycles = _context.ApplicationCycles.Where(c => c.IsActive);
                        foreach (var cycle in activeCycles)
                            cycle.IsActive = false;
                    }

                    _context.ApplicationCycles.Add(model);
                    _context.SaveChanges();

                    return Json(new { success = true, message = "Application cycle created successfully!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }

            var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Json(new { success = false, message = "Validation failed: " + errors });
        }

        [HttpPost]
        public ActionResult ToggleCycleStatus(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var cycle = _context.ApplicationCycles.Find(id);
            if (cycle == null)
                return Json(new { success = false, message = "Cycle not found" });

            if (!cycle.IsActive)
            {
                var activeCycles = _context.ApplicationCycles.Where(c => c.IsActive && c.Id != id);
                foreach (var c in activeCycles)
                    c.IsActive = false;
            }

            cycle.IsActive = !cycle.IsActive;
            _context.SaveChanges();

            return Json(new { success = true, message = "Status updated" });
        }

        [HttpPost]
        public ActionResult DeleteApplicationCycle(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var cycle = _context.ApplicationCycles.Find(id);
            if (cycle == null)
                return Json(new { success = false, message = "Cycle not found" });

            var hasApplications = _context.Applicants.Any(a => a.ApplicationCycleId == id);
            if (hasApplications)
                return Json(new { success = false, message = "Cannot delete cycle with existing applications" });

            _context.ApplicationCycles.Remove(cycle);
            _context.SaveChanges();

            return Json(new { success = true, message = "Cycle deleted successfully" });
        }

        // ============================================
        // BULK APPLICATION EVALUATION (NEW)
        // ============================================

        public ActionResult EvaluateApplications(int? cycleId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var cycle = cycleId.HasValue
                ? _context.ApplicationCycles.Find(cycleId)
                : _context.ApplicationCycles.FirstOrDefault(c => c.IsActive);

            if (cycle == null)
            {
                TempData["ErrorMessage"] = "No active application cycle found. Please create one first.";
                return RedirectToAction("ApplicationCycles");
            }

            var applicants = _context.Applicants
                .Include(a => a.User)
                .Include(a => a.GradeApplyingFor)
                .Where(a => a.ApplicationCycleId == cycle.Id && a.Status == ApplicationStatus.Pending)
                .OrderByDescending(a => a.AcademicAverage)
                .ToList();

            var viewModel = new ApplicationEvaluationViewModel
            {
                Cycle = cycle,
                Applicants = applicants,
                GradeGroups = applicants.GroupBy(a => a.GradeApplyingFor?.Name ?? "Unknown").ToList()
            };

            ViewBag.AvailableClasses = _context.Classes
                .Include(c => c.Grade)
                .OrderBy(c => c.Grade.Level)
                .ThenBy(c => c.Name)
                .ToList();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkApprove(BulkApprovalViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var applicantIds = model.SelectedApplicantIds?.Split(',').Select(int.Parse).ToList() ?? new List<int>();

                if (!applicantIds.Any())
                    return Json(new { success = false, message = "No applicants selected" });

                int approvedCount = 0;

                foreach (var id in applicantIds)
                {
                    var applicant = _context.Applicants.Find(id);
                    if (applicant != null && applicant.Status == ApplicationStatus.Pending)
                    {
                        applicant.Status = ApplicationStatus.Approved;
                        applicant.ReviewDate = DateTime.Now;
                        applicant.ReviewedBy = User.Identity.Name;

                        var user = _context.Users.Find(applicant.UserId);
                        if (user != null)
                        {
                            user.Role = UserRole.Student;
                        }

                        var existingStudent = _context.Students.FirstOrDefault(s => s.ApplicantId == applicant.Id);
                        if (existingStudent == null)
                        {
                            var student = CreateStudentFromApplicant(applicant);

                            // AUTO-ALLOCATE
                            var classId = AutoAllocateStudentToClass(student);
                            student.ClassId = classId;

                            _context.Students.Add(student);
                        }

                        approvedCount++;
                    }
                }

                _context.SaveChanges();

                return Json(new { success = true, message = $"{approvedCount} applications approved and auto-allocated!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkReject(BulkApprovalViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var applicantIds = model.SelectedApplicantIds?.Split(',').Select(int.Parse).ToList() ?? new List<int>();

                if (!applicantIds.Any())
                    return Json(new { success = false, message = "No applicants selected" });

                int rejectedCount = 0;
                foreach (var id in applicantIds)
                {
                    var applicant = _context.Applicants.Find(id);
                    if (applicant != null && applicant.Status == ApplicationStatus.Pending)
                    {
                        applicant.Status = ApplicationStatus.Rejected;
                        applicant.ReviewDate = DateTime.Now;
                        applicant.ReviewedBy = User.Identity.Name;
                        applicant.RejectionReason = model.RejectionReason ?? "Application not successful";

                        rejectedCount++;

                        try
                        {
                            var user = _context.Users.Find(applicant.UserId);
                            if (user != null)
                            {
                                _emailService.SendApplicationRejectedEmail(
                                    user.Email,
                                    applicant.FullName,
                                    user.StudentNumber,
                                    model.RejectionReason);
                            }
                        }
                        catch { }
                    }
                }

                _context.SaveChanges();

                return Json(new { success = true, message = $"{rejectedCount} applications rejected." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private Student CreateStudentFromApplicant(Applicant applicant)
        {
            return new Student
            {
                UserId = applicant.UserId,
                ApplicantId = applicant.Id,
                FirstName = applicant.FirstName,
                LastName = applicant.LastName,
                DateOfBirth = applicant.DateOfBirth,
                Gender = applicant.Gender,
                IdentityNumber = applicant.IdentityNumber,
                Nationality = applicant.Nationality,
                HomeLanguage = applicant.HomeLanguage,
                CellPhone = applicant.CellPhone,
                AlternativePhone = applicant.AlternativePhone,
                PhysicalAddress = applicant.PhysicalAddress,
                PostalAddress = applicant.PostalAddress,
                StreamId = applicant.StreamId,
                ParentName = applicant.ParentName,
                ParentIdNumber = applicant.ParentIdNumber,
                ParentRelationship = applicant.ParentRelationship,
                ParentCellPhone = applicant.ParentCellPhone,
                ParentEmail = applicant.ParentEmail,
                ParentWorkPhone = applicant.ParentWorkPhone,
                ParentOccupation = applicant.ParentOccupation,
                ParentEmployer = applicant.ParentEmployer,
                ParentWorkAddress = applicant.ParentWorkAddress,
                EmergencyContactName = applicant.EmergencyContactName,
                EmergencyContactPhone = applicant.EmergencyContactPhone,
                EmergencyContactRelationship = applicant.EmergencyContactRelationship,
                MedicalConditions = applicant.MedicalConditions,
                Allergies = applicant.Allergies,
                CurrentMedication = applicant.CurrentMedication,
                DoctorName = applicant.DoctorName,
                DoctorPhone = applicant.DoctorPhone,
                MedicalAidName = applicant.MedicalAidName,
                MedicalAidNumber = applicant.MedicalAidNumber,
                IdDocumentPath = applicant.IdDocumentPath,
                ReportCardPath = applicant.ReportCardPath,
                TransferCertificatePath = applicant.TransferCertificatePath,
                ProofOfResidencePath = applicant.ProofOfResidencePath,
                ParentIdDocumentPath = applicant.ParentIdDocumentPath,
                IsActive = true,
                EnrollmentDate = DateTime.Now
            };
        }

        // ============================================
        // CLASS ALLOCATION
        // ============================================

        public ActionResult AllocateStudents(int? gradeId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var query = _context.Students
                .Include(s => s.User)
                .Include(s => s.Stream)
                .Include(s => s.GradeApplyingFor)
                .Where(s => s.IsActive)
                .Where(s => s.ClassId == null);

            if (gradeId.HasValue)
            {
                query = query.Where(s => s.GradeApplyingForId == gradeId.Value);
            }

            var unallocatedStudents = query
                .OrderBy(s => s.GradeApplyingForId)
                .ThenBy(s => s.LastName)
                .ToList();

            var classes = _context.Classes
                .Include(c => c.Grade)
                .Include(c => c.ClassTeacher)
                .OrderBy(c => c.Grade.Level)
                .ThenBy(c => c.Name)
                .ToList();

            var grades = _context.Grades.OrderBy(g => g.Level).ToList();

            // Calculate student counts for each class
            var classStudentCounts = new Dictionary<int, int>();
            foreach (var cls in classes)
            {
                classStudentCounts[cls.Id] = _context.Students.Count(s => s.ClassId == cls.Id);
            }

            var viewModel = new ClassAllocationViewModel
            {
                UnallocatedStudents = unallocatedStudents,
                AvailableClasses = classes,
                Grades = grades,
                SelectedGradeId = gradeId,
                ClassStudentCounts = classStudentCounts  // ADD THIS
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AutoAllocateStudents(int? gradeId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var query = _context.Students
                    .Include(s => s.GradeApplyingFor)
                    .Where(s => s.ClassId == null && s.IsActive);  // Unallocated only

                if (gradeId.HasValue)
                {
                    query = query.Where(s => s.GradeApplyingForId == gradeId.Value);
                }

                var students = query.OrderBy(s => s.LastName).ToList();

                if (!students.Any())
                    return Json(new { success = false, message = "No unallocated students found" });

                var classesQuery = _context.Classes
                    .Include(c => c.Grade)
                    .AsQueryable();

                if (gradeId.HasValue)
                {
                    classesQuery = classesQuery.Where(c => c.GradeId == gradeId.Value);
                }

                var classes = classesQuery.OrderBy(c => c.Name).ToList();

                if (!classes.Any())
                    return Json(new { success = false, message = "No classes available for allocation" });

                int allocatedCount = 0;
                var skippedStudents = new List<string>();

                // Group students by their grade
                var studentsByGrade = students.GroupBy(s => s.GradeApplyingForId);

                foreach (var gradeGroup in studentsByGrade)
                {
                    var gradeClasses = classes.Where(c => c.GradeId == gradeGroup.Key).ToList();

                    if (!gradeClasses.Any())
                    {
                        foreach (var s in gradeGroup)
                            skippedStudents.Add($"{s.FullName} (No class for this grade)");
                        continue;
                    }

                    int classIndex = 0;
                    foreach (var student in gradeGroup)
                    {
                        bool allocated = false;

                        // Try each class in round-robin fashion
                        for (int i = 0; i < gradeClasses.Count; i++)
                        {
                            var targetClass = gradeClasses[(classIndex + i) % gradeClasses.Count];
                            var currentCount = _context.Students.Count(s => s.ClassId == targetClass.Id);

                            if (currentCount < targetClass.Capacity)
                            {
                                student.ClassId = targetClass.Id;
                                student.UpdatedAt = DateTime.Now;
                                allocatedCount++;
                                classIndex = (classIndex + i + 1) % gradeClasses.Count;
                                allocated = true;
                                break;
                            }
                        }

                        if (!allocated)
                        {
                            skippedStudents.Add($"{student.FullName} (All classes full)");
                        }
                    }
                }

                _context.SaveChanges();

                var message = $"{allocatedCount} students allocated successfully!";
                if (skippedStudents.Any())
                {
                    message += $"\n\nSkipped ({skippedStudents.Count}):\n";
                    message += string.Join("\n", skippedStudents.Take(10));
                    if (skippedStudents.Count > 10)
                        message += $"\n... and {skippedStudents.Count - 10} more";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ManuallyAllocateStudent(int studentId, int classId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var student = _context.Students.Find(studentId);
            var targetClass = _context.Classes.Find(classId);

            if (student == null || targetClass == null)
                return Json(new { success = false, message = "Student or class not found" });

            var currentCount = _context.Students.Count(s => s.ClassId == classId);
            if (currentCount >= targetClass.Capacity)
                return Json(new { success = false, message = "Class is at full capacity" });

            student.ClassId = classId;
            student.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            return Json(new { success = true, message = $"{student.FullName} allocated to {targetClass.FullName}" });
        }

        // ============================================
        // SEED DATA (NEW)
        // ============================================

        public ActionResult SeedData()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SeedGradesAndSubjects();
            TempData["SuccessMessage"] = "Grades, Subjects, Classes, and Streams have been seeded successfully!";
            return RedirectToAction("Dashboard");
        }

        private void SeedGradesAndSubjects()
        {
            // Create Grades if none exist
            if (!_context.Grades.Any())
            {
                var grades = new List<Grade>
                {
                    new Grade { Name = "Grade 8", Level = 8 },
                    new Grade { Name = "Grade 9", Level = 9 },
                    new Grade { Name = "Grade 10", Level = 10 },
                    new Grade { Name = "Grade 11", Level = 11 },
                    new Grade { Name = "Grade 12", Level = 12 }
                };
                _context.Grades.AddRange(grades);
                _context.SaveChanges();
            }

            // Create Subjects if none exist
            if (!_context.Subjects.Any())
            {
                var subjects = new List<Subject>
                {
                    // Core Subjects
                    new Subject { Name = "English (First Additional Language)", Code = "ENG", Category = SubjectCategory.Core },
                    new Subject { Name = "isiZulu (Home Language)", Code = "ZUL", Category = SubjectCategory.Core },
                    new Subject { Name = "Life Orientation", Code = "LO", Category = SubjectCategory.Core },
                    new Subject { Name = "Mathematics", Code = "MATH", Category = SubjectCategory.Core },
                    
                    // Grade 8-9 Specific
                    new Subject { Name = "Natural Science", Code = "NSCI", Category = SubjectCategory.Core },
                    new Subject { Name = "Social Science", Code = "SSCI", Category = SubjectCategory.Core },
                    new Subject { Name = "Creative Arts", Code = "CART", Category = SubjectCategory.Core },
                    new Subject { Name = "Economic Management Science", Code = "EMS", Category = SubjectCategory.Core },
                    new Subject { Name = "Technology", Code = "TECH", Category = SubjectCategory.Core },
                    
                    // Elective Subjects (Grade 10-12)
                    new Subject { Name = "Mathematical Literacy", Code = "MLIT", Category = SubjectCategory.Elective },
                    new Subject { Name = "Physical Sciences", Code = "PHYS", Category = SubjectCategory.Elective },
                    new Subject { Name = "Life Sciences", Code = "LIFE", Category = SubjectCategory.Elective },
                    new Subject { Name = "History", Code = "HIST", Category = SubjectCategory.Elective },
                    new Subject { Name = "Geography", Code = "GEOG", Category = SubjectCategory.Elective },
                    new Subject { Name = "Accounting", Code = "ACCT", Category = SubjectCategory.Elective },
                    new Subject { Name = "Business Studies", Code = "BSTD", Category = SubjectCategory.Elective },
                    new Subject { Name = "Economics", Code = "ECON", Category = SubjectCategory.Elective },
                    new Subject { Name = "Agricultural Sciences", Code = "AGRI", Category = SubjectCategory.Elective },
                    
                    // Technology Subjects
                    new Subject { Name = "Computer Applications Technology", Code = "CAT", Category = SubjectCategory.Technology },
                    new Subject { Name = "Information Technology", Code = "IT", Category = SubjectCategory.Technology }
                };
                _context.Subjects.AddRange(subjects);
                _context.SaveChanges();
            }

            // Create Classes for each grade if none exist
            if (!_context.Classes.Any())
            {
                var grades = _context.Grades.ToList();
                foreach (var grade in grades)
                {
                    var classCount = (grade.Level == 8 || grade.Level == 9) ? 2 : 3;
                    for (int i = 0; i < classCount; i++)
                    {
                        var className = ((char)('A' + i)).ToString();
                        _context.Classes.Add(new Class
                        {
                            Name = className,
                            FullName = $"{grade.Name} {className}",
                            GradeId = grade.Id,
                            Capacity = 35
                        });
                    }
                }
                _context.SaveChanges();
            }

            // Create Streams if none exist
            if (!_context.Streams.Any())
            {
                var streams = new List<Stream>
                {
                    new Stream { Name = "Mathematics, Life Science & Physics", Description = "Science stream with Physics" },
                    new Stream { Name = "Mathematics, Life Science & Agriculture", Description = "Science stream with Agriculture" },
                    new Stream { Name = "Mathematical Literacy, History & Geography", Description = "Commerce/Humanities stream" }
                };
                _context.Streams.AddRange(streams);
                _context.SaveChanges();
            }
        }

        // ============================================
        // ANNOUNCEMENTS MANAGEMENT
        // ============================================

        public ActionResult SendAnnouncement()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).Select(g => g.Name).ToList();
            ViewBag.Grades.Insert(0, "All Grades");
            ViewBag.TargetAudiences = new List<string> { "All Users", "Students Only", "Teachers Only", "Parents Only" };
            ViewBag.AnnouncementTypes = new List<string> { "General", "Important", "Urgent", "Event" };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendAnnouncement(SendAnnouncementViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                var adminUser = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
                if (adminUser == null)
                    return Json(new { success = false, message = "Admin not found" });

                string targetGrade = model.TargetGrade == "All Grades" ? null : model.TargetGrade;

                var announcement = new Announcement
                {
                    Title = model.Title,
                    Content = model.Content,
                    TargetAudience = model.TargetAudience,
                    TargetGrade = targetGrade,
                    TargetClass = model.TargetClass,
                    AnnouncementType = model.AnnouncementType,
                    CreatedBy = adminUser.Id,
                    CreatedAt = DateTime.Now,
                    ExpiryDate = model.ExpiryDate,
                    IsActive = true
                };

                _context.Announcements.Add(announcement);
                _context.SaveChanges();

                return Json(new { success = true, message = "Announcement sent successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewAnnouncements()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var announcements = _context.Announcements
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
                    AudienceBadgeClass = GetAudienceBadgeClass(a.TargetAudience)
                })
                .ToList();

            return View(announcements);
        }

        [HttpPost]
        public ActionResult DeleteAnnouncement(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var announcement = _context.Announcements.Find(id);
                if (announcement == null)
                    return Json(new { success = false, message = "Announcement not found" });

                _context.Announcements.Remove(announcement);
                _context.SaveChanges();

                return Json(new { success = true, message = "Announcement deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetClassesForGrade(string grade)
        {
            if (!IsAdmin()) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var gradeEntity = _context.Grades.FirstOrDefault(g => g.Name == grade);
            if (gradeEntity == null)
                return Json(new { success = false, classes = new List<object>() }, JsonRequestBehavior.AllowGet);

            var classes = _context.Classes
                .Where(c => c.GradeId == gradeEntity.Id)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, name = c.FullName })
                .ToList();

            return Json(new { success = true, classes = classes }, JsonRequestBehavior.AllowGet);
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

        private string GetAudienceBadgeClass(string audience)
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
        // APPLICATION MANAGEMENT (SINGLE)
        // ============================================

        public ActionResult ViewApplication(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants
                .Include(a => a.User)
                .Include(a => a.GradeApplyingFor)
                .Include(a => a.Stream)
                .FirstOrDefault(a => a.Id == id);

            if (applicant == null) return HttpNotFound();

            ViewBag.AvailableClasses = _context.Classes
                .Include(c => c.Grade)
                .Where(c => c.GradeId == applicant.GradeApplyingForId)
                .OrderBy(c => c.Name)
                .ToList();

            return View(applicant);
        }

        [HttpPost]
        public ActionResult Approve(int id, int classId = 0)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants.Find(id);
            if (applicant == null) return HttpNotFound();

            applicant.Status = ApplicationStatus.Approved;
            applicant.ReviewDate = DateTime.Now;
            applicant.ReviewedBy = User.Identity.Name;

            var user = _context.Users.Find(applicant.UserId);
            if (user != null) user.Role = UserRole.Student;

            var existingStudent = _context.Students.FirstOrDefault(s => s.ApplicantId == applicant.Id);

            if (existingStudent == null)
            {
                var student = CreateStudentFromApplicant(applicant);

                // AUTO-ALLOCATE if no specific class selected
                if (classId == 0)
                {
                    classId = AutoAllocateStudentToClass(student);
                }
                student.ClassId = classId;

                _context.Students.Add(student);
            }
            else
            {
                if (classId == 0)
                {
                    classId = AutoAllocateStudentToClass(existingStudent);
                }
                existingStudent.ClassId = classId;
                existingStudent.UpdatedAt = DateTime.Now;
            }

            _context.SaveChanges();

            // Send email with class info
            var assignedClass = _context.Classes.Find(classId);
            try
            {
                _emailService.SendApplicationApprovedEmail(
                    user.Email,
                    applicant.FullName,
                    user.StudentNumber,
                    assignedClass?.FullName ?? "To be assigned");
            }
            catch { }

            TempData["SuccessMessage"] = $"Application approved! Student enrolled in {assignedClass?.FullName ?? "a class"}.";
            return RedirectToAction("PendingApplications");
        }
        [HttpPost]
        public ActionResult Reject(int id, string rejectionReason)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants.Find(id);
            if (applicant == null) return HttpNotFound();

            applicant.Status = ApplicationStatus.Rejected;
            applicant.ReviewDate = DateTime.Now;
            applicant.ReviewedBy = User.Identity.Name;
            applicant.RejectionReason = rejectionReason;
            _context.SaveChanges();

            try
            {
                var user = _context.Users.Find(applicant.UserId);
                if (user != null)
                {
                    _emailService.SendApplicationRejectedEmail(user.Email, applicant.FullName, user.StudentNumber, rejectionReason);
                }
            }
            catch { }

            TempData["SuccessMessage"] = "Application rejected.";
            return RedirectToAction("PendingApplications");
        }

        [HttpPost]
        public ActionResult SetUnderReview(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants.Find(id);
            if (applicant != null)
            {
                applicant.Status = ApplicationStatus.UnderReview;
                _context.SaveChanges();
            }

            return RedirectToAction("Dashboard");
        }

        public ActionResult PendingApplications(string status, string search, string grade)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var applications = _context.Applicants
                .Include(a => a.User)
                .Include(a => a.GradeApplyingFor)
                .Where(a => a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.UnderReview)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Pending")
                    applications = applications.Where(a => a.Status == ApplicationStatus.Pending);
                else if (status == "UnderReview")
                    applications = applications.Where(a => a.Status == ApplicationStatus.UnderReview);
            }

            if (!string.IsNullOrEmpty(search))
            {
                applications = applications.Where(a =>
                    a.FirstName.Contains(search) ||
                    a.LastName.Contains(search) ||
                    a.ParentName.Contains(search) ||
                    (a.FirstName + " " + a.LastName).Contains(search));
            }

            if (!string.IsNullOrEmpty(grade))
            {
                var gradeEntity = _context.Grades.FirstOrDefault(g => g.Name == grade);
                if (gradeEntity != null)
                    applications = applications.Where(a => a.GradeApplyingForId == gradeEntity.Id);
            }

            return View(applications.OrderByDescending(a => a.AcademicAverage).ToList());
        }

        // ============================================
        // CLASS REGISTER MANAGEMENT
        // ============================================

        public ActionResult ClassRegister()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var grades = _context.Grades.OrderBy(g => g.Level).ToList();
            var gradeCards = new List<GradeCardViewModel>();

            foreach (var grade in grades)
            {
                var totalRegisters = _context.ClassRegisters.Count(r => r.Grade == grade.Name && r.IsActive);
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

                gradeCards.Add(new GradeCardViewModel { Grade = grade.Name, PaperCount = totalRegisters, Icon = icon, Color = color });
            }

            return View(gradeCards);
        }

        public ActionResult SelectClass(string grade)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var gradeEntity = _context.Grades.FirstOrDefault(g => g.Name == grade);
            if (gradeEntity == null) return HttpNotFound();

            var classes = _context.Classes
                .Where(c => c.GradeId == gradeEntity.Id)
                .OrderBy(c => c.Name)
                .Select(c => c.FullName)
                .ToList();

            ViewBag.Grade = grade;
            return View(classes);
        }

        public ActionResult UploadClassRegister(string grade, string className)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.Grade = grade;
            ViewBag.ClassName = className;
            ViewBag.Terms = new List<string> { "Term 1", "Term 2", "Term 3", "Term 4" };
            ViewBag.Years = GetYearsList();

            return View();
        }

        [HttpPost]
        public ActionResult UploadClassRegister(UploadClassRegisterViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                string uploadPath = Server.MapPath("~/Uploads/ClassRegisters/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                string fileName = null;
                if (model.RegisterFile != null && model.RegisterFile.ContentLength > 0)
                {
                    if (model.RegisterFile.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.RegisterFile.FileName);
                    fileName = $"ClassRegister_{model.Grade}_{model.ClassName}_{model.Term}_{model.Year}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.RegisterFile.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                var adminUser = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
                int uploadedBy = adminUser?.Id ?? 1;

                var classRegister = new ClassRegister
                {
                    Grade = model.Grade,
                    ClassName = model.ClassName,
                    Term = model.Term,
                    Year = model.Year,
                    FilePath = "/Uploads/ClassRegisters/" + fileName,
                    Description = model.Description,
                    UploadedBy = uploadedBy,
                    UploadedAt = DateTime.Now,
                    IsActive = true
                };

                _context.ClassRegisters.Add(classRegister);
                _context.SaveChanges();

                return Json(new { success = true, message = $"Class register for {model.ClassName} - {model.Term} {model.Year} uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult ViewClassRegisters(string grade, string className)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var registers = _context.ClassRegisters
                .Where(r => r.Grade == grade && r.ClassName == className && r.IsActive)
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
                })
                .ToList();

            ViewBag.Grade = grade;
            ViewBag.ClassName = className;
            return View(registers);
        }

        [HttpPost]
        public ActionResult DeleteClassRegister(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var register = _context.ClassRegisters.Find(id);
                if (register == null)
                    return Json(new { success = false, message = "Register not found" });

                string filePath = Server.MapPath(register.FilePath);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _context.ClassRegisters.Remove(register);
                _context.SaveChanges();

                return Json(new { success = true, message = "Class register deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // TEACHER MANAGEMENT
        // ============================================

        public ActionResult RegisterTeacher()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var teachers = _context.Teachers
                .Include(t => t.User)
                .Include(t => t.SubjectQualifications)
                .Include(t => t.GradeAssignments)
                .OrderBy(t => t.CreatedAt)
                .ToList();

            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();

            return View(teachers);
        }

        [HttpGet]
        public ActionResult GetTeacherDetails(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            var teacher = _context.Teachers
                .Include(t => t.SubjectQualifications)
                .Include(t => t.GradeAssignments)
                .FirstOrDefault(t => t.Id == id);

            if (teacher == null) return Json(new { success = false, message = "Teacher not found" }, JsonRequestBehavior.AllowGet);

            var user = _context.Users.Find(teacher.UserId);

            return Json(new
            {
                success = true,
                teacher = new
                {
                    id = teacher.Id,
                    firstName = teacher.FirstName,
                    middleName = teacher.MiddleName,
                    lastName = teacher.LastName,
                    identityNumber = teacher.IdentityNumber,
                    dateOfBirth = teacher.DateOfBirth.ToString("yyyy-MM-dd"),
                    email = user?.Email,
                    phoneNumber = teacher.PhoneNumber,
                    alternativePhone = teacher.AlternativePhone,
                    address = teacher.Address,
                    qualification = teacher.Qualification,
                    yearsOfExperience = teacher.YearsOfExperience,
                    emergencyContactName = teacher.EmergencyContactName,
                    emergencyContactPhone = teacher.EmergencyContactPhone,
                    subjectIds = teacher.SubjectQualifications.Select(sq => sq.SubjectId).ToList(),
                    gradeIds = teacher.GradeAssignments.Select(ga => ga.GradeId).ToList()
                }
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult AddTeacher(TeacherRegistrationViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                if (string.IsNullOrEmpty(model.FirstName) || string.IsNullOrEmpty(model.LastName))
                    return Json(new { success = false, message = "First and last name are required" });

                if (string.IsNullOrEmpty(model.Email))
                    return Json(new { success = false, message = "Email is required" });

                if (_context.Users.Any(u => u.Email == model.Email))
                    return Json(new { success = false, message = "A user with this email already exists" });

                var staffNumber = GenerateStaffNumber(model.IdentityNumber);
                var tempPassword = GenerateTempPassword();

                var user = new ApplicationUser
                {
                    StudentNumber = staffNumber,
                    Email = model.Email,
                    PasswordHash = HashPassword(tempPassword),
                    Role = UserRole.Teacher,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    HasChangedPassword = false
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                var teacher = new Teacher
                {
                    UserId = user.Id,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.LastName,
                    IdentityNumber = model.IdentityNumber,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    AlternativePhone = model.AlternativePhone,
                    Address = model.Address,
                    Qualification = model.Qualification,
                    YearsOfExperience = model.YearsOfExperience,
                    EmergencyContactName = model.EmergencyContactName,
                    EmergencyContactPhone = model.EmergencyContactPhone,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Teachers.Add(teacher);
                _context.SaveChanges();

                if (model.SubjectIds != null)
                {
                    foreach (var subjectId in model.SubjectIds)
                    {
                        _context.TeacherSubjectQualifications.Add(new TeacherSubjectQualification
                        {
                            TeacherId = teacher.Id,
                            SubjectId = subjectId
                        });
                    }
                }

                if (model.GradeIds != null)
                {
                    foreach (var gradeId in model.GradeIds)
                    {
                        _context.TeacherGradeAssignments.Add(new TeacherGradeAssignment
                        {
                            TeacherId = teacher.Id,
                            GradeId = gradeId
                        });
                    }
                }

                _context.SaveChanges();

                try
                {
                    var subjects = string.Join(", ", model.SubjectIds?.Select(id =>
                        _context.Subjects.Find(id)?.Name) ?? new List<string>());
                    var grades = string.Join(", ", model.GradeIds?.Select(id =>
                        _context.Grades.Find(id)?.Name) ?? new List<string>());

                    _emailService.SendTeacherRegistrationEmail(model.Email, teacher.FullName, staffNumber, tempPassword, grades, subjects);
                }
                catch { }

                return Json(new { success = true, message = "Teacher registered successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult UpdateTeacher(EditTeacherViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var teacher = _context.Teachers
                    .Include(t => t.SubjectQualifications)
                    .Include(t => t.GradeAssignments)
                    .FirstOrDefault(t => t.Id == model.Id);

                if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

                var user = _context.Users.Find(teacher.UserId);
                if (user == null) return Json(new { success = false, message = "User not found" });

                teacher.FirstName = model.FirstName;
                teacher.MiddleName = model.MiddleName;
                teacher.LastName = model.LastName;
                teacher.IdentityNumber = model.IdentityNumber;
                teacher.DateOfBirth = model.DateOfBirth;
                teacher.PhoneNumber = model.PhoneNumber;
                teacher.AlternativePhone = model.AlternativePhone;
                teacher.Address = model.Address;
                teacher.Qualification = model.Qualification;
                teacher.YearsOfExperience = model.YearsOfExperience;
                teacher.EmergencyContactName = model.EmergencyContactName;
                teacher.EmergencyContactPhone = model.EmergencyContactPhone;
                teacher.UpdatedAt = DateTime.Now;
                user.Email = model.Email;

                _context.TeacherSubjectQualifications.RemoveRange(teacher.SubjectQualifications);
                _context.TeacherGradeAssignments.RemoveRange(teacher.GradeAssignments);

                if (model.SubjectIds != null)
                {
                    foreach (var subjectId in model.SubjectIds)
                    {
                        _context.TeacherSubjectQualifications.Add(new TeacherSubjectQualification
                        {
                            TeacherId = teacher.Id,
                            SubjectId = subjectId
                        });
                    }
                }

                if (model.GradeIds != null)
                {
                    foreach (var gradeId in model.GradeIds)
                    {
                        _context.TeacherGradeAssignments.Add(new TeacherGradeAssignment
                        {
                            TeacherId = teacher.Id,
                            GradeId = gradeId
                        });
                    }
                }

                _context.SaveChanges();

                return Json(new { success = true, message = "Teacher updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DeleteTeacher(int id)
        {
            if (!IsAdmin()) return Json(new { success = false });

            var teacher = _context.Teachers
                .Include(t => t.SubjectQualifications)
                .Include(t => t.GradeAssignments)
                .FirstOrDefault(t => t.Id == id);

            if (teacher == null) return Json(new { success = false });

            _context.TeacherSubjectQualifications.RemoveRange(teacher.SubjectQualifications);
            _context.TeacherGradeAssignments.RemoveRange(teacher.GradeAssignments);

            var user = _context.Users.Find(teacher.UserId);
            _context.Teachers.Remove(teacher);
            if (user != null) _context.Users.Remove(user);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // ============================================
        // UPDATED CLASS STRUCTURE
        // ============================================

        public ActionResult ClassStructure()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var grades = _context.Grades
                .OrderBy(g => g.Level)
                .ToList();

            if (!grades.Any())
            {
                SeedGradesAndSubjects();
                grades = _context.Grades.OrderBy(g => g.Level).ToList();
            }

            var classes = _context.Classes
                .Include(c => c.Grade)
                .Include(c => c.ClassTeacher)
                .OrderBy(c => c.Grade.Level)
                .ThenBy(c => c.Name)
                .ToList();

            var teachers = _context.Teachers
                .Include(t => t.User)
                .Where(t => t.IsActive)
                .OrderBy(t => t.LastName)
                .ToList();

            var viewModel = new ClassStructureViewModel
            {
                Grades = grades,
                Classes = classes,
                Teachers = teachers
            };

            return View(viewModel);
        }

        public ActionResult ClassDetails(int classId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var classEntity = _context.Classes
                .Include(c => c.Grade)
                .Include(c => c.ClassTeacher)
                .FirstOrDefault(c => c.Id == classId);

            if (classEntity == null)
                return HttpNotFound();

            var subjects = GetSubjectsForGradeLevel(classEntity.Grade.Level);

            var assignments = _context.TeacherSubjectAssignments
                .Include(a => a.Teacher)
                .Include(a => a.Subject)
                .Where(a => a.ClassId == classId && a.IsActive)
                .ToList();

            var qualifiedTeachers = _context.Teachers
                .Include(t => t.SubjectQualifications)
                .Include(t => t.GradeAssignments)
                .Where(t => t.IsActive)
                .ToList();

            var viewModel = new ClassDetailsViewModel
            {
                Class = classEntity,
                Subjects = subjects,
                CurrentAssignments = assignments,
                QualifiedTeachers = qualifiedTeachers
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult AssignClassTeacher(int classId, int teacherId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var classEntity = _context.Classes.Find(classId);
                if (classEntity == null)
                    return Json(new { success = false, message = "Class not found" });

                classEntity.ClassTeacherId = teacherId;
                _context.SaveChanges();

                return Json(new { success = true, message = "Class teacher assigned successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult RemoveClassTeacher(int classId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var classEntity = _context.Classes.Find(classId);
                if (classEntity == null)
                    return Json(new { success = false, message = "Class not found" });

                classEntity.ClassTeacherId = null;
                _context.SaveChanges();

                return Json(new { success = true, message = "Class teacher removed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult AssignSubjectTeacher(int classId, int subjectId, int teacherId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var existingAssignment = _context.TeacherSubjectAssignments
                    .FirstOrDefault(a => a.ClassId == classId && a.SubjectId == subjectId && a.IsActive);

                if (existingAssignment != null)
                {
                    existingAssignment.TeacherId = teacherId;
                    existingAssignment.AssignedAt = DateTime.Now;
                }
                else
                {
                    var assignment = new TeacherSubjectAssignment
                    {
                        TeacherId = teacherId,
                        ClassId = classId,
                        SubjectId = subjectId,
                        AssignedAt = DateTime.Now,
                        IsActive = true
                    };
                    _context.TeacherSubjectAssignments.Add(assignment);
                }

                _context.SaveChanges();
                return Json(new { success = true, message = "Teacher assigned successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult RemoveSubjectTeacher(int classId, int subjectId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var assignment = _context.TeacherSubjectAssignments
                    .FirstOrDefault(a => a.ClassId == classId && a.SubjectId == subjectId && a.IsActive);

                if (assignment != null)
                {
                    assignment.IsActive = false;
                    _context.SaveChanges();
                }

                return Json(new { success = true, message = "Teacher removed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // TIME TABLE MANAGEMENT
        // ============================================

        public ActionResult TimeTable()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View();
        }

        public ActionResult ManageTimeTable(string type, int? gradeId = null, int? classId = null)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            ViewBag.Type = type;
            ViewBag.TypeName = type == "Teachers" ? "Teachers Timetable" : "Learners Timetable";

            if (type == "Teachers")
            {
                ViewBag.ShowTeacherTimetable = true;
                var teacherTimetables = _context.TimeTables
                    .Where(t => t.Type == type && t.IsActive)
                    .OrderByDescending(t => t.UploadedAt)
                    .ToList()
                    .Select(t => new TimeTableViewModel
                    {
                        Id = t.Id,
                        FilePath = t.FilePath,
                        Type = t.Type,
                        Title = t.Title,
                        Description = t.Description,
                        UploadedAt = t.UploadedAt,
                        UploadedByName = "Admin"
                    })
                    .ToList();
                return View(teacherTimetables);
            }

            if (!gradeId.HasValue)
            {
                ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
                ViewBag.ShowGradeSelection = true;
                return View();
            }

            var selectedGrade = _context.Grades.Find(gradeId);
            if (selectedGrade == null) return HttpNotFound();

            if (!classId.HasValue)
            {
                ViewBag.Grade = selectedGrade;
                ViewBag.Classes = _context.Classes.Where(c => c.GradeId == gradeId).OrderBy(c => c.Name).ToList();
                ViewBag.ShowClassSelection = true;
                return View();
            }

            var selectedClass = _context.Classes.Find(classId);
            if (selectedClass == null) return HttpNotFound();

            ViewBag.Grade = selectedGrade;
            ViewBag.ClassName = selectedClass;
            ViewBag.ShowTimetables = true;

            var learnerTimetables = _context.TimeTables
                .Where(t => t.Type == type && t.Grade == selectedGrade.Name && t.ClassName == selectedClass.FullName && t.IsActive)
                .OrderByDescending(t => t.UploadedAt)
                .ToList()
                .Select(t => new TimeTableViewModel
                {
                    Id = t.Id,
                    FilePath = t.FilePath,
                    Type = t.Type,
                    Title = t.Title,
                    Description = t.Description,
                    UploadedAt = t.UploadedAt,
                    UploadedByName = "Admin"
                })
                .ToList();

            return View(learnerTimetables);
        }

        [HttpPost]
        public ActionResult UploadTimeTable(UploadTimeTableViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                string uploadPath = Server.MapPath("~/Uploads/Timetables/");
                if (!System.IO.Directory.Exists(uploadPath))
                    System.IO.Directory.CreateDirectory(uploadPath);

                string fileName = null;
                if (model.TimetableFile != null && model.TimetableFile.ContentLength > 0)
                {
                    if (model.TimetableFile.ContentLength > 10 * 1024 * 1024)
                        return Json(new { success = false, message = "File size exceeds 10MB limit" });

                    string extension = System.IO.Path.GetExtension(model.TimetableFile.FileName);
                    if (model.Type == "Teachers")
                    {
                        fileName = $"TeachersTimetable_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    }
                    else
                    {
                        fileName = $"Timetable_{model.Type}_{model.Grade}_{model.ClassName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                    }
                    string fullPath = System.IO.Path.Combine(uploadPath, fileName);
                    model.TimetableFile.SaveAs(fullPath);
                }
                else
                {
                    return Json(new { success = false, message = "Please select a file to upload" });
                }

                var timetable = new TimeTable
                {
                    FilePath = "/Uploads/Timetables/" + fileName,
                    Type = model.Type,
                    Grade = model.Type == "Teachers" ? null : model.Grade,
                    ClassName = model.Type == "Teachers" ? null : model.ClassName,
                    Title = model.Title,
                    Description = model.Description,
                    UploadedBy = GetCurrentUserId(),
                    UploadedAt = DateTime.Now,
                    IsActive = true
                };

                _context.TimeTables.Add(timetable);
                _context.SaveChanges();

                return Json(new { success = true, message = "Timetable uploaded successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DeleteTimeTable(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var timetable = _context.TimeTables.Find(id);
                if (timetable == null) return Json(new { success = false, message = "Timetable not found" });

                string filePath = Server.MapPath(timetable.FilePath);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _context.TimeTables.Remove(timetable);
                _context.SaveChanges();

                return Json(new { success = true, message = "Timetable deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // USER MANAGEMENT
        // ============================================

        public ActionResult ManageUsers()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            // Get teachers first (execute query)
            var teachers = _context.Teachers
                .Include(t => t.User)
                .Include(t => t.SubjectQualifications.Select(sq => sq.Subject))
                .Include(t => t.GradeAssignments.Select(ga => ga.Grade))
                .ToList()  // <-- Execute query FIRST
                .Select(t => new TeacherUserViewModel
                {
                    Id = t.Id,
                    StaffNumber = t.User.StudentNumber,
                    FullName = t.FirstName + " " + t.LastName,
                    Email = t.User.Email,
                    PhoneNumber = t.PhoneNumber,
                    Qualification = t.Qualification,
                    // Do string.Join AFTER data is in memory (.ToList() already executed)
                    Subjects = t.SubjectQualifications != null && t.SubjectQualifications.Any()
                        ? string.Join(", ", t.SubjectQualifications.Select(sq => sq.Subject?.Name ?? ""))
                        : "",
                    Grades = t.GradeAssignments != null && t.GradeAssignments.Any()
                        ? string.Join(", ", t.GradeAssignments.Select(ga => ga.Grade?.Name ?? ""))
                        : "",
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt
                })
                .ToList();

            // Get students
            var students = _context.Students
                .Include(s => s.User)
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .Include(s => s.Stream)
                .Where(s => s.IsActive)
                .ToList()  // <-- Execute query FIRST
                .Select(s => new StudentUserViewModel
                {
                    UserId = s.UserId,
                    StudentNumber = s.User.StudentNumber,
                    FullName = s.FullName,
                    Email = s.User.Email,
                    CellPhone = s.CellPhone,
                    GradeApplyingFor = s.Class?.Grade?.Name ?? "Not assigned",
                    StreamChoice = s.Stream?.Name ?? "",
                    Status = s.IsActive ? "Active" : "Inactive",
                    ApplicationDate = s.EnrollmentDate
                })
                .ToList();

            var viewModel = new ManageUsersViewModel
            {
                Teachers = teachers,
                Students = students
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult ResetUserPassword(int userId, string userType)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var user = _context.Users.Find(userId);
                if (user == null) return Json(new { success = false, message = "User not found" });

                var tempPassword = GenerateTempPassword();
                user.PasswordHash = HashPassword(tempPassword);
                user.HasChangedPassword = false;
                _context.SaveChanges();

                try
                {
                    _emailService.SendCustomEmail(user.Email, "Password Reset - ElevateED",
                        $"<p>Your password has been reset.</p><p><strong>Temporary Password:</strong> {tempPassword}</p><p>Please log in and change your password immediately.</p>");
                }
                catch { }

                return Json(new { success = true, message = "Password reset successfully", email = user.Email });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult DeleteUser(int userId, string userType)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var user = _context.Users.Find(userId);
                if (user == null) return Json(new { success = false, message = "User not found" });

                if (userType == "teacher")
                {
                    var teacher = _context.Teachers
                        .Include(t => t.SubjectQualifications)
                        .Include(t => t.GradeAssignments)
                        .Include(t => t.SubjectAssignments)
                        .FirstOrDefault(t => t.UserId == userId);

                    if (teacher != null)
                    {
                        // Delete related records
                        _context.TeacherSubjectQualifications.RemoveRange(teacher.SubjectQualifications);
                        _context.TeacherGradeAssignments.RemoveRange(teacher.GradeAssignments);
                        _context.TeacherSubjectAssignments.RemoveRange(teacher.SubjectAssignments);
                        _context.Teachers.Remove(teacher);
                    }
                }
                else if (userType == "student")
                {
                    var student = _context.Students.FirstOrDefault(s => s.UserId == userId);
                    if (student != null)
                    {
                        // Also delete the applicant record
                        var applicant = _context.Applicants.Find(student.ApplicantId);
                        if (applicant != null)
                        {
                            _context.Applicants.Remove(applicant);
                        }
                        _context.Students.Remove(student);
                    }
                }

                // Finally delete the user
                _context.Users.Remove(user);
                _context.SaveChanges();

                return Json(new { success = true, message = $"{userType} deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // ISSUE MANAGEMENT
        // ============================================

        public ActionResult Issues(string status, string priority, string category)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var query = _context.Issues.AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<IssueStatus>(status, out var statusEnum))
                query = query.Where(i => i.Status == statusEnum);

            if (!string.IsNullOrEmpty(priority) && Enum.TryParse<IssuePriority>(priority, out var priorityEnum))
                query = query.Where(i => i.Priority == priorityEnum);

            if (!string.IsNullOrEmpty(category) && Enum.TryParse<IssueCategory>(category, out var categoryEnum))
                query = query.Where(i => i.Category == categoryEnum);

            var issues = query
                .OrderByDescending(i => i.Priority == IssuePriority.Critical)
                .ThenByDescending(i => i.Priority == IssuePriority.High)
                .ThenBy(i => i.Status == IssueStatus.Resolved)
                .ThenByDescending(i => i.CreatedAt)
                .ToList()
                .Select(i => new AdminIssueViewModel
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description.Length > 100 ? i.Description.Substring(0, 100) + "..." : i.Description,
                    Category = i.Category,
                    Priority = i.Priority,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt,
                    IsAnonymous = i.IsAnonymous,
                    StudentName = i.IsAnonymous ? "Anonymous" : GetStudentName(i.StudentId),
                    StudentNumber = i.IsAnonymous ? "-" : GetStudentNumber(i.StudentId),
                    Grade = i.IsAnonymous ? "-" : GetStudentGrade(i.StudentId),
                    ClassName = i.IsAnonymous ? "-" : GetStudentClass(i.StudentId)
                })
                .ToList();

            ViewBag.Stats = new IssueStatsViewModel
            {
                TotalIssues = _context.Issues.Count(),
                PendingIssues = _context.Issues.Count(i => i.Status == IssueStatus.Pending),
                UnderReviewIssues = _context.Issues.Count(i => i.Status == IssueStatus.UnderReview),
                ResolvedIssues = _context.Issues.Count(i => i.Status == IssueStatus.Resolved),
                CriticalIssues = _context.Issues.Count(i => i.Priority == IssuePriority.Critical && i.Status != IssueStatus.Resolved)
            };

            ViewBag.Statuses = Enum.GetNames(typeof(IssueStatus));
            ViewBag.Priorities = Enum.GetNames(typeof(IssuePriority));
            ViewBag.Categories = Enum.GetNames(typeof(IssueCategory));

            return View(issues);
        }

        public ActionResult IssueDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var issue = _context.Issues.Find(id);
            if (issue == null) return HttpNotFound();

            var student = _context.Students.Find(issue.StudentId);
            var user = student != null ? _context.Users.Find(student.UserId) : null;

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
                StudentName = issue.IsAnonymous ? "Anonymous" : student?.FullName ?? "Unknown",
                StudentNumber = issue.IsAnonymous ? "-" : user?.StudentNumber ?? "-",
                StudentEmail = issue.IsAnonymous ? "-" : user?.Email ?? "-",
                Grade = issue.IsAnonymous ? "-" : student?.Class?.Grade?.Name ?? "-",
                ClassName = issue.IsAnonymous ? "-" : student?.Class?.FullName ?? "-",
                ResolvedBy = issue.ResolvedBy,
                ResolvedByName = issue.ResolvedBy.HasValue ? GetAdminName(issue.ResolvedBy.Value) : null,
                ResolutionNotes = issue.ResolutionNotes,
                AdminResponse = issue.AdminResponse,
                AttachmentsPath = issue.AttachmentsPath
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult UpdateIssuePriority(int id, IssuePriority priority)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var issue = _context.Issues.Find(id);
                if (issue == null) return Json(new { success = false, message = "Issue not found" });

                issue.Priority = priority;
                issue.UpdatedAt = DateTime.Now;
                _context.SaveChanges();

                return Json(new { success = true, message = "Priority updated" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResolveIssue(ResolveIssueViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var adminUser = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
                var issue = _context.Issues.Find(model.Id);

                if (issue == null) return Json(new { success = false, message = "Issue not found" });

                issue.Status = model.NewStatus;
                issue.ResolutionNotes = model.ResolutionNotes;
                issue.AdminResponse = model.AdminResponse;
                issue.ResolvedBy = adminUser?.Id;
                issue.ResolvedAt = DateTime.Now;
                issue.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                return Json(new { success = true, message = $"Issue marked as {model.NewStatus.ToString().ToLower()} successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult SetIssueUnderReview(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var issue = _context.Issues.Find(id);
                if (issue == null) return Json(new { success = false, message = "Issue not found" });

                issue.Status = IssueStatus.UnderReview;
                issue.UpdatedAt = DateTime.Now;
                _context.SaveChanges();

                return Json(new { success = true, message = "Issue marked as under review" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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

        private int GetCurrentUserId()
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == User.Identity.Name);
            return user?.Id ?? 1;
        }

        private string GenerateStaffNumber(string idNumber)
        {
            var year = DateTime.Now.Year.ToString();
            var idPrefix = !string.IsNullOrEmpty(idNumber) && idNumber.Length >= 4 ? idNumber.Substring(0, 4) : "0000";
            var random = new Random();
            var suffix = random.Next(10, 99).ToString("D2");
            return "TCH" + year + idPrefix + suffix;
        }

        private string GenerateTempPassword()
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var res = new System.Text.StringBuilder();
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                var uintBuffer = new byte[sizeof(uint)];
                while (res.Length < 12)
                {
                    rng.GetBytes(uintBuffer);
                    var num = BitConverter.ToUInt32(uintBuffer, 0);
                    res.Append(valid[(int)(num % (uint)valid.Length)]);
                }
            }
            return res.ToString();
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private List<string> GetYearsList()
        {
            var years = new List<string>();
            int currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 5; i--)
                years.Add(i.ToString());
            return years;
        }

        private List<Subject> GetSubjectsForGradeLevel(int gradeLevel)
        {
            var subjects = _context.Subjects
                .Where(s => s.Category == SubjectCategory.Core)
                .ToList();

            if (gradeLevel >= 10)
            {
                subjects.AddRange(_context.Subjects.Where(s => s.Category == SubjectCategory.Elective));
                subjects.AddRange(_context.Subjects.Where(s => s.Category == SubjectCategory.Technology));
            }

            return subjects.Distinct().OrderBy(s => s.Name).ToList();
        }

        private string GetStudentName(int studentId)
        {
            var student = _context.Students.Find(studentId);
            return student?.FullName ?? "Unknown";
        }

        private string GetStudentNumber(int studentId)
        {
            var student = _context.Students.Find(studentId);
            if (student == null) return "-";
            var user = _context.Users.Find(student.UserId);
            return user?.StudentNumber ?? "-";
        }

        private string GetStudentGrade(int studentId)
        {
            var student = _context.Students.Include(s => s.Class.Grade).FirstOrDefault(s => s.Id == studentId);
            return student?.Class?.Grade?.Name ?? "-";
        }

        private string GetStudentClass(int studentId)
        {
            var student = _context.Students.Include(s => s.Class).FirstOrDefault(s => s.Id == studentId);
            return student?.Class?.FullName ?? "-";
        }

        private string GetAdminName(int userId)
        {
            var user = _context.Users.Find(userId);
            return user?.Email ?? "Unknown";
        }
        private int AutoAllocateStudentToClass(Student student)
        {
            // Get the grade the student applied for
            var gradeId = student.GradeApplyingForId;

            if (!gradeId.HasValue)
                return 0;

            // Get all classes for this grade
            var classes = _context.Classes
                .Where(c => c.GradeId == gradeId.Value)
                .OrderBy(c => c.Name)
                .ToList();

            if (!classes.Any())
                return 0;

            // Find the class with the fewest students (balanced distribution)
            var classCounts = classes.ToDictionary(
                c => c.Id,
                c => _context.Students.Count(s => s.ClassId == c.Id)
            );

            // Find class with lowest count that isn't full
            var bestClass = classes
                .Where(c => classCounts[c.Id] < c.Capacity)
                .OrderBy(c => classCounts[c.Id])
                .FirstOrDefault();

            return bestClass?.Id ?? 0;
        }
    }
}