using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ElevateED.Models
{
    public class MathSolverHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ProblemText { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string SolutionText { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string StepsJson { get; set; }

        public string ProblemType { get; set; }

        public DateTime SolvedAt { get; set; }

        public bool IsFavorite { get; set; }

        // Navigation property
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public MathSolverHistory()
        {
            SolvedAt = DateTime.Now;
            IsFavorite = false;
        }
    }

}