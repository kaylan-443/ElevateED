using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class PodcastHistory
    {
        [Key]
        public int PodcastHistoryId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(255)]
        public string OriginalFileName { get; set; }

        public string ExtractedText { get; set; }

        public string GeneratedScript { get; set; }

        public string AudioUrl { get; set; }

        public int Duration { get; set; } // in seconds

        public DateTime CreatedAt { get; set; }

        [StringLength(100)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string Grade { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Processing";

        public string ErrorMessage { get; set; }

        public PodcastHistory()
        {
            CreatedAt = DateTime.Now;
            Status = "Processing";
        }
    }
}