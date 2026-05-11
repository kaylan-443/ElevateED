using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace ElevateED.ViewModels
{
    public class QuizViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public List<QuestionViewModel> Questions { get; set; }
    }

    public class QuestionViewModel
    {
        public int Id { get; set; }
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
    }

    public class QuizSubmissionViewModel
    {
        public int SubjectId { get; set; }
        public List<AnswerSubmission> Answers { get; set; }
    }

    public class AnswerSubmission
    {
        public int QuestionId { get; set; }
        public string SelectedAnswer { get; set; }
    }

    public class QuizResultViewModel
    {
        public int AttemptId { get; set; }
        public string SubjectName { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public decimal Score { get; set; }
        public DateTime AttemptedAt { get; set; }
        public List<AnswerResultViewModel> Answers { get; set; }
    }

    public class AnswerResultViewModel
    {
        public string QuestionText { get; set; }
        public string SelectedAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
    }
}