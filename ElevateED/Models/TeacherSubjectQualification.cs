using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class TeacherSubjectQualification
    {
        [Key, Column(Order = 0)]
        public int TeacherId { get; set; }

        [Key, Column(Order = 1)]
        public int SubjectId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }
    }
}