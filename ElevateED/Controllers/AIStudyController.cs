using ElevateED.Models;
using ElevateED.Services;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize]
    public class AIStudyController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private AIStudyService _aiService = new AIStudyService();
        private PdfExtractionService _pdfService = new PdfExtractionService();

        // ============================================
        // LANDING PAGE - Shows all sessions
        // ============================================
        public ActionResult Index()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var sessions = _context.AIStudySessions
                .Where(s => s.StudentId == studentId && s.IsActive)
                .OrderByDescending(s => s.LastAccessedDate)
                .ToList();

            return View(sessions);
        }

        // ============================================
        // CREATE NEW SESSION (Upload)
        // ============================================
        [HttpPost]
        public async Task<ActionResult> CreateSession(HttpPostedFileBase file, string pasteText, string sessionTitle)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            string extractedText = null;
            string originalFileName = null;

            // Handle file upload
            if (file != null && file.ContentLength > 0)
            {
                if (file.ContentLength > 20 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "File too large (max 20MB).";
                    return RedirectToAction("Index");
                }

                using (var stream = file.InputStream)
                {
                    extractedText = await _pdfService.ExtractTextAsync(stream, file.FileName);
                }

                if (string.IsNullOrEmpty(extractedText) || extractedText.StartsWith("Error"))
                {
                    TempData["ErrorMessage"] = extractedText ?? "Could not extract text.";
                    return RedirectToAction("Index");
                }

                originalFileName = file.FileName;
            }
            // Handle pasted text
            else if (!string.IsNullOrWhiteSpace(pasteText))
            {
                extractedText = pasteText.Trim();
                originalFileName = "Pasted Notes";
            }
            else
            {
                TempData["ErrorMessage"] = "Please upload a file or paste text.";
                return RedirectToAction("Index");
            }

            // Create session
            var session = new AIStudySession
            {
                StudentId = studentId,
                SessionTitle = !string.IsNullOrWhiteSpace(sessionTitle) ? sessionTitle : "Study Session",
                OriginalFileName = originalFileName,
                ExtractedText = extractedText,
                CreatedDate = DateTime.Now,
                LastAccessedDate = DateTime.Now
            };

            _context.AIStudySessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction("Workspace", new { id = session.Id });
        }

        // ============================================
        // WORKSPACE - Persistent study workspace
        // ============================================
        public ActionResult Workspace(int id)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var session = _context.AIStudySessions
                .Include(s => s.Outputs)
                .FirstOrDefault(s => s.Id == id && s.StudentId == studentId);

            if (session == null)
                return RedirectToAction("Index");

            // Update last accessed
            session.LastAccessedDate = DateTime.Now;
            _context.SaveChanges();

            return View(session);
        }

        // ============================================
        // GENERATE CONTENT (AJAX)
        // ============================================
        [HttpPost]
        public async Task<ActionResult> GenerateContent(int sessionId, string contentType)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0) return Json(new { success = false, message = "Unauthorized" });

            var session = _context.AIStudySessions.Find(sessionId);
            if (session == null || session.StudentId != studentId)
                return Json(new { success = false, message = "Session not found" });

            if (string.IsNullOrWhiteSpace(session.ExtractedText))
                return Json(new { success = false, message = "No notes found in this session" });

            string generatedContent = null;

            switch (contentType)
            {
                case "flashcards":
                    generatedContent = await _aiService.GenerateFlashcards(session.ExtractedText);
                    break;
                case "simplify":
                    generatedContent = await _aiService.SimplifyNotes(session.ExtractedText);
                    break;
                case "examnotes":
                    generatedContent = await _aiService.GenerateExamNotes(session.ExtractedText);
                    break;
                case "quiz":
                    generatedContent = await _aiService.GenerateQuiz(session.ExtractedText);
                    break;
                default:
                    return Json(new { success = false, message = "Invalid content type" });
            }

            // Save output
            var output = new AIStudyOutput
            {
                SessionId = sessionId,
                ContentType = contentType,
                GeneratedContent = generatedContent,
                CreatedDate = DateTime.Now
            };

            _context.AIStudyOutputs.Add(output);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                outputId = output.Id,
                content = generatedContent,
                contentType = contentType
            });
        }

        // ============================================
        // GET SPECIFIC OUTPUT (AJAX)
        // ============================================
        [HttpGet]
        public ActionResult GetOutput(int outputId)
        {
            var studentId = GetCurrentStudentId();
            var output = _context.AIStudyOutputs
                .Include(o => o.Session)
                .FirstOrDefault(o => o.Id == outputId && o.Session.StudentId == studentId);

            if (output == null)
                return Json(new { success = false, message = "Output not found" }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                content = output.GeneratedContent,
                contentType = output.ContentType,
                outputId = output.Id
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================
        // DELETE SESSION
        // ============================================
        [HttpPost]
        public ActionResult DeleteSession(int id)
        {
            var studentId = GetCurrentStudentId();
            var session = _context.AIStudySessions.Find(id);

            if (session == null || session.StudentId != studentId)
                return Json(new { success = false });

            session.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // ============================================
        // TOGGLE FAVORITE
        // ============================================
        [HttpPost]
        public ActionResult ToggleFavorite(int outputId)
        {
            var output = _context.AIStudyOutputs.Find(outputId);
            if (output == null) return Json(new { success = false });

            output.IsFavorite = !output.IsFavorite;
            _context.SaveChanges();

            return Json(new { success = true, isFavorite = output.IsFavorite });
        }

        // ============================================
        // HELPER
        // ============================================
        private int GetCurrentStudentId()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return 0;
            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);
            return student?.Id ?? 0;
        }
    }
}