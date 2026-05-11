using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class PastPaper
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string Grade { get; set; } // Grade 8, Grade 9, Grade 10, Grade 11, Grade 12

        [Required]
        public string Year { get; set; } // 2023, 2024, 2025

        [Required]
        public string Term { get; set; } // Term 1, Term 2, Term 3, Term 4

        [Required]
        public string ExamType { get; set; } // "Mid-Year", "Final", "Trial", "Test"

        public string Description { get; set; }

        [Required]
        public string FilePath { get; set; } // Path to the uploaded PDF file

        public string MemoPath { get; set; } // Path to memo PDF (optional)

        public int UploadedBy { get; set; } // TeacherId

        [ForeignKey("UploadedBy")]
        public virtual Teacher Teacher { get; set; }

        public DateTime UploadedAt { get; set; }

        public bool IsPublished { get; set; } = true;

        public int DownloadCount { get; set; } = 0;

        public PastPaper()
        {
            UploadedAt = DateTime.Now;
            IsPublished = true;
            DownloadCount = 0;
        }
    }
}