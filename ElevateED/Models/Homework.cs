using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Homework
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Instructions { get; set; }

        // Relational properties (NEW)
        public int? SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public int? ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        // String fields for backward compatibility (renamed to avoid conflict)
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string ClassNameValue { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public long FileSize { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public int UploadedBy { get; set; }

        [ForeignKey("UploadedBy")]
        public virtual Teacher Teacher { get; set; }

        public DateTime UploadedAt { get; set; }

        public bool IsActive { get; set; }

        // Computed property for display (different name to avoid conflict)
        [NotMapped]
        public string SubjectDisplay => Subject?.Name ?? SubjectName;

        [NotMapped]
        public string GradeDisplay => Class?.Grade?.Name ?? GradeName;

        [NotMapped]
        public string ClassNameDisplay => Class?.FullName ?? ClassNameValue;

        public Homework()
        {
            UploadedAt = DateTime.Now;
            IsActive = true;
        }
    }

    public class Classwork
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Instructions { get; set; }

        // Relational properties (NEW)
        public int? SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public int? ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        // String fields for backward compatibility (renamed to avoid conflict)
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string ClassNameValue { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public long FileSize { get; set; }

        public int UploadedBy { get; set; }

        [ForeignKey("UploadedBy")]
        public virtual Teacher Teacher { get; set; }

        public DateTime UploadedAt { get; set; }

        public bool IsActive { get; set; }

        // Computed property for display (different name to avoid conflict)
        [NotMapped]
        public string SubjectDisplay => Subject?.Name ?? SubjectName;

        [NotMapped]
        public string GradeDisplay => Class?.Grade?.Name ?? GradeName;

        [NotMapped]
        public string ClassNameDisplay => Class?.FullName ?? ClassNameValue;

        public Classwork()
        {
            UploadedAt = DateTime.Now;
            IsActive = true;
        }
    }
}