using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    // A mark a learner is aiming for in one subject, set from the admission
    // outcome simulator. One row per student/subject — saving again replaces
    // the previous target rather than accumulating a history.
    public class StudentMarkTarget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal TargetMark { get; set; }

        public DateTime UpdatedAt { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public StudentMarkTarget()
        {
            UpdatedAt = DateTime.Now;
        }
    }
}
