using ElevateED.Models;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    [Authorize]
    public class VirtualLabController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // STUDENT VIEWS
        // ============================================

        [Authorize(Roles = "Student")]
        public ActionResult Index()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var studentGrade = student.Grade ?? student.GradeApplyingFor?.Name;
            var studentStream = student.StreamChoice;

            var allowedLabTypes = GetAllowedLabTypesForStudent(student);

            // Get all assignments for this student's class (including self-study ones created automatically)
            var assignments = _context.VirtualLabAssignments
                .Include(a => a.Experiment)
                .Include(a => a.Class)
                .Where(a => (a.ClassId == student.ClassId || a.ClassId == 0) && a.IsActive)
                .ToList();

            var results = _context.VirtualLabResults
                .Include(r => r.Assignment)
                .Include(r => r.Assignment.Experiment)
                .Include(r => r.Session)
                .Where(r => r.StudentId == student.Id)
                .ToList();

            var assignedExperiments = new List<VirtualLabExperiment>();
            var inProgressExperiments = new List<VirtualLabExperiment>();
            var completedExperiments = new List<VirtualLabResult>();
            var availableExperiments = new List<VirtualLabExperiment>();

            foreach (var assignment in assignments)
            {
                var result = results.FirstOrDefault(r => r.AssignmentId == assignment.Id);

                if (result == null)
                {
                    if (allowedLabTypes.Contains(assignment.Experiment.LabType))
                    {
                        assignedExperiments.Add(assignment.Experiment);
                    }
                }
                else if (result.Status == "InProgress")
                {
                    if (allowedLabTypes.Contains(assignment.Experiment.LabType))
                    {
                        inProgressExperiments.Add(assignment.Experiment);
                    }
                }
                else if (result.Status == "Submitted" || result.Status == "Graded")
                {
                    completedExperiments.Add(result);
                }
            }

            // Get available experiments (not assigned but allowed)
            var allExperiments = _context.VirtualLabExperiments
                .Where(e => e.IsActive &&
                       e.GradeLevel == studentGrade &&
                       allowedLabTypes.Contains(e.LabType))
                .ToList();

            var assignedIds = assignments.Select(a => a.ExperimentId).ToList();
            availableExperiments = allExperiments
                .Where(e => !assignedIds.Contains(e.Id))
                .ToList();

            var viewModel = new VirtualLabDashboardViewModel
            {
                StudentName = student.FullName,
                StudentNumber = student.User?.StudentNumber,
                Grade = studentGrade,
                ClassName = student.ClassName,
                Stream = studentStream,
                AllowedLabTypes = allowedLabTypes,
                AssignedExperiments = assignedExperiments,
                InProgressExperiments = inProgressExperiments,
                CompletedExperiments = completedExperiments,
                AvailableExperiments = availableExperiments
            };

            ViewBag.StudentName = student.FullName;
            ViewBag.Grade = studentGrade;
            ViewBag.ClassName = student.ClassName;

            return View(viewModel);
        }

        // ============================================
        // SPECIFIC LAB LAUNCHERS
        // ============================================

        [Authorize(Roles = "Student")]
        public ActionResult LaunchBiologyLab(int experimentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            var allowedLabTypes = GetAllowedLabTypesForStudent(student);
            if (!allowedLabTypes.Contains(LabType.Biology))
            {
                TempData["ErrorMessage"] = "You don't have access to Biology labs based on your subject selection.";
                return RedirectToAction("Index");
            }

            // Check if there's an assignment for this experiment and class
            var assignment = _context.VirtualLabAssignments
                .FirstOrDefault(a => a.ExperimentId == experimentId && a.ClassId == student.ClassId && a.IsActive);

            // If no assignment exists, create one for self-study
            if (assignment == null)
            {
                assignment = new VirtualLabAssignment
                {
                    ExperimentId = experimentId,
                    ClassId = student.ClassId ?? 0,
                    DueDate = DateTime.Now.AddDays(30),
                    Instructions = "Self-study experiment - No due date",
                    AssignedBy = 1,
                    IsActive = true,
                    AssignedAt = DateTime.Now
                };
                _context.VirtualLabAssignments.Add(assignment);
                _context.SaveChanges();
            }

            // Find existing result
            var result = _context.VirtualLabResults
                .Include(r => r.Session)
                .FirstOrDefault(r => r.AssignmentId == assignment.Id && r.StudentId == student.Id);

            if (result == null)
            {
                var session = new VirtualLabSession();
                _context.VirtualLabSessions.Add(session);
                _context.SaveChanges();

                result = new VirtualLabResult
                {
                    AssignmentId = assignment.Id,
                    StudentId = student.Id,
                    StartedAt = DateTime.Now,
                    Status = "InProgress",
                    SessionId = session.Id
                };
                _context.VirtualLabResults.Add(result);
                _context.SaveChanges();
            }

            // Update session activity
            if (result.SessionId.HasValue)
            {
                var session = _context.VirtualLabSessions.Find(result.SessionId.Value);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.Now;
                    var interactions = string.IsNullOrEmpty(session.InteractionsLog)
                        ? new List<object>()
                        : JsonConvert.DeserializeObject<List<object>>(session.InteractionsLog);
                    interactions.Add(new { action = "launched", labType = "Biology", timestamp = DateTime.Now });
                    session.InteractionsLog = JsonConvert.SerializeObject(interactions);
                    _context.SaveChanges();
                }
            }

            ViewBag.Experiment = experiment;
            ViewBag.ResultId = result.Id;
            ViewBag.SessionId = result.SessionId ?? 0;
            ViewBag.IsAssigned = true;
            ViewBag.DueDate = assignment.DueDate;

            return View("BiologyLab");
        }

        [Authorize(Roles = "Student")]
        public ActionResult LaunchChemistryLab(int experimentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            var allowedLabTypes = GetAllowedLabTypesForStudent(student);
            if (!allowedLabTypes.Contains(LabType.Chemistry))
            {
                TempData["ErrorMessage"] = "You don't have access to Chemistry labs based on your subject selection.";
                return RedirectToAction("Index");
            }

            var assignment = _context.VirtualLabAssignments
                .FirstOrDefault(a => a.ExperimentId == experimentId && a.ClassId == student.ClassId && a.IsActive);

            if (assignment == null)
            {
                assignment = new VirtualLabAssignment
                {
                    ExperimentId = experimentId,
                    ClassId = student.ClassId ?? 0,
                    DueDate = DateTime.Now.AddDays(30),
                    Instructions = "Self-study experiment - No due date",
                    AssignedBy = 1,
                    IsActive = true,
                    AssignedAt = DateTime.Now
                };
                _context.VirtualLabAssignments.Add(assignment);
                _context.SaveChanges();
            }

            var result = _context.VirtualLabResults
                .Include(r => r.Session)
                .FirstOrDefault(r => r.AssignmentId == assignment.Id && r.StudentId == student.Id);

            if (result == null)
            {
                var session = new VirtualLabSession();
                _context.VirtualLabSessions.Add(session);
                _context.SaveChanges();

                result = new VirtualLabResult
                {
                    AssignmentId = assignment.Id,
                    StudentId = student.Id,
                    StartedAt = DateTime.Now,
                    Status = "InProgress",
                    SessionId = session.Id
                };
                _context.VirtualLabResults.Add(result);
                _context.SaveChanges();
            }

            if (result.SessionId.HasValue)
            {
                var session = _context.VirtualLabSessions.Find(result.SessionId.Value);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.Now;
                    var interactions = string.IsNullOrEmpty(session.InteractionsLog)
                        ? new List<object>()
                        : JsonConvert.DeserializeObject<List<object>>(session.InteractionsLog);
                    interactions.Add(new { action = "launched", labType = "Chemistry", timestamp = DateTime.Now });
                    session.InteractionsLog = JsonConvert.SerializeObject(interactions);
                    _context.SaveChanges();
                }
            }

            ViewBag.Experiment = experiment;
            ViewBag.ResultId = result.Id;
            ViewBag.SessionId = result.SessionId ?? 0;
            ViewBag.IsAssigned = true;
            ViewBag.DueDate = assignment.DueDate;

            return View("ChemistryLab");
        }

        [Authorize(Roles = "Student")]
        public ActionResult LaunchPhysicsLab(int experimentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            var allowedLabTypes = GetAllowedLabTypesForStudent(student);
            if (!allowedLabTypes.Contains(LabType.Physics))
            {
                TempData["ErrorMessage"] = "You don't have access to Physics labs based on your subject selection.";
                return RedirectToAction("Index");
            }

            var assignment = _context.VirtualLabAssignments
                .FirstOrDefault(a => a.ExperimentId == experimentId && a.ClassId == student.ClassId && a.IsActive);

            if (assignment == null)
            {
                assignment = new VirtualLabAssignment
                {
                    ExperimentId = experimentId,
                    ClassId = student.ClassId ?? 0,
                    DueDate = DateTime.Now.AddDays(30),
                    Instructions = "Self-study experiment - No due date",
                    AssignedBy = 1,
                    IsActive = true,
                    AssignedAt = DateTime.Now
                };
                _context.VirtualLabAssignments.Add(assignment);
                _context.SaveChanges();
            }

            var result = _context.VirtualLabResults
                .Include(r => r.Session)
                .FirstOrDefault(r => r.AssignmentId == assignment.Id && r.StudentId == student.Id);

            if (result == null)
            {
                var session = new VirtualLabSession();
                _context.VirtualLabSessions.Add(session);
                _context.SaveChanges();

                result = new VirtualLabResult
                {
                    AssignmentId = assignment.Id,
                    StudentId = student.Id,
                    StartedAt = DateTime.Now,
                    Status = "InProgress",
                    SessionId = session.Id
                };
                _context.VirtualLabResults.Add(result);
                _context.SaveChanges();
            }

            if (result.SessionId.HasValue)
            {
                var session = _context.VirtualLabSessions.Find(result.SessionId.Value);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.Now;
                    var interactions = string.IsNullOrEmpty(session.InteractionsLog)
                        ? new List<object>()
                        : JsonConvert.DeserializeObject<List<object>>(session.InteractionsLog);
                    interactions.Add(new { action = "launched", labType = "Physics", timestamp = DateTime.Now });
                    session.InteractionsLog = JsonConvert.SerializeObject(interactions);
                    _context.SaveChanges();
                }
            }

            ViewBag.Experiment = experiment;
            ViewBag.ResultId = result.Id;
            ViewBag.SessionId = result.SessionId ?? 0;
            ViewBag.IsAssigned = true;
            ViewBag.DueDate = assignment.DueDate;

            return View("PhysicsLab");
        }

        // Keep the generic LaunchLab for backward compatibility
        [Authorize(Roles = "Student")]
        public ActionResult LaunchLab(int experimentId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            // Route to specific lab based on LabType
            switch (experiment.LabType)
            {
                case LabType.Biology:
                    return RedirectToAction("LaunchBiologyLab", new { experimentId });
                case LabType.Chemistry:
                    return RedirectToAction("LaunchChemistryLab", new { experimentId });
                case LabType.Physics:
                    return RedirectToAction("LaunchPhysicsLab", new { experimentId });
                default:
                    TempData["ErrorMessage"] = "Unknown lab type.";
                    return RedirectToAction("Index");
            }
        }

        // ============================================
        // PROGRESS AND SUBMISSION
        // ============================================

        [HttpPost]
        [Authorize(Roles = "Student")]
        public JsonResult SaveLabProgress(int resultId, string observations, string conclusions, string resultsData)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Unauthorized" });

            var result = _context.VirtualLabResults
                .Include(r => r.Session)
                .FirstOrDefault(r => r.Id == resultId && r.StudentId == student.Id);

            if (result == null)
                return Json(new { success = false, message = "Result not found" });

            if (!string.IsNullOrEmpty(observations))
                result.Observations = observations;

            if (!string.IsNullOrEmpty(conclusions))
                result.Conclusions = conclusions;

            if (!string.IsNullOrEmpty(resultsData))
            {
                var currentData = string.IsNullOrEmpty(result.ExperimentData)
                    ? new List<object>()
                    : JsonConvert.DeserializeObject<List<object>>(result.ExperimentData);

                var newData = JsonConvert.DeserializeObject(resultsData);
                currentData.Add(newData);
                result.ExperimentData = JsonConvert.SerializeObject(currentData);
            }

            if (result.Session != null)
            {
                var interactions = string.IsNullOrEmpty(result.Session.InteractionsLog)
                    ? new List<object>()
                    : JsonConvert.DeserializeObject<List<object>>(result.Session.InteractionsLog);

                interactions.Add(new { action = "progress_saved", timestamp = DateTime.Now });
                result.Session.InteractionsLog = JsonConvert.SerializeObject(interactions);
                result.Session.LastActivityAt = DateTime.Now;
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Progress saved" });
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public JsonResult SubmitLab(int resultId)
        {
            var student = GetCurrentStudent();
            if (student == null) return Json(new { success = false, message = "Unauthorized" });

            var result = _context.VirtualLabResults
                .Include(r => r.Session)
                .FirstOrDefault(r => r.Id == resultId && r.StudentId == student.Id);

            if (result == null)
                return Json(new { success = false, message = "Result not found" });

            result.Status = "Submitted";
            result.CompletedAt = DateTime.Now;

            if (result.Session != null)
            {
                result.Session.IsActive = false;
                var interactions = string.IsNullOrEmpty(result.Session.InteractionsLog)
                    ? new List<object>()
                    : JsonConvert.DeserializeObject<List<object>>(result.Session.InteractionsLog);
                interactions.Add(new { action = "submitted", timestamp = DateTime.Now });
                result.Session.InteractionsLog = JsonConvert.SerializeObject(interactions);
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Lab submitted successfully!" });
        }

        [Authorize(Roles = "Student")]
        public ActionResult MyResults()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var results = _context.VirtualLabResults
                .Include(r => r.Assignment)
                .Include(r => r.Assignment.Experiment)
                .Include(r => r.Session)
                .Where(r => r.StudentId == student.Id && (r.Status == "Submitted" || r.Status == "Graded"))
                .OrderByDescending(r => r.CompletedAt)
                .ToList();

            return View(results);
        }

        // ============================================
        // TEACHER VIEWS
        // ============================================

        [Authorize(Roles = "Teacher")]
        public ActionResult TeacherDashboard()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var experiments = _context.VirtualLabExperiments
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            var assignments = _context.VirtualLabAssignments
                .Include(a => a.Experiment)
                .Include(a => a.Class)
                .Where(a => a.AssignedBy == teacher.Id && a.IsActive)
                .OrderByDescending(a => a.AssignedAt)
                .ToList();

            var pendingSubmissions = _context.VirtualLabResults
                .Include(r => r.Assignment)
                .Include(r => r.Assignment.Experiment)
                .Include(r => r.Student)
                .Where(r => r.Status == "Submitted")
                .OrderBy(r => r.CompletedAt)
                .ToList();

            var viewModel = new TeacherLabDashboardViewModel
            {
                Experiments = experiments,
                Assignments = assignments,
                PendingSubmissions = pendingSubmissions,
                TeacherName = teacher.FullName
            };

            ViewBag.TeacherName = teacher.FullName;

            return View(viewModel);
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult CreateExperiment()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            ViewBag.LabTypes = new SelectList(new[] { "Biology", "Chemistry", "Physics" });
            ViewBag.Difficulties = new SelectList(new[] { "Beginner", "Intermediate", "Advanced" });
            ViewBag.Grades = _context.Grades.OrderBy(g => g.Level).Select(g => g.Name).ToList();

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public JsonResult CreateExperiment(CreateExperimentViewModel model)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields" });

            try
            {
                LabType labType;
                Enum.TryParse(model.LabType.ToString(), out labType);

                LabDifficulty difficulty;
                Enum.TryParse(model.Difficulty.ToString(), out difficulty);

                var experiment = new VirtualLabExperiment
                {
                    Title = model.Title,
                    Description = model.Description,
                    Instructions = model.Instructions,
                    LabType = labType,
                    Difficulty = difficulty,
                    DurationMinutes = model.DurationMinutes,
                    GradeLevel = model.GradeLevel,
                    LearningObjectives = model.LearningObjectives,
                    PreLabQuestions = model.PreLabQuestions,
                    PostLabQuestions = model.PostLabQuestions,
                    EquipmentList = model.EquipmentList,
                    CreatedBy = teacher.Id,
                    IsActive = true
                };

                _context.VirtualLabExperiments.Add(experiment);
                _context.SaveChanges();

                return Json(new { success = true, message = "Experiment created successfully!", experimentId = experiment.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult AssignExperiment(int experimentId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            var teacherClasses = _context.TeacherSubjectAssignments
                .Include(a => a.Class)
                .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                .Select(a => a.Class)
                .Distinct()
                .ToList();

            ViewBag.Experiment = experiment;
            ViewBag.Classes = teacherClasses;
            ViewBag.Subjects = _context.Subjects.OrderBy(s => s.Name).ToList();

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public JsonResult AssignExperiment(int experimentId, int classId, int? subjectId, DateTime dueDate, string instructions)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var assignment = new VirtualLabAssignment
                {
                    ExperimentId = experimentId,
                    ClassId = classId,
                    SubjectId = subjectId,
                    DueDate = dueDate,
                    Instructions = instructions,
                    AssignedBy = teacher.Id,
                    IsActive = true
                };

                _context.VirtualLabAssignments.Add(assignment);
                _context.SaveChanges();

                return Json(new { success = true, message = "Lab assigned to class successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [Authorize(Roles = "Teacher")]
        public ActionResult GradeSubmissions(int experimentId)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var experiment = _context.VirtualLabExperiments.Find(experimentId);
            if (experiment == null) return HttpNotFound();

            var assignments = _context.VirtualLabAssignments
                .Where(a => a.ExperimentId == experimentId && a.AssignedBy == teacher.Id)
                .Select(a => a.Id)
                .ToList();

            var submissions = _context.VirtualLabResults
    .Include(r => r.Student)
    .Include(r => r.Student.User)
    .Where(r => r.AssignmentId.HasValue && assignments.Contains(r.AssignmentId.Value) && r.Status == "Submitted")
    .OrderBy(r => r.CompletedAt)
    .ToList();

            ViewBag.Experiment = experiment;

            return View(submissions);
        }

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        public JsonResult SaveGrade(int resultId, int score, string feedback)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var result = _context.VirtualLabResults.Find(resultId);
            if (result == null)
                return Json(new { success = false, message = "Result not found" });

            result.Score = score;
            result.TeacherFeedback = feedback;
            result.GradedBy = teacher.Id;
            result.GradedAt = DateTime.Now;
            result.Status = "Graded";

            _context.SaveChanges();

            return Json(new { success = true, message = "Grade saved successfully!" });
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.User)
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .Include(s => s.GradeApplyingFor)
                .Include(s => s.Stream)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

        private Teacher GetCurrentTeacher()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);
            if (user == null) return null;

            return _context.Teachers
                .Include(t => t.User)
                .FirstOrDefault(t => t.UserId == user.Id);
        }

        private List<LabType> GetAllowedLabTypesForStudent(Student student)
        {
            var allowedLabs = new List<LabType>();

            var streamChoice = student.StreamChoice?.ToLower() ?? "";
            var gradeLevel = student.Grade ?? student.GradeApplyingFor?.Name ?? "";

            var subjects = GetStudentSubjects(student);

            // Biology/Life Sciences access
            if (subjects.Contains("Life Sciences") ||
                subjects.Contains("Life Science") ||
                subjects.Contains("Natural Science") ||
                streamChoice.Contains("life science"))
            {
                allowedLabs.Add(LabType.Biology);
            }

            // Chemistry/Physical Sciences access
            if (subjects.Contains("Physical Sciences") ||
                subjects.Contains("Chemistry") ||
                subjects.Contains("Natural Science") ||
                streamChoice.Contains("physics") ||
                streamChoice.Contains("physical science"))
            {
                allowedLabs.Add(LabType.Chemistry);
                allowedLabs.Add(LabType.Physics);
            }

            // For grades 8-9, allow all basic science labs
            if (gradeLevel.Contains("Grade 8") || gradeLevel.Contains("Grade 9"))
            {
                if (!allowedLabs.Contains(LabType.Biology))
                    allowedLabs.Add(LabType.Biology);
                if (!allowedLabs.Contains(LabType.Chemistry))
                    allowedLabs.Add(LabType.Chemistry);
                if (!allowedLabs.Contains(LabType.Physics))
                    allowedLabs.Add(LabType.Physics);
            }

            return allowedLabs.Distinct().ToList();
        }

        private List<string> GetStudentSubjects(Student student)
        {
            var subjects = new List<string>();

            if (student.Stream != null)
            {
                subjects.Add(student.Stream.Name);
            }

            if (student.ClassId.HasValue)
            {
                var classSubjects = _context.TeacherSubjectAssignments
                    .Include(a => a.Subject)
                    .Where(a => a.ClassId == student.ClassId && a.IsActive)
                    .Select(a => a.Subject.Name)
                    .ToList();
                subjects.AddRange(classSubjects);
            }

            return subjects.Distinct().ToList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}