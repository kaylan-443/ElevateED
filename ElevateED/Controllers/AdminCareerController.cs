using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.ViewModels;
using Newtonsoft.Json;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin,Principal")]
    public class AdminCareerController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // OVERVIEW + INTEREST ANALYTICS
        // ============================================
        public ActionResult Index()
        {
            var vm = new AdminCareerOverviewViewModel
            {
                FieldCount = _context.CareerFields.Count(),
                CareerCount = _context.Careers.Count(),
                QuestionCount = _context.InterestQuestions.Count(),
                QuizzesTaken = _context.StudentInterestResults.Count()
            };

            // Distribution of learners' top field (latest result per student).
            var latestPerStudent = _context.StudentInterestResults
                .GroupBy(r => r.StudentId)
                .Select(g => g.OrderByDescending(r => r.TakenAt).FirstOrDefault())
                .ToList();

            var byField = latestPerStudent
                .Where(r => r != null && r.TopField1Id.HasValue)
                .GroupBy(r => r.TopField1Id.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var fields = _context.CareerFields.OrderBy(f => f.Name).ToList();
            vm.InterestDistribution = fields
                .Select(f => new FieldCountItem
                {
                    FieldName = f.Name,
                    Count = byField.ContainsKey(f.Id) ? byField[f.Id] : 0
                })
                .ToList();

            return View(vm);
        }

        // ============================================
        // CAREER FIELDS
        // ============================================
        public ActionResult Fields()
        {
            var fields = _context.CareerFields
                .OrderBy(f => f.Name)
                .ToList();
            ViewBag.CareerCountByField = _context.Careers
                .GroupBy(c => c.CareerFieldId)
                .ToDictionary(g => g.Key, g => g.Count());
            return View(fields);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveField(int id, string name, string description, string iconClass, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Field name is required.";
                return RedirectToAction("Fields");
            }

            if (id == 0)
            {
                _context.CareerFields.Add(new CareerField
                {
                    Name = name.Trim(),
                    Description = description,
                    IconClass = string.IsNullOrWhiteSpace(iconClass) ? "briefcase" : iconClass.Trim(),
                    IsActive = isActive
                });
                TempData["SuccessMessage"] = "Career field added.";
            }
            else
            {
                var field = _context.CareerFields.Find(id);
                if (field != null)
                {
                    field.Name = name.Trim();
                    field.Description = description;
                    field.IconClass = string.IsNullOrWhiteSpace(iconClass) ? "briefcase" : iconClass.Trim();
                    field.IsActive = isActive;
                    TempData["SuccessMessage"] = "Career field updated.";
                }
            }
            _context.SaveChanges();
            return RedirectToAction("Fields");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteField(int id)
        {
            var field = _context.CareerFields.Find(id);
            if (field != null)
            {
                if (_context.Careers.Any(c => c.CareerFieldId == id))
                {
                    TempData["ErrorMessage"] = "Remove or reassign careers in this field first.";
                    return RedirectToAction("Fields");
                }
                _context.CareerFields.Remove(field);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Career field deleted.";
            }
            return RedirectToAction("Fields");
        }

        // ============================================
        // CAREERS
        // ============================================
        public ActionResult Careers(int? fieldId)
        {
            var query = _context.Careers
                .Include(c => c.CareerField)
                .Include(c => c.SubjectRequirements)
                .AsQueryable();

            if (fieldId.HasValue)
                query = query.Where(c => c.CareerFieldId == fieldId.Value);

            var careers = query.OrderBy(c => c.CareerField.Name).ThenBy(c => c.Name).ToList();

            ViewBag.Fields = _context.CareerFields.OrderBy(f => f.Name).ToList();
            ViewBag.SelectedFieldId = fieldId;
            return View(careers);
        }

        public ActionResult CareerForm(int? id)
        {
            var vm = new AdminCareerFormViewModel
            {
                Fields = _context.CareerFields.OrderBy(f => f.Name).ToList(),
                Subjects = _context.Subjects.OrderBy(s => s.Name).ToList()
            };

            if (id.HasValue)
            {
                var career = _context.Careers
                    .Include(c => c.SubjectRequirements)
                    .FirstOrDefault(c => c.Id == id.Value);
                if (career == null) return HttpNotFound();

                vm.Id = career.Id;
                vm.CareerFieldId = career.CareerFieldId;
                vm.Name = career.Name;
                vm.Description = career.Description;
                vm.TypicalQualification = career.TypicalQualification;
                vm.WhereToStudy = career.WhereToStudy;
                vm.MinimumAps = career.MinimumAps;
                vm.IsActive = career.IsActive;
                vm.Requirements = career.SubjectRequirements
                    .Select(r => new AdminRequirementRow
                    {
                        SubjectId = r.SubjectId,
                        MinimumLevel = r.MinimumLevel,
                        IsCompulsory = r.IsCompulsory
                    }).ToList();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCareer(AdminCareerFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || model.CareerFieldId == 0)
            {
                TempData["ErrorMessage"] = "Career name and field are required.";
                return RedirectToAction("CareerForm", new { id = model.Id == 0 ? (int?)null : model.Id });
            }

            Career career;
            if (model.Id == 0)
            {
                career = new Career();
                _context.Careers.Add(career);
            }
            else
            {
                career = _context.Careers
                    .Include(c => c.SubjectRequirements)
                    .FirstOrDefault(c => c.Id == model.Id);
                if (career == null) return HttpNotFound();

                // Clear existing requirements; they are rebuilt from the form.
                foreach (var r in career.SubjectRequirements.ToList())
                    _context.CareerSubjectRequirements.Remove(r);
            }

            career.CareerFieldId = model.CareerFieldId;
            career.Name = model.Name.Trim();
            career.Description = model.Description;
            career.TypicalQualification = model.TypicalQualification;
            career.WhereToStudy = model.WhereToStudy;
            career.MinimumAps = model.MinimumAps;
            career.IsActive = model.IsActive;

            if (model.Requirements != null)
            {
                foreach (var row in model.Requirements)
                {
                    if (row.SubjectId <= 0) continue; // skip empty rows
                    career.SubjectRequirements.Add(new CareerSubjectRequirement
                    {
                        SubjectId = row.SubjectId,
                        MinimumLevel = Math.Max(1, Math.Min(7, row.MinimumLevel)),
                        IsCompulsory = row.IsCompulsory
                    });
                }
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = model.Id == 0 ? "Career added." : "Career updated.";
            return RedirectToAction("Careers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCareer(int id)
        {
            var career = _context.Careers
                .Include(c => c.SubjectRequirements)
                .FirstOrDefault(c => c.Id == id);
            if (career != null)
            {
                var bookmarks = _context.StudentCareerBookmarks.Where(b => b.CareerId == id).ToList();
                _context.StudentCareerBookmarks.RemoveRange(bookmarks);

                // Detach any study plans that had this career as their goal.
                foreach (var plan in _context.StudyPlans.Where(p => p.GoalCareerId == id))
                    plan.GoalCareerId = null;

                _context.CareerSubjectRequirements.RemoveRange(career.SubjectRequirements);
                _context.Careers.Remove(career);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Career deleted.";
            }
            return RedirectToAction("Careers");
        }

        // ============================================
        // INTEREST QUESTIONS
        // ============================================
        public ActionResult Questions()
        {
            var questions = _context.InterestQuestions
                .Include(q => q.CareerField)
                .OrderBy(q => q.CareerField.Name)
                .ThenBy(q => q.Id)
                .ToList();
            ViewBag.Fields = _context.CareerFields.OrderBy(f => f.Name).ToList();
            return View(questions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveQuestion(int id, string text, int careerFieldId, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(text) || careerFieldId == 0)
            {
                TempData["ErrorMessage"] = "Question text and a career field are required.";
                return RedirectToAction("Questions");
            }

            if (id == 0)
            {
                _context.InterestQuestions.Add(new InterestQuestion
                {
                    Text = text.Trim(),
                    CareerFieldId = careerFieldId,
                    IsActive = isActive
                });
                TempData["SuccessMessage"] = "Question added.";
            }
            else
            {
                var q = _context.InterestQuestions.Find(id);
                if (q != null)
                {
                    q.Text = text.Trim();
                    q.CareerFieldId = careerFieldId;
                    q.IsActive = isActive;
                    TempData["SuccessMessage"] = "Question updated.";
                }
            }
            _context.SaveChanges();
            return RedirectToAction("Questions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteQuestion(int id)
        {
            var q = _context.InterestQuestions.Find(id);
            if (q != null)
            {
                _context.InterestQuestions.Remove(q);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Question deleted.";
            }
            return RedirectToAction("Questions");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}
