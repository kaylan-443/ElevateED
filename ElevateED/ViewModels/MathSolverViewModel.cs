using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.ViewModels
{
    public class MathSolverViewModel
    {
        [Required]
        [Display(Name = "Math Problem")]
        public string Problem { get; set; }

        public string Solution { get; set; }
        public string StepsHtml { get; set; }
        public string ProblemType { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class SaveMathSolutionViewModel
    {
        [Required]
        public string Problem { get; set; }

        [Required]
        public string Solution { get; set; }

        public string StepsJson { get; set; }

        public string ProblemType { get; set; }
    }

    public class MathSolverHistoryViewModel
    {
        public int Id { get; set; }
        public string ProblemText { get; set; }
        public string ProblemPreview { get; set; }
        public string SolutionText { get; set; }
        public string SolutionPreview { get; set; }
        public string ProblemType { get; set; }
        public DateTime SolvedAt { get; set; }
        public string SolvedAtFormatted => SolvedAt.ToString("dd MMM yyyy, HH:mm");
        public bool IsFavorite { get; set; }
    }

    public class MathSolverDetailViewModel
    {
        public int Id { get; set; }
        public string ProblemText { get; set; }
        public string SolutionText { get; set; }
        public List<string> Steps { get; set; }
        public string ProblemType { get; set; }
        public DateTime SolvedAt { get; set; }
        public bool IsFavorite { get; set; }
    }
}