using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Security.Cryptography;
using System.Text;
using ElevateED.Models;
using ElevateED.ViewModels;
using ElevateED.Services;

namespace ElevateED.Controllers
{
    public class ApplicationController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private EmailService _emailService = new EmailService();

        private const long MAX_FILE_SIZE_BYTES = 8 * 1024 * 1024;
        private const long MAX_TOTAL_SIZE_BYTES = 30 * 1024 * 1024;

        public ActionResult Apply()
        {
            // Use the SAME context (_context) for consistency
            var activeCycle = _context.ApplicationCycles
                .FirstOrDefault(c => c.IsActive);

            System.Diagnostics.Debug.WriteLine($"[GET Apply] Active cycle: {(activeCycle != null ? activeCycle.Name : "NULL")}");

            bool isOpen = true;
            string errorMessage = null;
            string successMessage = null;

            if (activeCycle == null)
            {
                errorMessage = "Applications are not currently open. Please check back later.";
                isOpen = false;
            }
            else if (DateTime.Now > activeCycle.DeadlineDate)
            {
                errorMessage = "The application deadline has passed.";
                isOpen = false;
            }
            else if (DateTime.Now < activeCycle.StartDate)
            {
                errorMessage = $"Applications open on {activeCycle.StartDate:dd MMM yyyy}. Please check back then.";
                isOpen = false;
            }
            else
            {
                successMessage = $"Applications are open! Deadline: {activeCycle.DeadlineDate:dd MMM yyyy}";
            }

            ViewBag.IsOpen = isOpen;
            ViewBag.ActiveCycle = activeCycle;
            ViewBag.ErrorMessage = errorMessage;
            ViewBag.SuccessMessage = successMessage;

            // Load dropdown data
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Streams = _context.Streams.OrderBy(s => s.Name).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Apply(ApplyViewModel model)
        {
            // Use the SAME context (_context)
            var activeCycle = _context.ApplicationCycles.FirstOrDefault(c => c.IsActive);

            if (activeCycle == null)
            {
                ModelState.AddModelError("", "Applications are not currently open.");
                ReloadViewBagData();
                return View(model);
            }

            if (DateTime.Now > activeCycle.DeadlineDate)
            {
                ModelState.AddModelError("", "The application deadline has passed.");
                ReloadViewBagData();
                return View(model);
            }

            // Find the selected grade
            var selectedGrade = _context.Grades.FirstOrDefault(g => g.Name == model.GradeApplyingFor);
            if (selectedGrade == null)
            {
                ModelState.AddModelError("GradeApplyingFor", "Please select a valid grade.");
                ReloadViewBagData();
                return View(model);
            }

            // Custom validation for StreamChoice based on grade
            if (selectedGrade.Level >= 10)
            {
                if (string.IsNullOrEmpty(model.StreamChoice) || model.StreamChoice == "-- Select Stream --")
                {
                    ModelState.AddModelError("StreamChoice", "Stream/Subject choice is required for Grade 10-12 applicants.");
                }
            }

            // Validate file sizes
            var fileValidationError = ValidateFiles(model);
            if (!string.IsNullOrEmpty(fileValidationError))
            {
                ModelState.AddModelError("", fileValidationError);
                ReloadViewBagData();
                return View(model);
            }

            // Check for existing email
            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "An application with this email already exists.");
                ReloadViewBagData();
                return View(model);
            }

            // Check for existing ID number
            if (_context.Applicants.Any(a => a.IdentityNumber == model.IdentityNumber))
            {
                ModelState.AddModelError("IdentityNumber", "An application with this ID number already exists.");
                ReloadViewBagData();
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                ReloadViewBagData();
                return View(model);
            }

            try
            {
                var studentNumber = GenerateStudentNumber();
                var tempPassword = GenerateTempPassword();

                // Create user
                var user = new ApplicationUser
                {
                    StudentNumber = studentNumber,
                    Email = model.Email,
                    PasswordHash = HashPassword(tempPassword),
                    Role = UserRole.Applicant,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    HasChangedPassword = false
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                // Handle file uploads
                string uploadPath = Server.MapPath("~/Uploads/Documents/");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                // Find selected stream
                int? streamId = null;
                if (!string.IsNullOrEmpty(model.StreamChoice) && model.StreamChoice != "-- Select Stream --")
                {
                    var selectedStream = _context.Streams.FirstOrDefault(s => s.Name == model.StreamChoice);
                    streamId = selectedStream?.Id;
                }

                // Create applicant
                var applicant = new Applicant
                {
                    UserId = user.Id,
                    ApplicationCycleId = activeCycle.Id,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    DateOfBirth = model.DateOfBirth,
                    Gender = model.Gender,
                    IdentityNumber = model.IdentityNumber,
                    Nationality = model.Nationality ?? "South African",
                    HomeLanguage = model.HomeLanguage,
                    CellPhone = model.CellPhone,
                    AlternativePhone = model.AlternativePhone,
                    PhysicalAddress = model.PhysicalAddress,
                    PostalAddress = model.PostalAddress,
                    PreviousSchool = model.PreviousSchool,
                    HighestGradePassed = model.HighestGradePassed,
                    YearCompleted = model.YearCompleted,
                    PreviousSchoolAddress = model.PreviousSchoolAddress,
                    AcademicAverage = model.AcademicAverage,
                    GradeApplyingForId = selectedGrade.Id,
                    GradeApplyingForName = selectedGrade.Name,
                    StreamId = streamId,
                    StreamChoiceName = model.StreamChoice,
                    ParentName = model.ParentName,
                    ParentIdNumber = model.ParentIdNumber,
                    ParentRelationship = model.ParentRelationship,
                    ParentCellPhone = model.ParentCellPhone,
                    ParentEmail = model.ParentEmail,
                    ParentWorkPhone = model.ParentWorkPhone,
                    ParentOccupation = model.ParentOccupation,
                    ParentEmployer = model.ParentEmployer,
                    ParentWorkAddress = model.ParentWorkAddress,
                    EmergencyContactName = model.EmergencyContactName,
                    EmergencyContactPhone = model.EmergencyContactPhone,
                    EmergencyContactRelationship = model.EmergencyContactRelationship,
                    MedicalConditions = model.MedicalConditions,
                    Allergies = model.Allergies,
                    CurrentMedication = model.CurrentMedication,
                    DoctorName = model.DoctorName,
                    DoctorPhone = model.DoctorPhone,
                    MedicalAidName = model.MedicalAidName,
                    MedicalAidNumber = model.MedicalAidNumber,
                    IdDocumentPath = SaveFile(model.IdDocument, uploadPath, studentNumber, "ID"),
                    ReportCardPath = SaveFile(model.ReportCard, uploadPath, studentNumber, "Report"),
                    TransferCertificatePath = SaveFile(model.TransferCertificate, uploadPath, studentNumber, "Transfer"),
                    ProofOfResidencePath = SaveFile(model.ProofOfResidence, uploadPath, studentNumber, "Residence"),
                    ParentIdDocumentPath = SaveFile(model.ParentIdDocument, uploadPath, studentNumber, "ParentID"),
                    Status = ApplicationStatus.Pending,
                    ApplicationDate = DateTime.Now
                };

                _context.Applicants.Add(applicant);
                _context.SaveChanges();

                // Send email
                try
                {
                    _emailService.SendApplicationSubmittedEmail(
                        model.Email,
                        model.FirstName + " " + model.LastName,
                        studentNumber,
                        tempPassword
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Email failed: " + ex.Message);
                }

                TempData["StudentNumber"] = studentNumber;
                TempData["TempPassword"] = tempPassword;
                TempData["GradeAppliedFor"] = selectedGrade.Name;
                TempData["Subjects"] = GetFormattedSubjects(selectedGrade.Level, model.StreamChoice);

                return RedirectToAction("ApplicationSuccess");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Application error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                ModelState.AddModelError("", "An error occurred while processing your application. Please try again.");

                ReloadViewBagData();
                return View(model);
            }
        }

        // Helper method to reload ViewBag data
        private void ReloadViewBagData()
        {
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).ToList();
            ViewBag.Streams = _context.Streams.OrderBy(s => s.Name).ToList();

            var activeCycle = _context.ApplicationCycles.FirstOrDefault(c => c.IsActive);
            ViewBag.IsOpen = activeCycle != null;
            ViewBag.ActiveCycle = activeCycle;
        }

        // ... rest of your helper methods (ValidateFiles, SaveFile, GenerateStudentNumber, etc.) ...

        private string ValidateFiles(ApplyViewModel model)
        {
            var filesToCheck = new[]
            {
                new { File = model.IdDocument, Name = "ID Document", IsRequired = true },
                new { File = model.ReportCard, Name = "Report Card", IsRequired = true },
                new { File = model.ProofOfResidence, Name = "Proof of Residence", IsRequired = true },
                new { File = model.ParentIdDocument, Name = "Parent ID Document", IsRequired = true },
                new { File = model.TransferCertificate, Name = "Transfer Certificate", IsRequired = false }
            };

            long totalSize = 0;

            foreach (var file in filesToCheck)
            {
                if (file.File != null)
                {
                    if (file.File.ContentLength > MAX_FILE_SIZE_BYTES)
                    {
                        return $"{file.Name} exceeds the 8MB size limit.";
                    }
                    totalSize += file.File.ContentLength;
                }
                else if (file.IsRequired)
                {
                    return $"{file.Name} is required.";
                }
            }

            if (totalSize > MAX_TOTAL_SIZE_BYTES)
            {
                return $"Total file size exceeds the 30MB limit.";
            }

            return null;
        }

        public ActionResult ApplicationSuccess()
        {
            if (TempData["StudentNumber"] == null)
                return RedirectToAction("Apply");

            ViewBag.StudentNumber = TempData["StudentNumber"];
            ViewBag.TempPassword = TempData["TempPassword"];
            ViewBag.GradeAppliedFor = TempData["GradeAppliedFor"];
            ViewBag.Subjects = TempData["Subjects"];
            return View();
        }

        [Authorize]
        public ActionResult PendingApproval()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants
                .Include("GradeApplyingFor")
                .Include("Stream")
                .FirstOrDefault(a => a.UserId == user.Id);

            if (applicant == null)
                return RedirectToAction("Index", "Home");

            if (applicant.Status == ApplicationStatus.Approved)
            {
                if (user.Role == UserRole.Applicant)
                {
                    user.Role = UserRole.Student;
                    _context.SaveChanges();
                }
                return RedirectToAction("Approved");
            }

            if (applicant.Status == ApplicationStatus.Rejected)
                return RedirectToAction("Rejected");

            var viewModel = new ApplicationStatusViewModel
            {
                StudentNumber = studentNumber,
                ApplicantName = applicant.FullName,
                Status = applicant.Status,
                ApplicationDate = applicant.ApplicationDate,
                GradeApplyingFor = applicant.GradeApplyingForDisplay
            };

            return View(viewModel);
        }

        [Authorize]
        public ActionResult Approved()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants
                .Include("GradeApplyingFor")
                .FirstOrDefault(a => a.UserId == user.Id);

            if (applicant == null || applicant.Status != ApplicationStatus.Approved)
                return RedirectToAction("PendingApproval");

            if (user.Role == UserRole.Student)
            {
                var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);
                if (student != null && student.ClassId.HasValue)
                    return RedirectToAction("Dashboard", "Student");

                ViewBag.PendingAllocation = true;
            }

            var viewModel = new ApplicationStatusViewModel
            {
                StudentNumber = studentNumber,
                ApplicantName = applicant.FullName,
                Status = applicant.Status,
                ApplicationDate = applicant.ApplicationDate,
                GradeApplyingFor = applicant.GradeApplyingForDisplay
            };

            return View(viewModel);
        }

        [Authorize]
        public ActionResult Rejected()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var applicant = _context.Applicants
                .Include("GradeApplyingFor")
                .FirstOrDefault(a => a.UserId == user.Id);

            if (applicant == null || applicant.Status != ApplicationStatus.Rejected)
                return RedirectToAction("PendingApproval");

            var viewModel = new ApplicationStatusViewModel
            {
                StudentNumber = studentNumber,
                ApplicantName = applicant.FullName,
                Status = applicant.Status,
                ApplicationDate = applicant.ApplicationDate,
                GradeApplyingFor = applicant.GradeApplyingForDisplay,
                RejectionReason = applicant.RejectionReason,
                AdminNotes = applicant.AdminNotes
            };

            return View(viewModel);
        }

        // Helper methods (keep your existing ones)
        private string GenerateStudentNumber()
        {
            var year = DateTime.Now.Year.ToString().Substring(2);
            var random = new Random();
            var number = random.Next(10000, 99999);
            return "MHS" + year + number.ToString("D5");
        }

        private string GenerateTempPassword()
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var res = new StringBuilder();
            using (var rng = new RNGCryptoServiceProvider())
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
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private string SaveFile(HttpPostedFileBase file, string uploadPath, string studentNumber, string docType)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            var extension = Path.GetExtension(file.FileName);
            var fileName = studentNumber + "_" + docType + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
            var path = Path.Combine(uploadPath, fileName);
            file.SaveAs(path);
            return "/Uploads/Documents/" + fileName;
        }

        private string GetFormattedSubjects(int gradeLevel, string streamChoice)
        {
            if (gradeLevel <= 9)
                return GetDefaultSubjectsForGrade8And9();
            else
                return GetStreamSubjects(streamChoice);
        }

        private string GetDefaultSubjectsForGrade8And9()
        {
            return @"CORE SUBJECTS:
• English (First Additional Language)
• isiZulu (Home Language)
• Life Orientation

OTHER SUBJECTS:
• Mathematics
• Natural Science
• Social Science
• Creative Arts
• Economic Management Science
• Technology";
        }

        private string GetStreamSubjects(string streamChoice)
        {
            var coreSubjects = @"CORE SUBJECTS:
• English (First Additional Language)
• isiZulu (Home Language)
• Life Orientation";

            var electiveSubjects = "";
            var technologySubject = "";

            switch (streamChoice)
            {
                case "Mathematics, Life Science & Physics":
                    electiveSubjects = @"ELECTIVE SUBJECTS:
• Mathematics
• Life Science
• Physical Sciences";
                    technologySubject = @"• Information Technology (IT)";
                    break;
                case "Mathematics, Life Science & Agriculture":
                    electiveSubjects = @"ELECTIVE SUBJECTS:
• Mathematics
• Life Science
• Agricultural Sciences";
                    technologySubject = @"• Computer Applications Technology (CAT)";
                    break;
                case "Mathematical Literacy, History & Geography":
                    electiveSubjects = @"ELECTIVE SUBJECTS:
• Mathematical Literacy
• History
• Geography";
                    technologySubject = @"• Computer Applications Technology (CAT)";
                    break;
                default:
                    return coreSubjects;
            }

            return coreSubjects + "\n\n" + electiveSubjects + "\n\nTECHNOLOGY SUBJECT:\n" + technologySubject;
        }
    }
}