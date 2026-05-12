using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.ViewModels
{
    /// <summary>
    /// ViewModel for displaying attendance analytics and statistics.
    /// </summary>
    public class AnalyticsViewModel
    {
        [Display(Name = "Time Period")]
        public string Filter { get; set; } = "weekly";

        [Display(Name = "Class")]
        public int? ClassId { get; set; }

        public IEnumerable<SelectListItem> AvailableClasses { get; set; }

        [Display(Name = "Overall Attendance Rate")]
        public double OverallAttendanceRate { get; set; }

        [Display(Name = "Total Sessions")]
        public int TotalSessions { get; set; }

        [Display(Name = "At-Risk Students")]
        public int AtRiskCount { get; set; }

        public List<ClassAttendanceStat> ClassStats { get; set; } = new List<ClassAttendanceStat>();

        public int TotalAssessments { get; set; }
        public int SubmittedAssessments { get; set; }
        public int ApprovedAssessments { get; set; }
        public int GeneratedReportCards { get; set; }
        public decimal AcademicAverage { get; set; }
        public decimal AcademicPassRate { get; set; }
        public List<AnalyticsAcademicStat> SubjectPerformance { get; set; } = new List<AnalyticsAcademicStat>();
        public List<AnalyticsAcademicStat> ClassPerformance { get; set; } = new List<AnalyticsAcademicStat>();
    }

    public class AnalyticsAcademicStat
    {
        public string Name { get; set; }
        public decimal Average { get; set; }
        public decimal PassRate { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Class-level attendance statistics.
    /// </summary>
    public class ClassAttendanceStat
    {
        public string ClassName { get; set; }
        public int TotalSessions { get; set; }
        public double AttendanceRate { get; set; }
        public List<LearnerStat> AtRiskLearners { get; set; } = new List<LearnerStat>();
        public List<LearnerStat> AllLearnerStats { get; set; } = new List<LearnerStat>();
    }

    /// <summary>
    /// Individual learner attendance statistics.
    /// </summary>
    public class LearnerStat
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendancePercent { get; set; }
        public bool IsAtRisk => AttendancePercent < 75;
    }

    // ============ ADMIN ANALYTICS VIEWMODELS ============

    /// <summary>
    /// Comprehensive admin dashboard analytics.
    /// </summary>
    public class AdminAnalyticsViewModel
    {
        // Key Metrics
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalClasses { get; set; }
        public double ApprovalRate { get; set; }
        public int StudentGrowth { get; set; }
        public int TeacherGrowth { get; set; }

        // Application Stats
        public ApplicationStats ApplicationStats { get; set; } = new ApplicationStats();
        public List<ApplicationBreakdownItem> ApplicationBreakdown { get; set; } = new List<ApplicationBreakdownItem>();
        public List<RecentApplicationItem> RecentApplications { get; set; } = new List<RecentApplicationItem>();

        // Grade Distribution
        public Dictionary<string, int> GradeDistribution { get; set; } = new Dictionary<string, int>();

        // Pending Tasks
        public int PendingIssuesCount { get; set; }
        public int UnassignedClassesCount { get; set; }
        public int PendingTeacherAssignments { get; set; }
    }

    public class ApplicationStats
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
    }

    public class ApplicationBreakdownItem
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
    }

    public class RecentApplicationItem
    {
        public string FullName { get; set; }
        public string GradeApplyingFor { get; set; }
        public string Status { get; set; }
        public int DaysAgo { get; set; }
    }

    public class PrincipalAnalyticsViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalClasses { get; set; }
        public int TotalAssessments { get; set; }
        public int PendingMarkApprovals { get; set; }
        public int GeneratedReportCards { get; set; }
        public int PublishedExamSessions { get; set; }
        public decimal SchoolAverage { get; set; }
        public decimal PassRate { get; set; }
        public decimal PromotionRate { get; set; }
        public decimal ProgressionRate { get; set; }
        public decimal NotPromotedRate { get; set; }
        public List<PrincipalSubjectAnalyticsItem> SubjectPerformance { get; set; } = new List<PrincipalSubjectAnalyticsItem>();
        public List<PrincipalClassAnalyticsItem> ClassPerformance { get; set; } = new List<PrincipalClassAnalyticsItem>();
        public List<PrincipalTeacherAnalyticsItem> TeacherPerformance { get; set; } = new List<PrincipalTeacherAnalyticsItem>();
        public List<PrincipalAtRiskLearnerItem> AtRiskLearners { get; set; } = new List<PrincipalAtRiskLearnerItem>();
        public List<PrincipalExamAnalyticsItem> ExamTimetables { get; set; } = new List<PrincipalExamAnalyticsItem>();
    }

    public class PrincipalSubjectAnalyticsItem
    {
        public string SubjectName { get; set; }
        public decimal Average { get; set; }
        public decimal PassRate { get; set; }
        public int LearnerCount { get; set; }
    }

    public class PrincipalClassAnalyticsItem
    {
        public string ClassName { get; set; }
        public decimal Average { get; set; }
        public decimal PassRate { get; set; }
        public int PromotedCount { get; set; }
        public int ProgressedCount { get; set; }
        public int NotPromotedCount { get; set; }
    }

    public class PrincipalTeacherAnalyticsItem
    {
        public string TeacherName { get; set; }
        public int AssessmentCount { get; set; }
        public int ApprovedAssessmentCount { get; set; }
        public decimal AverageLearnerMark { get; set; }
        public decimal PassRate { get; set; }
    }

    public class PrincipalAtRiskLearnerItem
    {
        public string StudentName { get; set; }
        public string ClassName { get; set; }
        public decimal FinalMark { get; set; }
        public string PromotionDecision { get; set; }
        public string Reason { get; set; }
    }

    public class PrincipalExamAnalyticsItem
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public int TotalSessions { get; set; }
        public int ProposedSessions { get; set; }
        public int PublishedSessions { get; set; }
    }
}
