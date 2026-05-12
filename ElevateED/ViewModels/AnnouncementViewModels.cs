using ElevateED.Models;
using ElevateED.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.ViewModels
{
    public class AnnouncementWizardViewModel
    {
        [Required]
        public string Topic { get; set; }

        [Required]
        public string TargetAudience { get; set; }

        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }
        public string TargetResidence { get; set; }

        public bool IsUrgent { get; set; }

        public string ImportantDateDescription { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ImportantDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DeadlineDate { get; set; }

        public string Tone { get; set; } = "Formal";

        public string AdditionalNotes { get; set; }

        public string TemplateName { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? ScheduledSendDate { get; set; }

        public bool SendReminder { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? ReminderDate { get; set; }

        public List<AnnouncementTemplate> Templates { get; set; }
        public List<SelectListItem> TargetAudiences { get; set; }
        public List<SelectListItem> ToneOptions { get; set; }
    }

    public class QuickAnnouncementViewModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public string TargetAudience { get; set; }

        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }
        public string AnnouncementType { get; set; } = "General";
        public string Tone { get; set; } = "Formal";
        public DateTime? ImportantDate { get; set; }
        public DateTime? DeadlineDate { get; set; }
        public DateTime? ScheduledSendDate { get; set; }
    }

    public class AnnouncementDashboardViewModel
    {
        public List<Announcement> ScheduledAnnouncements { get; set; }
        public List<Announcement> RecentAnnouncements { get; set; }
        public AnnouncementAnalytics Analytics { get; set; }
        public List<AnnouncementTemplate> Templates { get; set; }
    }

    public class AnnouncementAnalyticsViewModel
    {
        public AnnouncementAnalytics Analytics { get; set; }
        public List<Announcement> TopAnnouncements { get; set; }
    }

    public class AnnouncementListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string TargetAudience { get; set; }
        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }
        public string AnnouncementType { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public string TypeBadgeClass { get; set; }
        public string AudienceBadgeClass { get; set; }
    }
}