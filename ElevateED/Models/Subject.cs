using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(50)]
        public string Code { get; set; } // e.g., MATH, ENGHL

        // Optional: If subjects have a category (Core, Elective)
        public SubjectCategory Category { get; set; }

        // Navigation Properties
        public virtual ICollection<TeacherSubjectQualification> TeacherQualifications { get; set; }
        public virtual ICollection<TeacherSubjectAssignment> TeacherAssignments { get; set; }
    }

    public enum SubjectCategory
    {
        Core,
        Elective,
        Technology
    }
}