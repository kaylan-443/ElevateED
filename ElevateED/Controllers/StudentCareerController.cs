using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentCareerController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private CareerGuidanceService _service;
        private CareerSimulatorService _simulator;

        public StudentCareerController()
        {
            _service = new CareerGuidanceService(_context);
            _simulator = new CareerSimulatorService(_context);
        }

        // ============================================
        // MY APS DASHBOARD
        // ============================================
        public ActionResult Index()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var aps = _service.CalculateApsForStudent(student.Id);

            // Match against all active careers to count qualifying/close.
            var careers = LoadActiveCareers();
            var matches = careers.Select(c => _service.EvaluateCareer(c, aps)).ToList();

            var lastResult = _context.StudentInterestResults
                .Include(r => r.TopField1)
                .Include(r => r.TopField2)
                .Include(r => r.TopField3)
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.TakenAt)
                .FirstOrDefault();

            var vm = new CareerDashboardViewModel
            {
                StudentName = student.FullName,
                Grade = student.Grade ?? "Not assigned",
                Aps = aps,
                QualifyingCount = matches.Count(m => m.Verdict == CareerMatchVerdict.Qualifies),
                CloseCount = matches.Count(m => m.Verdict == CareerMatchVerdict.Close),
                BookmarkCount = _context.StudentCareerBookmarks.Count(b => b.StudentId == student.Id),
                HasTakenQuiz = lastResult != null
            };

            if (lastResult != null)
            {
                if (lastResult.TopField1 != null) vm.TopFieldNames.Add(lastResult.TopField1.Name);
                if (lastResult.TopField2 != null) vm.TopFieldNames.Add(lastResult.TopField2.Name);
                if (lastResult.TopField3 != null) vm.TopFieldNames.Add(lastResult.TopField3.Name);
            }

            return View(vm);
        }

        // ============================================
        // CAREER EXPLORER
        // ============================================
        public ActionResult Explore(int? fieldId, string verdict)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var aps = _service.CalculateApsForStudent(student.Id);

            var careersQuery = LoadActiveCareers();
            if (fieldId.HasValue)
                careersQuery = careersQuery.Where(c => c.CareerFieldId == fieldId.Value).ToList();

            var matches = careersQuery
                .Select(c => _service.EvaluateCareer(c, aps))
                .ToList();

            // Optional verdict filter.
            if (verdict == "qualifies")
                matches = matches.Where(m => m.Verdict == CareerMatchVerdict.Qualifies).ToList();
            else if (verdict == "close")
                matches = matches.Where(m => m.Verdict == CareerMatchVerdict.Close).ToList();

            // Order best matches first, then by name.
            matches = matches
                .OrderBy(m => VerdictSortOrder(m.Verdict))
                .ThenBy(m => m.Career.Name)
                .ToList();

            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();

            var vm = new CareerExplorerViewModel
            {
                Fields = _context.CareerFields.Where(f => f.IsActive).OrderBy(f => f.Name).ToList(),
                SelectedFieldId = fieldId,
                VerdictFilter = verdict,
                Matches = matches,
                BookmarkedCareerIds = new HashSet<int>(bookmarkedIds),
                HasReportCard = aps.HasReportCard,
                StudentAps = aps.Aps
            };

            return View(vm);
        }

        // ============================================
        // CAREER DETAIL
        // ============================================
        public ActionResult Detail(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var career = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .FirstOrDefault(c => c.Id == id && c.IsActive);

            if (career == null) return HttpNotFound();

            var aps = _service.CalculateApsForStudent(student.Id);
            var match = _service.EvaluateCareer(career, aps);

            var vm = new CareerDetailViewModel
            {
                Match = match,
                Aps = aps,
                IsBookmarked = _context.StudentCareerBookmarks.Any(b => b.StudentId == student.Id && b.CareerId == id),
                AllRequirements = career.SubjectRequirements.OrderByDescending(r => r.IsCompulsory).ToList()
            };

            return View(vm);
        }

        // ============================================
        // BOOKMARK TOGGLE
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleBookmark(int careerId, string returnUrl)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var existing = _context.StudentCareerBookmarks
                .FirstOrDefault(b => b.StudentId == student.Id && b.CareerId == careerId);

            if (existing != null)
            {
                _context.StudentCareerBookmarks.Remove(existing);
                TempData["SuccessMessage"] = "Removed from your shortlist.";
            }
            else if (_context.Careers.Any(c => c.Id == careerId))
            {
                _context.StudentCareerBookmarks.Add(new StudentCareerBookmark
                {
                    StudentId = student.Id,
                    CareerId = careerId
                });
                TempData["SuccessMessage"] = "Added to your shortlist.";
            }

            _context.SaveChanges();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Detail", new { id = careerId });
        }

        // ============================================
        // MY SHORTLIST (BOOKMARKS)
        // ============================================
        public ActionResult Bookmarks()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var aps = _service.CalculateApsForStudent(student.Id);

            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => b.CareerId)
                .ToList();

            var careers = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => bookmarkedIds.Contains(c.Id))
                .ToList();

            var matches = careers.Select(c => _service.EvaluateCareer(c, aps)).ToList();

            var vm = new CareerBookmarksViewModel
            {
                Matches = matches.OrderBy(m => VerdictSortOrder(m.Verdict)).ThenBy(m => m.Career.Name).ToList(),
                HasReportCard = aps.HasReportCard
            };

            return View(vm);
        }

        // ============================================
        // COMPARE CAREERS
        // ============================================
        public ActionResult Compare(int[] ids)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var requested = (ids ?? new int[0]).Distinct().ToArray();

            if (requested.Length < 2)
            {
                TempData["ErrorMessage"] = "Pick at least two careers from your shortlist to compare.";
                return RedirectToAction("Bookmarks");
            }
            if (requested.Length > 3)
            {
                TempData["ErrorMessage"] = "Compare up to three careers at a time — narrow your selection and try again.";
                return RedirectToAction("Bookmarks");
            }

            // Only careers the learner has actually shortlisted may be compared.
            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();
            if (requested.Any(id => !bookmarkedIds.Contains(id)))
            {
                TempData["ErrorMessage"] = "You can only compare careers on your shortlist.";
                return RedirectToAction("Bookmarks");
            }

            var careers = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => requested.Contains(c.Id) && c.IsActive)
                .ToList();
            if (careers.Count < 2)
            {
                TempData["ErrorMessage"] = "One or more of those careers is no longer available.";
                return RedirectToAction("Bookmarks");
            }

            var aps = _service.CalculateApsForStudent(student.Id);
            var vm = new CareerCompareViewModel
            {
                Aps = aps,
                Comparison = _service.BuildComparison(careers, aps)
            };
            return View(vm);
        }

        // ============================================
        // IDENTIFY PRIORITY SUBJECTS
        // ============================================
        public ActionResult PrioritySubjects(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var career = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .FirstOrDefault(c => c.Id == id && c.IsActive);
            if (career == null) return HttpNotFound();

            var aps = _service.CalculateApsForStudent(student.Id);
            var match = _service.EvaluateCareer(career, aps);

            if (match.Verdict == CareerMatchVerdict.Qualifies)
            {
                TempData["SuccessMessage"] = "You already qualify for " + career.Name + " — nothing to prioritise here.";
                return RedirectToAction("Detail", new { id });
            }
            if (match.Verdict == CareerMatchVerdict.NoData)
            {
                TempData["ErrorMessage"] = "We need a published report card before we can work out what to prioritise.";
                return RedirectToAction("Detail", new { id });
            }

            var vm = new PrioritySubjectsViewModel
            {
                Career = career,
                Match = match,
                Breakdown = _service.RankPrioritySubjects(career, aps, match)
            };
            return View(vm);
        }

        // ============================================
        // ADMISSION OUTCOME SIMULATOR
        // ============================================
        public ActionResult Simulate()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var payload = _simulator.BuildPayload(student);

            var vm = new SimulatorViewModel
            {
                StudentName = student.FullName,
                Payload = payload,
                HasBookmarks = payload.Careers.Any(),
                PayloadJson = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                    Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                })
            };

            return View(vm);
        }

        // Authoritative recompute — the client mirrors this arithmetic for
        // instant feedback, but this is the version that is ever trusted.
        // Used by the page's "verify with server" check.
        // marksJson is a plain form field holding a JSON object, e.g.
        // {"3":72,"7":55} — MVC 5 has no automatic JSON-body binding the way
        // Web API does, so the client posts it as a normal form value and this
        // action deserialises it itself rather than fighting the model binder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecomputeOutcomes(string marksJson)
        {
            var student = GetCurrentStudent();
            if (student == null) return new HttpStatusCodeResult(401);

            Dictionary<int, decimal> marks;
            try
            {
                marks = JsonConvert.DeserializeObject<Dictionary<int, decimal>>(marksJson ?? "");
            }
            catch (JsonException)
            {
                return Json(new { error = "Could not read the projected marks." });
            }

            if (marks == null || !marks.Any())
                return Json(new { error = "No projected marks supplied." });

            var result = _simulator.Recompute(student, marks);
            return Json(new
            {
                aps = result.Aps,
                careers = result.Careers.Select(r => new { r.CareerId, r.CareerName, Verdict = r.Verdict.ToString() })
            });
        }

        // Persist a target mark for one subject. Purely exploratory otherwise —
        // nothing is saved until the learner explicitly asks for it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTarget(int subjectId, decimal targetMark)
        {
            var student = GetCurrentStudent();
            if (student == null) return new HttpStatusCodeResult(401);

            if (targetMark < 0 || targetMark > 100)
                return Json(new { success = false, message = "Target mark must be between 0 and 100." });

            _simulator.SaveTarget(student.Id, subjectId, targetMark);
            return Json(new { success = true });
        }

        // ============================================
        // INTEREST QUIZ
        // ============================================
        public ActionResult Quiz()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var vm = new InterestQuizViewModel
            {
                Questions = _context.InterestQuestions
                    .Where(q => q.IsActive)
                    .OrderBy(q => q.Id)
                    .ToList()
            };

            if (!vm.Questions.Any())
            {
                TempData["ErrorMessage"] = "The interest quiz is not available yet.";
                return RedirectToAction("Index");
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitQuiz(InterestQuizSubmission model)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            if (model?.Answers == null || !model.Answers.Any())
            {
                TempData["ErrorMessage"] = "Please answer the questions before submitting.";
                return RedirectToAction("Quiz");
            }

            // Map each answered question to its field and accumulate scores.
            var questionIds = model.Answers.Select(a => a.QuestionId).ToList();
            var questions = _context.InterestQuestions
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionary(q => q.Id, q => q.CareerFieldId);

            var scoreByField = new Dictionary<int, int>();
            foreach (var answer in model.Answers)
            {
                if (!questions.ContainsKey(answer.QuestionId)) continue;
                var fieldId = questions[answer.QuestionId];
                var score = Math.Max(1, Math.Min(5, answer.Score)); // clamp 1..5
                if (!scoreByField.ContainsKey(fieldId)) scoreByField[fieldId] = 0;
                scoreByField[fieldId] += score;
            }

            var topFields = scoreByField
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            var result = new StudentInterestResult
            {
                StudentId = student.Id,
                TakenAt = DateTime.Now,
                RawScoresJson = JsonConvert.SerializeObject(scoreByField),
                TopField1Id = topFields.Count > 0 ? topFields[0] : (int?)null,
                TopField2Id = topFields.Count > 1 ? topFields[1] : (int?)null,
                TopField3Id = topFields.Count > 2 ? topFields[2] : (int?)null
            };

            _context.StudentInterestResults.Add(result);
            _context.SaveChanges();

            return RedirectToAction("QuizResult", new { id = result.Id });
        }

        public ActionResult QuizResult(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var result = _context.StudentInterestResults
                .Include(r => r.TopField1)
                .Include(r => r.TopField2)
                .Include(r => r.TopField3)
                .FirstOrDefault(r => r.Id == id && r.StudentId == student.Id);

            if (result == null) return HttpNotFound();

            var topFieldIds = new List<int>();
            if (result.TopField1Id.HasValue) topFieldIds.Add(result.TopField1Id.Value);
            if (result.TopField2Id.HasValue) topFieldIds.Add(result.TopField2Id.Value);
            if (result.TopField3Id.HasValue) topFieldIds.Add(result.TopField3Id.Value);

            var topFields = new List<CareerField>();
            if (result.TopField1 != null) topFields.Add(result.TopField1);
            if (result.TopField2 != null) topFields.Add(result.TopField2);
            if (result.TopField3 != null) topFields.Add(result.TopField3);

            var aps = _service.CalculateApsForStudent(student.Id);

            // Recommend careers from the top fields, best matches first.
            var careers = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => c.IsActive && topFieldIds.Contains(c.CareerFieldId))
                .ToList();

            var matches = careers
                .Select(c => _service.EvaluateCareer(c, aps))
                .OrderBy(m => VerdictSortOrder(m.Verdict))
                .ThenBy(m => m.Career.Name)
                .Take(9)
                .ToList();

            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();

            var vm = new InterestResultViewModel
            {
                TopFields = topFields,
                RecommendedCareers = matches,
                BookmarkedCareerIds = new HashSet<int>(bookmarkedIds),
                HasReportCard = aps.HasReportCard
            };

            return View(vm);
        }

        // ============================================
        // HELPERS
        // ============================================
        private List<Career> LoadActiveCareers()
        {
            return _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => c.IsActive)
                .ToList();
        }

        private static int VerdictSortOrder(CareerMatchVerdict verdict)
        {
            switch (verdict)
            {
                case CareerMatchVerdict.Qualifies: return 0;
                case CareerMatchVerdict.Close: return 1;
                case CareerMatchVerdict.MissingSubjects: return 2;
                default: return 3;
            }
        }

        private void SetStudentViewBag(Student student)
        {
            ViewBag.ActivePage = "career";
            ViewBag.StudentFirstName = student.FirstName;
            ViewBag.StudentFullName = student.FullName;
            ViewBag.StudentNumber = User.Identity.Name;
        }

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}
