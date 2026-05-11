using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.ViewModels;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentQuizController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // QUIZ HOME - SELECT SUBJECT
        // ============================================

        public ActionResult Index()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            // Get subjects that have quiz questions
            var subjects = _context.QuizQuestions
                .Where(q => q.IsActive)
                .Select(q => q.Subject)
                .Distinct()
                .OrderBy(s => s.Name)
                .ToList();

            // Get recent attempts
            var recentAttempts = _context.QuizAttempts
                .Include(a => a.Subject)
                .Where(a => a.StudentId == student.Id)
                .OrderByDescending(a => a.AttemptedAt)
                .Take(5)
                .ToList();

            ViewBag.RecentAttempts = recentAttempts;

            return View(subjects);
        }

        // ============================================
        // GENERATE QUIZ
        // ============================================

        public ActionResult Generate(int subjectId, int count = 10)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var subject = _context.Subjects.Find(subjectId);
            if (subject == null) return RedirectToAction("Index");

            // Get random questions for this subject
            var questions = _context.QuizQuestions
                .Where(q => q.SubjectId == subjectId && q.IsActive)
                .OrderBy(q => Guid.NewGuid()) // Random order
                .Take(count)
                .ToList();

            if (!questions.Any())
            {
                TempData["ErrorMessage"] = "No questions available for this subject yet.";
                return RedirectToAction("Index");
            }

            var viewModel = new QuizViewModel
            {
                SubjectId = subjectId,
                SubjectName = subject.Name,
                Questions = questions.Select(q => new QuestionViewModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                }).ToList()
            };

            return View(viewModel);
        }

        // ============================================
        // SUBMIT QUIZ
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(QuizSubmissionViewModel model)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            if (model.Answers == null || !model.Answers.Any())
            {
                TempData["ErrorMessage"] = "Please answer all questions.";
                return RedirectToAction("Generate", new { subjectId = model.SubjectId });
            }

            // Get the actual questions from database
            var questionIds = model.Answers.Select(a => a.QuestionId).ToList();
            var questions = _context.QuizQuestions
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionary(q => q.Id);

            int correctCount = 0;
            var answers = new List<QuizAnswer>();

            foreach (var answer in model.Answers)
            {
                if (questions.ContainsKey(answer.QuestionId))
                {
                    var question = questions[answer.QuestionId];
                    bool isCorrect = question.CorrectAnswer == answer.SelectedAnswer;

                    if (isCorrect) correctCount++;

                    answers.Add(new QuizAnswer
                    {
                        QuestionId = answer.QuestionId,
                        SelectedAnswer = answer.SelectedAnswer,
                        IsCorrect = isCorrect
                    });
                }
            }

            // Create quiz attempt
            var attempt = new QuizAttempt
            {
                StudentId = student.Id,
                SubjectId = model.SubjectId,
                TotalQuestions = model.Answers.Count,
                CorrectAnswers = correctCount,
                Score = Math.Round((decimal)correctCount / model.Answers.Count * 100, 2),
                AttemptedAt = DateTime.Now,
                Answers = answers
            };

            _context.QuizAttempts.Add(attempt);
            _context.SaveChanges();

            // Redirect to results
            return RedirectToAction("Results", new { attemptId = attempt.Id });
        }

        // ============================================
        // VIEW RESULTS
        // ============================================

        public ActionResult Results(int attemptId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var attempt = _context.QuizAttempts
                .Include(a => a.Subject)
                .Include(a => a.Answers.Select(ans => ans.Question))
                .FirstOrDefault(a => a.Id == attemptId && a.StudentId == student.Id);

            if (attempt == null) return HttpNotFound();

            var viewModel = new QuizResultViewModel
            {
                AttemptId = attempt.Id,
                SubjectName = attempt.Subject?.Name,
                TotalQuestions = attempt.TotalQuestions,
                CorrectAnswers = attempt.CorrectAnswers,
                Score = attempt.Score,
                AttemptedAt = attempt.AttemptedAt,
                Answers = attempt.Answers.Select(a => new AnswerResultViewModel
                {
                    QuestionText = a.Question?.QuestionText ?? "N/A",
                    SelectedAnswer = a.SelectedAnswer,
                    CorrectAnswer = a.Question?.CorrectAnswer ?? "N/A",
                    IsCorrect = a.IsCorrect,
                    OptionA = a.Question?.OptionA ?? "",
                    OptionB = a.Question?.OptionB ?? "",
                    OptionC = a.Question?.OptionC ?? "",
                    OptionD = a.Question?.OptionD ?? ""
                }).ToList()
            };

            return View(viewModel);
        }

        // ============================================
        // QUIZ HISTORY
        // ============================================

        public ActionResult History()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var attempts = _context.QuizAttempts
                .Include(a => a.Subject)
                .Where(a => a.StudentId == student.Id)
                .OrderByDescending(a => a.AttemptedAt)
                .ToList();

            return View(attempts);
        }

        // ============================================
        // HELPER
        // ============================================

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
    }
}