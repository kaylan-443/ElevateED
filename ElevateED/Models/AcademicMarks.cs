using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public enum AssessmentType
    {
        ClassTest = 0,
        Assignment = 1,
        Project = 2,
        Practical = 3,
        Exam = 4
    }

    public enum MarkApprovalStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3
    }

    public enum PromotionDecision
    {
        Pending = 0,
        Promoted = 1,
        Progressed = 2,
        NotPromoted = 3
    }

    public class Assessment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        public string Term { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        [Required]
        public AssessmentType AssessmentType { get; set; }

        [Required]
        public decimal MaxMark { get; set; }

        public decimal Weight { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        public DateTime AssessmentDate { get; set; }
        public MarkApprovalStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string AdminComment { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public virtual ICollection<AssessmentMark> Marks { get; set; }

        public Assessment()
        {
            Status = MarkApprovalStatus.Draft;
            CreatedAt = DateTime.Now;
            AssessmentDate = DateTime.Today;
            Weight = 1;
            Marks = new HashSet<AssessmentMark>();
        }
    }

    public class AssessmentMark
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AssessmentId { get; set; }

        [Required]
        public int StudentId { get; set; }

        public decimal? Mark { get; set; }
        public string Comment { get; set; }
        public DateTime CapturedAt { get; set; }

        [ForeignKey("AssessmentId")]
        public virtual Assessment Assessment { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public AssessmentMark()
        {
            CapturedAt = DateTime.Now;
        }
    }

    public class StudentReportCard
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        [StringLength(20)]
        public string Term { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        public decimal TermMark { get; set; }
        public decimal ExamMark { get; set; }
        public decimal FinalMark { get; set; }

        [StringLength(20)]
        public string PassFailStatus { get; set; }

        public decimal ClassAverage { get; set; }
        public decimal HighestMark { get; set; }
        public decimal LowestMark { get; set; }

        [StringLength(40)]
        public string PerformanceTrend { get; set; }

        public PromotionDecision PromotionDecision { get; set; }
        public string ImprovementComment { get; set; }
        public DateTime GeneratedAt { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }
        public virtual ICollection<StudentReportCardSubject> Subjects { get; set; }

        public StudentReportCard()
        {
            GeneratedAt = DateTime.Now;
            PromotionDecision = PromotionDecision.Pending;
            Subjects = new HashSet<StudentReportCardSubject>();
        }
    }

    public class PromotionRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public int? GradeId { get; set; }
        public decimal PromotionMinimumAverage { get; set; }
        public decimal ProgressionMinimumAverage { get; set; }
        public int MaximumFailedSubjectsForPromotion { get; set; }
        public int MaximumFailedSubjectsForProgression { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }
        public virtual ICollection<PromotionRuleRequiredSubject> RequiredSubjects { get; set; }

        public PromotionRule()
        {
            Name = "Default Promotion Rule";
            PromotionMinimumAverage = 50;
            ProgressionMinimumAverage = 40;
            MaximumFailedSubjectsForPromotion = 1;
            MaximumFailedSubjectsForProgression = 3;
            IsActive = true;
            CreatedAt = DateTime.Now;
            RequiredSubjects = new HashSet<PromotionRuleRequiredSubject>();
        }
    }

    public class PromotionRuleRequiredSubject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PromotionRuleId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [ForeignKey("PromotionRuleId")]
        public virtual PromotionRule PromotionRule { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }
    }

    public class StudentReportCardSubject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentReportCardId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        public decimal TermMark { get; set; }
        public decimal ExamMark { get; set; }
        public decimal FinalMark { get; set; }

        [StringLength(20)]
        public string PassFailStatus { get; set; }

        public decimal ClassAverage { get; set; }
        public decimal HighestMark { get; set; }
        public decimal LowestMark { get; set; }

        [StringLength(40)]
        public string PerformanceTrend { get; set; }

        public string ImprovementComment { get; set; }

        [ForeignKey("StudentReportCardId")]
        public virtual StudentReportCard StudentReportCard { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }
    }
}
