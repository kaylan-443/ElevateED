using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class TeacherSubjectAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        // SubjectId = 0 indicates this is a Class Teacher assignment (no subject)
        public DateTime AssignedAt { get; set; }

        public bool IsActive { get; set; }

        // Helper properties
        public bool IsClassTeacher => SubjectId == 0;
        public string Grade => Class?.Grade?.Name;
        public string ClassName => Class?.FullName;
        public string SubjectName => IsClassTeacher ? "Class Teacher" : Subject?.Name;

        public TeacherSubjectAssignment()
        {
            IsActive = true;
            AssignedAt = DateTime.Now;
        }
    }
}