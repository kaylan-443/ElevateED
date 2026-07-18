using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAnnouncementController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private AIAnnouncementService _aiService = new AIAnnouncementService();
        private EmailService _emailService = new EmailService();

        public ActionResult Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var recent = _context.Announcements.Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).Take(20).ToList();
            ViewBag.TotalSent = _context.Announcements.Count(a => a.IsSent);
            ViewBag.TotalDrafts = _context.Announcements.Count(a => !a.IsSent);
            return View(recent);
        }

        public ActionResult CreateWizard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(new AnnouncementWizardViewModel());
        }

        [HttpPost]
        public async Task<ActionResult> StartWizard(AnnouncementWizardViewModel model)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var session = new AnnouncementGeneratorSession
            {
                AdminId = GetCurrentUserId(),
                Topic = model.Topic,
                TargetAudience = model.TargetAudience,
                TargetGrade = model.TargetGrade,
                TargetClass = model.TargetClass,
                IsUrgent = model.IsUrgent,
                ImportantDateDescription = model.ImportantDateDescription,
                ImportantDate = model.ImportantDate,
                DeadlineDate = model.DeadlineDate,
                Tone = model.Tone,
                AdditionalNotes = model.AdditionalNotes
            };

            _context.AnnouncementGeneratorSessions.Add(session);
            await _context.SaveChangesAsync();

            var result = await _aiService.GenerateAnnouncementAsync(session);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                sessionId = session.Id,
                generatedBy = result.GeneratedBy,
                result = new
                {
                    title = result.Title,
                    content = result.Content,
                    summary = result.Summary,
                    emailSubject = result.EmailSubject
                }
            });
        }

        [HttpPost]
        public async Task<ActionResult> RegenerateContent(int sessionId, string feedback)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var session = _context.AnnouncementGeneratorSessions.Find(sessionId);
            if (session == null) return Json(new { success = false, message = "Session not found" });

            if (!string.IsNullOrEmpty(session.AdditionalNotes))
                session.AdditionalNotes += " | " + feedback;
            else
                session.AdditionalNotes = feedback;

            var result = await _aiService.GenerateAnnouncementAsync(session);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                generatedBy = result.GeneratedBy,
                result = new
                {
                    title = result.Title,
                    content = result.Content,
                    summary = result.Summary,
                    emailSubject = result.EmailSubject
                }
            });
        }

        [HttpPost]
        public ActionResult PreviewAndSave(int sessionId, string title, string content,
            string summary, string emailSubject, bool sendNow = false)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var session = _context.AnnouncementGeneratorSessions.Find(sessionId);
            if (session == null) return Json(new { success = false, message = "Session not found" });

            var announcement = new Announcement
            {
                Title = title,
                Content = content,
                Summary = summary,
                TargetAudience = session.TargetAudience,
                TargetGrade = session.TargetGrade,
                TargetClass = session.TargetClass,
                AnnouncementType = session.IsUrgent ? "Urgent" : "General",
                Tone = session.Tone,
                ImportantDate = session.ImportantDate,
                DeadlineDate = session.DeadlineDate,
                ImportantDateDescription = session.ImportantDateDescription,
                IsAIGenerated = true,
                CreatedBy = session.AdminId,
                IsActive = true,
                IsSent = sendNow,
                CreatedAt = DateTime.Now
            };

            _context.Announcements.Add(announcement);
            _context.SaveChanges();

            session.CreatedAnnouncementId = announcement.Id;
            _context.SaveChanges();

            if (sendNow)
            {
                try
                {
                    int sentCount = SendAnnouncementEmails(announcement);
                    announcement.IsSent = true;
                    announcement.EmailSentCount = sentCount;
                    _context.SaveChanges();
                    return Json(new { success = true, message = "Announcement sent to " + sentCount + " recipients!" });
                }
                catch (Exception ex)
                {
                    announcement.IsSent = false;
                    _context.SaveChanges();
                    return Json(new { success = true, message = "Saved as draft. Email error: " + ex.Message });
                }
            }

            return Json(new { success = true, message = "Announcement saved as draft!" });
        }

        private int SendAnnouncementEmails(Announcement announcement)
        {
            int sentCount = 0;
            var users = GetTargetUsers(announcement);

            foreach (var user in users)
            {
                try
                {
                    string emailBody = FormatEmailBody(announcement.Content, announcement.Title,
                        announcement.AnnouncementType == "Urgent");
                    string subject = (announcement.AnnouncementType == "Urgent" ? "[URGENT] " : "") +
                                   "[Mpiyakhe HS] " + announcement.Title;
                    _emailService.SendCustomEmail(user.Email, subject, emailBody);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Email failed: " + ex.Message);
                }
            }
            return sentCount;
        }

        private string FormatEmailBody(string content, string title, bool isUrgent)
        {
            var headerColor = isUrgent ? "#dc3545" : "#1e3c72";
            var escapedContent = System.Net.WebUtility.HtmlEncode(content).Replace("\n", "<br/>");
            return string.Format(@"
<!DOCTYPE html><html><head><meta charset='utf-8'/></head>
<body style='font-family:Arial,sans-serif;margin:0;padding:0;background:#f4f6f8;'>
<div style='max-width:600px;margin:0 auto;background:white;border-radius:12px;overflow:hidden;'>
<div style='background:{0};color:white;padding:30px;text-align:center;'><h1 style='margin:0;font-size:22px;'>{1}</h1></div>
<div style='background:#f8f9fa;padding:15px;text-align:center;'>🏫 Mpiyakhe High School</div>
<div style='padding:40px;'><div style='font-size:15px;line-height:1.8;color:#333;'>{2}</div></div>
<div style='background:#1e3c72;padding:20px;text-align:center;font-size:11px;color:#94a3b8;'>Sent via ElevateED | Mpiyakhe High School</div>
</div></body></html>", headerColor, System.Net.WebUtility.HtmlEncode(title), escapedContent);
        }

        private List<ApplicationUser> GetTargetUsers(Announcement announcement)
        {
            var query = _context.Users.Where(u => u.IsActive && !string.IsNullOrEmpty(u.Email));
            if (announcement.TargetAudience == "Students Only")
                query = query.Where(u => u.Role == UserRole.Student);
            else if (announcement.TargetAudience == "Teachers Only")
                query = query.Where(u => u.Role == UserRole.Teacher);
            return query.ToList();
        }

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
    }
}