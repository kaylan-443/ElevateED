using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public class ApplicationCycle
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } // e.g., "2026 Intake"

        [Required]
        public int AcademicYear { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime DeadlineDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Grade Limits (Optional)
        public int? Grade8Limit { get; set; }
        public int? Grade9Limit { get; set; }
        public int? Grade10Limit { get; set; }
        public int? Grade11Limit { get; set; }
        public int? Grade12Limit { get; set; }

        // Navigation
        public virtual ICollection<Applicant> Applicants { get; set; }
    }
}