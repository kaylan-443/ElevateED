using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class StudyMaterial
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [StringLength(100)]
        public string Subject { get; set; }

        [Required]
        [StringLength(20)]
        public string GradeLevel { get; set; } // Grade 8, Grade 9, etc.

        [Required]
        public string FilePath { get; set; }

        [Required]
        [StringLength(200)]
        public string FileName { get; set; }

        [Required]
        [StringLength(50)]
        public string FileType { get; set; }

        public long FileSize { get; set; }

        public int UploadedBy { get; set; } // TeacherId

        [ForeignKey("UploadedBy")]
        public virtual Teacher Teacher { get; set; }

        public DateTime UploadedDate { get; set; }

        public int DownloadCount { get; set; }

        public bool IsActive { get; set; }

        public StudyMaterial()
        {
            UploadedDate = DateTime.Now;
            DownloadCount = 0;
            IsActive = true;
        }
    }
}