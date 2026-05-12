using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class AIStudySession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [StringLength(255)]
        public string SessionTitle { get; set; }

        [StringLength(255)]
        public string OriginalFileName { get; set; }

        public string ExtractedText { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime LastAccessedDate { get; set; }

        public bool IsActive { get; set; }

        // Navigation property
        public virtual ICollection<AIStudyOutput> Outputs { get; set; }

        [NotMapped]
        public int OutputCount => Outputs?.Count ?? 0;

        [NotMapped]
        public string TextPreview => ExtractedText?.Length > 150
            ? ExtractedText.Substring(0, 150) + "..."
            : ExtractedText;

        public AIStudySession()
        {
            CreatedDate = DateTime.Now;
            LastAccessedDate = DateTime.Now;
            IsActive = true;
            Outputs = new HashSet<AIStudyOutput>();
        }
    }

    public class AIStudyOutput
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [ForeignKey("SessionId")]
        public virtual AIStudySession Session { get; set; }

        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } // Flashcards, ExamNotes, SimplifiedNotes, Quiz

        public string GeneratedContent { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsFavorite { get; set; }

        public AIStudyOutput()
        {
            CreatedDate = DateTime.Now;
            IsFavorite = false;
        }
    }
}