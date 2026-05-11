using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ElevateED.Models
{
    public class HomeworkSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HomeworkId { get; set; }

        [ForeignKey("HomeworkId")]
        public virtual Homework Homework { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        // Make Content optional - allow NULL
        public string Content { get; set; }

        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }

        public DateTime SubmittedAt { get; set; }

        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public int? GradedBy { get; set; }

        [ForeignKey("GradedBy")]
        public virtual Teacher Grader { get; set; }

        public DateTime? GradedAt { get; set; }

        public SubmissionStatus Status { get; set; }
        public bool IsLate { get; set; }

        public HomeworkSubmission()
        {
            SubmittedAt = DateTime.Now;
            Status = SubmissionStatus.Submitted;
            IsLate = false;
            Content = "";
        }
    }

    public class ClassworkSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClassworkId { get; set; }

        [ForeignKey("ClassworkId")]
        public virtual Classwork Classwork { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        // Make Content optional - allow NULL
        public string Content { get; set; }

        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }

        public DateTime SubmittedAt { get; set; }

        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public int? GradedBy { get; set; }

        [ForeignKey("GradedBy")]
        public virtual Teacher Grader { get; set; }

        public DateTime? GradedAt { get; set; }

        public SubmissionStatus Status { get; set; }
        public bool IsLate { get; set; }

        public ClassworkSubmission()
        {
            SubmittedAt = DateTime.Now;
            Status = SubmissionStatus.Submitted;
            IsLate = false;
            Content = "";
        }
    }

}