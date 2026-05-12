using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElevateED.Models;
using Newtonsoft.Json;

namespace ElevateED.Services
{
    public class AIAnnouncementService
    {
        private readonly ElevateEDContext _context;
        private readonly string _groqApiKey;
        private static readonly HttpClient _httpClient;

        static AIAnnouncementService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        }

        public AIAnnouncementService()
        {
            _context = new ElevateEDContext();
            _groqApiKey = ConfigurationManager.AppSettings["GroqApiKey"] ?? "";
        }

        public async Task<GeneratedAnnouncementResult> GenerateAnnouncementAsync(AnnouncementGeneratorSession session)
        {
            if (!string.IsNullOrEmpty(_groqApiKey))
            {
                try
                {
                    var result = await GenerateWithGroqAsync(session);
                    if (result.Success)
                    {
                        result.GeneratedBy = "AI";
                        SaveToSession(session, result);
                        return result;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Groq failed: " + ex.Message); }
            }
            var fallback = GenerateWithTemplate(session);
            fallback.GeneratedBy = "Template";
            return fallback;
        }

        private async Task<GeneratedAnnouncementResult> GenerateWithGroqAsync(AnnouncementGeneratorSession session)
        {
            var result = new GeneratedAnnouncementResult();
            try
            {
                var prompt = BuildPrompt(session);
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a school announcement writer. Write complete announcements in plain text with greeting, body, dates, call to action, and closing." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 2048
                };

                var json = JsonConvert.SerializeObject(requestBody);
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions"))
                {
                    request.Headers.Add("Authorization", "Bearer " + _groqApiKey);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.SendAsync(request);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic aiResponse = JsonConvert.DeserializeObject<dynamic>(responseText);
                        var aiText = (string)aiResponse.choices[0].message.content;
                        if (!string.IsNullOrEmpty(aiText))
                        {
                            result.Content = CleanText(aiText);
                            result.Title = GenerateSmartTitle(session);
                            result.EmailSubject = (session.IsUrgent ? "[URGENT] " : "") + "[Mpiyakhe HS] " + result.Title;
                            result.Summary = GenerateSummary(session, result.Content);
                            result.HtmlContent = TextToHtml(result.Content, result.Title, session);
                            result.Success = true;
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Groq error: " + ex.Message); }
            return result;
        }

        private string BuildPrompt(AnnouncementGeneratorSession session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Write a " + session.Tone.ToLower() + " school announcement for Mpiyakhe High School.");
            sb.AppendLine("TOPIC: " + session.Topic);
            sb.AppendLine("AUDIENCE: " + session.TargetAudience);
            if (session.IsUrgent) sb.AppendLine("URGENT - emphasize action required");
            if (session.ImportantDate.HasValue) sb.AppendLine("DATE: " + session.ImportantDate.Value.ToString("dddd, dd MMMM yyyy"));
            if (!string.IsNullOrEmpty(session.ImportantDateDescription)) sb.AppendLine("EVENT: " + session.ImportantDateDescription);
            if (session.DeadlineDate.HasValue) sb.AppendLine("DEADLINE: " + session.DeadlineDate.Value.ToString("dddd, dd MMMM yyyy"));
            if (!string.IsNullOrEmpty(session.AdditionalNotes)) sb.AppendLine("INCLUDE: " + session.AdditionalNotes);
            sb.AppendLine("Write the complete announcement now.");
            return sb.ToString();
        }

        private void SaveToSession(AnnouncementGeneratorSession session, GeneratedAnnouncementResult result)
        {
            session.GeneratedTitle = result.Title;
            session.GeneratedContent = result.Content;
            session.GeneratedSummary = result.Summary;
            session.GeneratedSubject = result.EmailSubject;
            session.CompletedAt = DateTime.Now;
            session.Status = "Completed";
        }

        private GeneratedAnnouncementResult GenerateWithTemplate(AnnouncementGeneratorSession session)
        {
            var result = new GeneratedAnnouncementResult();
            result.Title = GenerateSmartTitle(session);
            result.Content = GenerateSmartContent(session);
            result.Summary = GenerateSummary(session, result.Content);
            result.EmailSubject = (session.IsUrgent ? "[URGENT] " : "") + "[Mpiyakhe HS] " + result.Title;
            result.HtmlContent = TextToHtml(result.Content, result.Title, session);
            result.Success = true;
            result.GeneratedBy = "Template";
            return result;
        }

        private string GenerateSmartTitle(AnnouncementGeneratorSession session)
        {
            var parts = new List<string>();
            if (session.IsUrgent) parts.Add("URGENT");
            parts.Add(string.IsNullOrEmpty(session.Topic) ? "School Announcement" : session.Topic);
            if (session.ImportantDate.HasValue)
                parts[parts.Count - 1] += " (" + session.ImportantDate.Value.ToString("dd MMM") + ")";
            return string.Join(" - ", parts);
        }

        private string GenerateSmartContent(AnnouncementGeneratorSession session)
        {
            var extra = !string.IsNullOrEmpty(session.AdditionalNotes) ? session.AdditionalNotes.Trim() : "";
            var sb = new StringBuilder();
            string audience = session.TargetAudience == "Students Only" ? "Learners" :
                             (session.TargetAudience == "Teachers Only" ? "Teachers" : "Mpiyakhe High School Community");
            sb.AppendLine("Dear " + audience + ",");
            sb.AppendLine();
            string opener = session.Tone == "Formal" ? "This is an official communication regarding" :
                           session.Tone == "Friendly" ? "We are excited to share an update about" :
                           session.Tone == "Warning" ? "Please be advised regarding" :
                           "This is regarding";
            sb.AppendLine(opener + " " + session.Topic + ".");
            if (!string.IsNullOrEmpty(extra)) sb.AppendLine(extra);
            sb.AppendLine();
            if (session.ImportantDate.HasValue || session.DeadlineDate.HasValue)
            {
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  📅  IMPORTANT DATES");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
                if (session.ImportantDate.HasValue)
                {
                    sb.AppendLine("  📌 " + session.ImportantDate.Value.ToString("dddd, dd MMMM yyyy"));
                    if (!string.IsNullOrEmpty(session.ImportantDateDescription))
                        sb.AppendLine("      " + session.ImportantDateDescription);
                    sb.AppendLine();
                }
                if (session.DeadlineDate.HasValue)
                {
                    var d = (session.DeadlineDate.Value - DateTime.Now).Days;
                    sb.AppendLine("  ⏰ Deadline: " + session.DeadlineDate.Value.ToString("dd MMM yyyy") + (d > 0 ? " (" + d + " days)" : ""));
                    sb.AppendLine();
                }
            }
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine(session.IsUrgent ? "  ⚠️  Please take immediate action." : "  📌  For questions, contact the school administration.");
            sb.AppendLine();
            sb.AppendLine("Best regards,");
            sb.AppendLine("Mpiyakhe High School Administration");
            sb.AppendLine("──────────────────────────────────────────");
            sb.AppendLine("Sent via ElevateED | Empowering Future Leaders");
            return sb.ToString();
        }

        private string GenerateSummary(AnnouncementGeneratorSession s, string c)
        {
            return (s.IsUrgent ? "🚨 " : "📢 ") + (s.Topic.Length > 80 ? s.Topic.Substring(0, 77) + "..." : s.Topic) +
                   (s.ImportantDate.HasValue ? " | " + s.ImportantDate.Value.ToString("dd MMM") : "");
        }

        private string CleanText(string t)
        {
            if (string.IsNullOrEmpty(t)) return t;
            t = System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");
            return System.Net.WebUtility.HtmlDecode(t).Trim();
        }

        private string TextToHtml(string text, string title, AnnouncementGeneratorSession session)
        {
            var c = session.IsUrgent ? "#dc3545" : "#1e3c72";
            var escaped = System.Net.WebUtility.HtmlEncode(text).Replace("\n", "<br/>");
            return "<!DOCTYPE html><html><head><meta charset='utf-8'/></head>" +
                   "<body style='font-family:Arial,sans-serif;margin:0;padding:0;background:#f4f4f4;'>" +
                   "<div style='max-width:600px;margin:0 auto;background:white;border-radius:12px;overflow:hidden;'>" +
                   "<div style='background:" + c + ";color:white;padding:30px;text-align:center;'>" +
                   "<h1 style='margin:0;font-size:22px;'>" + System.Net.WebUtility.HtmlEncode(title) + "</h1></div>" +
                   "<div style='padding:40px;'><div style='font-size:15px;line-height:1.8;color:#333;'>" + escaped + "</div></div>" +
                   "<div style='background:#1e3c72;padding:20px;text-align:center;font-size:11px;color:#94a3b8;'>" +
                   "Sent via ElevateED | Mpiyakhe High School</div></div></body></html>";
        }

        public List<AnnouncementTemplate> GetTemplates()
        {
            return _context.AnnouncementTemplates.Where(t => t.IsActive).OrderByDescending(t => t.UsageCount).ToList();
        }

        public AnnouncementAnalytics GetAnalytics(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Announcements.Where(ann => ann.IsActive);
            if (fromDate.HasValue) query = query.Where(ann => ann.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(ann => ann.CreatedAt <= toDate.Value);
            var announcements = query.ToList();
            return new AnnouncementAnalytics
            {
                TotalAnnouncements = announcements.Count,
                UrgentCount = announcements.Count(x => x.AnnouncementType == "Urgent"),
                ScheduledCount = announcements.Count(x => x.IsScheduled && !x.IsSent),
                SentCount = announcements.Count(x => x.IsSent),
                AIAssistedCount = announcements.Count(x => x.IsAIGenerated)
            };
        }
    }

    public class GeneratedAnnouncementResult
    {
        public bool Success { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string HtmlContent { get; set; }
        public string Summary { get; set; }
        public string EmailSubject { get; set; }
        public string ErrorMessage { get; set; }
        public string GeneratedBy { get; set; }
    }

    public class AnnouncementAnalytics
    {
        public int TotalAnnouncements { get; set; }
        public int UrgentCount { get; set; }
        public int ScheduledCount { get; set; }
        public int SentCount { get; set; }
        public double AverageViewCount { get; set; }
        public int TotalEmailSent { get; set; }
        public int AIAssistedCount { get; set; }
    }
}