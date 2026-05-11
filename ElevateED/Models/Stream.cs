using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public class Stream
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } // e.g., "Mathematics, Life Science & Physics"

        public string Description { get; set; }

        // Navigation Properties
        public virtual ICollection<Subject> ElectiveSubjects { get; set; } // Many-to-Many
        public virtual ICollection<Subject> TechnologySubjects { get; set; } // Many-to-Many
    }
}