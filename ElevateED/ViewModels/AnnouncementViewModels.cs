// AnnouncementViewModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.ViewModels
{
    public class SendAnnouncementViewModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public string TargetAudience { get; set; }

        public string TargetGrade { get; set; }
        public string TargetClass { get; set; }

        public string AnnouncementType { get; set; }

        public DateTime? ExpiryDate { get; set; }
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