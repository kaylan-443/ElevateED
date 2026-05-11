using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class TimeTable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string Type { get; set; } // "Teachers" or "Learners"

        // Only used for Learners
        public string Grade { get; set; }
        public string ClassName { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public int UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; }

        public bool IsActive { get; set; }

        public TimeTable()
        {
            UploadedAt = DateTime.Now;
            IsActive = true;
        }
    }
}