using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize]
    public class PodcastController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private GeminiService _geminiService = new GeminiService();
        private PdfExtractionService _pdfService = new PdfExtractionService();
        private PodcastService _podcastService = new PodcastService();

        public ActionResult Index()
        {
            var studentId = GetCurrentStudentId();
            var model = new PodcastIndexViewModel
            {
                PodcastHistory = _podcastService.GetStudentPodcasts(studentId)
            };
            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> UploadNotes(HttpPostedFileBase file)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
                return Json(new { success = false, message = "Student not found." });

            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, message = "Please select a file." });

            if (file.ContentLength > 20 * 1024 * 1024)
                return Json(new { success = false, message = "File too large (max 20MB)." });

            string extractedText;
            using (var stream = file.InputStream)
            {
                extractedText = await _pdfService.ExtractTextAsync(stream, file.FileName);
            }

            if (string.IsNullOrEmpty(extractedText) || extractedText.StartsWith("Error"))
                return Json(new { success = false, message = extractedText ?? "Could not extract text." });

            var podcast = new PodcastHistory
            {
                StudentId = studentId,
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                OriginalFileName = file.FileName,
                ExtractedText = TruncateText(extractedText, 10000),
                Status = "Processing"
            };

            _context.PodcastHistories.Add(podcast);
            await _context.SaveChangesAsync();

            var result = await _geminiService.GeneratePodcastScriptAsync(extractedText);

            podcast.GeneratedScript = result.Script;
            podcast.Status = result.Success ? "Completed" : "Failed";
            podcast.ErrorMessage = result.ErrorMessage;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                podcastId = podcast.PodcastHistoryId,
                script = result.Script,
                title = podcast.Title
            });
        }

        [HttpPost]
        public async Task<ActionResult> GenerateFromText(string notesText, string title)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
                return Json(new { success = false, message = "Student not found." });

            if (string.IsNullOrWhiteSpace(notesText))
                return Json(new { success = false, message = "Please enter text." });

            var podcast = new PodcastHistory
            {
                StudentId = studentId,
                Title = !string.IsNullOrEmpty(title) ? title : "Study Notes",
                ExtractedText = TruncateText(notesText, 10000),
                Status = "Processing"
            };

            _context.PodcastHistories.Add(podcast);
            await _context.SaveChangesAsync();

            var result = await _geminiService.GeneratePodcastScriptAsync(notesText);

            podcast.GeneratedScript = result.Script;
            podcast.Status = result.Success ? "Completed" : "Failed";
            podcast.ErrorMessage = result.ErrorMessage;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                podcastId = podcast.PodcastHistoryId,
                script = result.Script,
                title = podcast.Title
            });
        }

        public ActionResult MyPodcasts()
        {
            var studentId = GetCurrentStudentId();
            var podcasts = _podcastService.GetStudentPodcasts(studentId);
            return View(podcasts);
        }

        [HttpGet]
        public ActionResult GetPodcast(int id)
        {
            var podcast = _context.PodcastHistories.Find(id);
            if (podcast == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                podcastId = podcast.PodcastHistoryId,
                title = podcast.Title,
                script = podcast.GeneratedScript,
                status = podcast.Status,
                createdAt = podcast.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult DeletePodcast(int id)
        {
            var podcast = _context.PodcastHistories.Find(id);
            if (podcast == null)
                return Json(new { success = false });

            _context.PodcastHistories.Remove(podcast);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> GenerateAudio(int podcastId, string voice = "nova")
        {
            var podcast = _context.PodcastHistories.Find(podcastId);
            if (podcast == null)
                return Json(new { success = false, message = "Podcast not found" });

            try
            {
                var ttsService = new OpenAITTSService();
                var audioBytes = await ttsService.GenerateSpeechAsync(
                    podcast.GeneratedScript,
                    voice: voice,
                    instructions: "Speak in a warm, engaging, friendly teacher voice. Sound natural and conversational."
                );

                if (audioBytes != null)
                {
                    var fileName = $"podcast_{podcastId}_{DateTime.Now:yyyyMMddHHmmss}.mp3";
                    var filePath = Server.MapPath("~/Uploads/Podcasts/" + fileName);
                    var directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    System.IO.File.WriteAllBytes(filePath, audioBytes);

                    podcast.AudioUrl = "/Uploads/Podcasts/" + fileName;
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, audioUrl = podcast.AudioUrl });
                }

                return Json(new { success = false, message = "OpenAI API key not configured." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private int GetCurrentStudentId()
        {
            var studentNumber = User.Identity.Name;
            return _podcastService.GetStudentId(studentNumber);
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}