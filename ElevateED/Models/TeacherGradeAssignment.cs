using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class TeacherGradeAssignment
    {
        [Key, Column(Order = 0)]
        public int TeacherId { get; set; }

        [Key, Column(Order = 1)]
        public int GradeId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }
    }
}