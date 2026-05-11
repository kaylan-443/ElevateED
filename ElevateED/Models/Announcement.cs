using System;
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

        [Required]
        public string TargetAudience { get; set; } // "All", "Students", "Teachers", "Parents"

        public string TargetGrade { get; set; } // Optional: "Grade 10", etc.
        public string TargetClass { get; set; } // Optional: "Grade 10A", etc.

        public string AnnouncementType { get; set; } // "General", "Important", "Urgent", "Event"

        public int CreatedBy { get; set; } // AdminId

        [ForeignKey("CreatedBy")]
        public virtual ApplicationUser Admin { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; }

        public Announcement()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
        }
    }
}