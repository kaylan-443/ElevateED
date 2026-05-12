using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.ViewModels
{
    public class AssessmentListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Term { get; set; }
        public int AcademicYear { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public string AssessmentType { get; set; }
        public decimal MaxMark { get; set; }
        public MarkApprovalStatus Status { get; set; }
        public int CapturedCount { get; set; }
        public int LearnerCount { get; set; }
    }

    public class CreateAssessmentViewModel
    {
        [Required]
        public int AssignmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Term { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        [Required]
        public AssessmentType AssessmentType { get; set; }

        [Required]
        [Range(1, 1000)]
        public decimal MaxMark { get; set; }

        [Range(0, 100)]
        public decimal Weight { get; set; }

        [Required]
        public DateTime AssessmentDate { get; set; }

        public IEnumerable<SelectListItem> AssignmentOptions { get; set; }
        public IEnumerable<SelectListItem> TermOptions { get; set; }
        public IEnumerable<SelectListItem> AssessmentTypeOptions { get; set; }
    }

    public class MarkCaptureViewModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentName { get; set; }
        public string Term { get; set; }
        public int AcademicYear { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public decimal MaxMark { get; set; }
        public MarkApprovalStatus Status { get; set; }
        public List<StudentMarkEntryViewModel> Marks { get; set; }

        public MarkCaptureViewModel()
        {
            Marks = new List<StudentMarkEntryViewModel>();
        }
    }

    public class StudentMarkEntryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public decimal? Mark { get; set; }
        public decimal? Percentage { get; set; }
        public string Comment { get; set; }
    }

    public class AssessmentReviewViewModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentName { get; set; }
        public string TeacherName { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public string Term { get; set; }
        public int AcademicYear { get; set; }
        public string AssessmentType { get; set; }
        public decimal MaxMark { get; set; }
        public MarkApprovalStatus Status { get; set; }
        public int LearnerCount { get; set; }
        public int CapturedCount { get; set; }
        public int MissingCount { get; set; }
        public decimal ClassAverage { get; set; }
        public decimal HighestMark { get; set; }
        public decimal LowestMark { get; set; }
        public List<StudentMarkEntryViewModel> Marks { get; set; }

        public AssessmentReviewViewModel()
        {
            Marks = new List<StudentMarkEntryViewModel>();
        }
    }

    public class ReportCardViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; }
        public string ClassName { get; set; }
        public string Term { get; set; }
        public int AcademicYear { get; set; }
        public decimal TermMark { get; set; }
        public decimal ExamMark { get; set; }
        public decimal FinalMark { get; set; }
        public string PassFailStatus { get; set; }
        public decimal ClassAverage { get; set; }
        public decimal HighestMark { get; set; }
        public decimal LowestMark { get; set; }
        public string PerformanceTrend { get; set; }
        public PromotionDecision PromotionDecision { get; set; }
        public string ImprovementComment { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<ReportCardSubjectViewModel> Subjects { get; set; }

        public ReportCardViewModel()
        {
            Subjects = new List<ReportCardSubjectViewModel>();
        }
    }

    public class ReportCardSubjectViewModel
    {
        public string SubjectName { get; set; }
        public decimal TermMark { get; set; }
        public decimal ExamMark { get; set; }
        public decimal FinalMark { get; set; }
        public string PassFailStatus { get; set; }
        public decimal ClassAverage { get; set; }
        public decimal HighestMark { get; set; }
        public decimal LowestMark { get; set; }
        public string PerformanceTrend { get; set; }
        public string ImprovementComment { get; set; }
    }

    public class PromotionRuleViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public int? GradeId { get; set; }

        [Range(0, 100)]
        public decimal PromotionMinimumAverage { get; set; }

        [Range(0, 100)]
        public decimal ProgressionMinimumAverage { get; set; }

        [Range(0, 20)]
        public int MaximumFailedSubjectsForPromotion { get; set; }

        [Range(0, 20)]
        public int MaximumFailedSubjectsForProgression { get; set; }

        public List<int> RequiredSubjectIds { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<SelectListItem> GradeOptions { get; set; }
        public IEnumerable<SelectListItem> SubjectOptions { get; set; }

        public PromotionRuleViewModel()
        {
            RequiredSubjectIds = new List<int>();
        }
    }
}
