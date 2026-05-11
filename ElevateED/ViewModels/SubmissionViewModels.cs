using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ElevateED.ViewModels
{
    public class SubmitHomeworkViewModel
    {
        public int HomeworkId { get; set; }
        public string Title { get; set; }
        public string Instructions { get; set; }
        public string Subject { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsLate { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "Upload Your Work")]
        public HttpPostedFileBase SubmissionFile { get; set; }

        public HomeworkSubmission ExistingSubmission { get; set; }

        public bool IsResubmission => ExistingSubmission != null;
    }

    // Student: Submit Classwork
    public class SubmitClassworkViewModel
    {
        public int ClassworkId { get; set; }
        public string Title { get; set; }
        public string Instructions { get; set; }
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "Upload Your Work")]
        public HttpPostedFileBase SubmissionFile { get; set; }

        public ClassworkSubmission ExistingSubmission { get; set; }

        public bool IsResubmission => ExistingSubmission != null;
    }

    // Student: View Homework Submissions
    public class StudentHomeworkSubmissionViewModel
    {
        public int Id { get; set; }
        public string HomeworkTitle { get; set; }
        public string Subject { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime DueDate { get; set; }
        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public SubmissionStatus Status { get; set; }
        public bool IsLate { get; set; }
        public string FilePath { get; set; }

        public bool HasFile => !string.IsNullOrEmpty(FilePath);
        public string GradeDisplay => Grade.HasValue ? $"{Grade.Value}%" : "Pending";

        public string StatusBadgeClass
        {
            get
            {
                switch (Status)
                {
                    case SubmissionStatus.Submitted: return "badge-warning";
                    case SubmissionStatus.Graded: return "badge-success";
                    case SubmissionStatus.Returned: return "badge-info";
                    case SubmissionStatus.Late: return "badge-danger";
                    default: return "badge-secondary";
                }
            }
        }
    }

    // Student: View Classwork Submissions
    public class StudentClassworkSubmissionViewModel
    {
        public int Id { get; set; }
        public string ClassworkTitle { get; set; }
        public string Subject { get; set; }
        public DateTime SubmittedAt { get; set; }
        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public SubmissionStatus Status { get; set; }
        public string FilePath { get; set; }

        public bool HasFile => !string.IsNullOrEmpty(FilePath);
        public string GradeDisplay => Grade.HasValue ? $"{Grade.Value}%" : "Pending";

        public string StatusBadgeClass
        {
            get
            {
                switch (Status)
                {
                    case SubmissionStatus.Submitted: return "badge-warning";
                    case SubmissionStatus.Graded: return "badge-success";
                    case SubmissionStatus.Returned: return "badge-info";
                    default: return "badge-secondary";
                }
            }
        }
    }
    // Teacher: Submission Analytics
    public class SubmissionAnalyticsViewModel
    {
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; }
        public string AssignmentType { get; set; } // "Homework" or "Classwork"
        public string Subject { get; set; }
        public string ClassName { get; set; }

        public int TotalStudents { get; set; }
        public int SubmissionsReceived { get; set; }
        public int NotSubmitted { get; set; }
        public double SubmissionRate { get; set; }

        public int PassCount { get; set; }      // 50% - 100%
        public int FailCount { get; set; }      // 0% - 49%
        public int UngradedCount { get; set; }
        public double PassRate { get; set; }

        public decimal AverageGrade { get; set; }
        public decimal HighestGrade { get; set; }
        public decimal LowestGrade { get; set; }

        public List<GradeDistributionItem> GradeDistribution { get; set; }
        public List<StudentGradeItem> StudentGrades { get; set; }
    }

    public class GradeDistributionItem
    {
        public string Range { get; set; }  // e.g., "0-49%", "50-59%", etc.
        public int Count { get; set; }
        public string Color { get; set; }
    }

    public class StudentGradeItem
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public decimal? Grade { get; set; }
        public string Status { get; set; }  // "Pass", "Fail", "Not Graded", "Not Submitted"
        public string Feedback { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
    public class GradeHomeworkViewModel
    {
        public int HomeworkId { get; set; }
        public string HomeworkTitle { get; set; }
        public string Subject { get; set; }
        public string ClassName { get; set; }
        public DateTime DueDate { get; set; }
        public List<HomeworkSubmissionViewModel> Submissions { get; set; }
        public int TotalSubmissions => Submissions?.Count ?? 0;
        public int GradedCount => Submissions?.Count(s => s.Grade.HasValue) ?? 0;
    }

    public class HomeworkSubmissionViewModel
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Content { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public SubmissionStatus Status { get; set; }
        public bool IsLate { get; set; }
        public bool HasFile => !string.IsNullOrEmpty(FilePath);
    }

    public class GradeClassworkViewModel
    {
        public int ClassworkId { get; set; }
        public string ClassworkTitle { get; set; }
        public string Subject { get; set; }
        public string ClassName { get; set; }
        public List<ClassworkSubmissionViewModel> Submissions { get; set; }
        public int TotalSubmissions => Submissions?.Count ?? 0;
        public int GradedCount => Submissions?.Count(s => s.Grade.HasValue) ?? 0;
    }

    public class ClassworkSubmissionViewModel
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Content { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public decimal? Grade { get; set; }
        public string TeacherFeedback { get; set; }
        public SubmissionStatus Status { get; set; }
        public bool HasFile => !string.IsNullOrEmpty(FilePath);
    }

}