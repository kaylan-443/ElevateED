using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Class
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } // e.g., "A", "B"

        [Required, StringLength(50)]
        public string FullName { get; set; } // e.g., "Grade 10A"

        public int Capacity { get; set; } = 35;

        // Foreign Keys
        public int GradeId { get; set; }

        // Navigation Properties
        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }

        public virtual ICollection<Student> Students { get; set; }
        public virtual ICollection<TeacherSubjectAssignment> TeacherAssignments { get; set; }

        public int? ClassTeacherId { get; set; }

        [ForeignKey("ClassTeacherId")]
        public virtual Teacher ClassTeacher { get; set; }
    }
}