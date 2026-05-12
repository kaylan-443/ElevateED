using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Announcement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string Summary { get; set; }

        [Required]
        public string TargetAudience { get; set; }

        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }
        public string TargetResidence { get; set; }

        public string AnnouncementType { get; set; }
        public string Tone { get; set; }

        public DateTime? ImportantDate { get; set; }
        public DateTime? DeadlineDate { get; set; }
        public string ImportantDateDescription { get; set; }

        public DateTime? ScheduledSendDate { get; set; }
        public bool IsScheduled { get; set; }
        public bool IsSent { get; set; }

        public bool SendReminder { get; set; }
        public DateTime? ReminderDate { get; set; }
        public bool ReminderSent { get; set; }

        public bool IsAIGenerated { get; set; }
        public string AIPrompt { get; set; }
        public string EmailTemplate { get; set; }

        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual ApplicationUser Admin { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }

        public int ViewCount { get; set; }
        public int EmailSentCount { get; set; }

        [StringLength(500)]
        public string Tags { get; set; }

        public Announcement()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            IsAIGenerated = false;
            IsScheduled = false;
            IsSent = false;
            SendReminder = false;
            ReminderSent = false;
            ViewCount = 0;
            EmailSentCount = 0;
        }
    }

    public class AnnouncementGeneratorSession
    {
        [Key]
        public int Id { get; set; }

        public int AdminId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }

        public string Topic { get; set; }
        public string TargetAudience { get; set; }
        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }
        public string TargetResidence { get; set; }
        public bool IsUrgent { get; set; }
        public string ImportantDateDescription { get; set; }
        public DateTime? ImportantDate { get; set; }
        public DateTime? DeadlineDate { get; set; }
        public string Tone { get; set; }
        public string AdditionalNotes { get; set; }
        public string TemplateName { get; set; }

        public string GeneratedTitle { get; set; }
        public string GeneratedContent { get; set; }
        public string GeneratedSummary { get; set; }
        public string GeneratedSubject { get; set; }

        public DateTime? ScheduledSendDate { get; set; }
        public bool SendReminder { get; set; }
        public DateTime? ReminderDate { get; set; }

        public int? CreatedAnnouncementId { get; set; }

        public AnnouncementGeneratorSession()
        {
            StartedAt = DateTime.Now;
            Status = "InProgress";
            IsUrgent = false;
            SendReminder = false;
        }
    }

    public class AnnouncementTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public string EmailTemplate { get; set; }

        [Required]
        public string NotificationTemplate { get; set; }

        [StringLength(50)]
        public string Category { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }

        public AnnouncementTemplate()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            UsageCount = 0;
        }
    }
}