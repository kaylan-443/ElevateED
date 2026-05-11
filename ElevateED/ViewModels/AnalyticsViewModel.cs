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
}