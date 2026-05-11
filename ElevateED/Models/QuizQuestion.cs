using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class QuizQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string QuestionText { get; set; }

        [Required]
        [StringLength(200)]
        public string OptionA { get; set; }

        [Required]
        [StringLength(200)]
        public string OptionB { get; set; }

        [Required]
        [StringLength(200)]
        public string OptionC { get; set; }

        [Required]
        [StringLength(200)]
        public string OptionD { get; set; }

        [Required]
        [StringLength(1)]
        public string CorrectAnswer { get; set; }

        public int SubjectId { get; set; }
        public int GradeId { get; set; }
        public int CreatedBy { get; set; }

        public virtual Subject Subject { get; set; }
        public virtual Grade Grade { get; set; }
        public virtual Teacher Teacher { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public QuizQuestion()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
        }
    }

    public class QuizAttempt
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public decimal Score { get; set; }
        public DateTime AttemptedAt { get; set; }

        public virtual Student Student { get; set; }
        public virtual Subject Subject { get; set; }
        public virtual ICollection<QuizAnswer> Answers { get; set; }

        public QuizAttempt()
        {
            AttemptedAt = DateTime.Now;
            Answers = new HashSet<QuizAnswer>();
        }
    }

    public class QuizAnswer
    {
        [Key]
        public int Id { get; set; }

        public int QuizAttemptId { get; set; }
        public int QuestionId { get; set; }

        [Required]
        [StringLength(1)]
        public string SelectedAnswer { get; set; }

        public bool IsCorrect { get; set; }

        public virtual QuizAttempt QuizAttempt { get; set; }
        public virtual QuizQuestion Question { get; set; }
    }
}