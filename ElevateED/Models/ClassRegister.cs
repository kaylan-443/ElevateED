using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class ClassRegister
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Grade { get; set; } // Grade 8, Grade 9, etc.

        [Required]
        public string ClassName { get; set; } // Grade 9A, Grade 9B, etc.

        [Required]
        public string Term { get; set; } // Term 1, Term 2, Term 3, Term 4

        [Required]
        public int Year { get; set; } // 2026

        [Required]
        public string FilePath { get; set; } // Path to the uploaded register file

        public string Description { get; set; }

        public int UploadedBy { get; set; } // AdminId or TeacherId

        public DateTime UploadedAt { get; set; }

        public bool IsActive { get; set; }

        public ClassRegister()
        {
            UploadedAt = DateTime.Now;
            IsActive = true;
            Year = DateTime.Now.Year;
        }
    }
}