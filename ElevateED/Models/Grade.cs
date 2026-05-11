using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public class Grade
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Name { get; set; } // e.g., "Grade 10"

        public int Level { get; set; } // 8, 9, 10, 11, 12 for easier querying

        // Navigation Properties
        public virtual ICollection<Class> Classes { get; set; }
        public virtual ICollection<TeacherGradeAssignment> TeacherAssignments { get; set; }
        public virtual ICollection<Subject> CoreSubjects { get; set; } // Many-to-Many relationship
    }
}