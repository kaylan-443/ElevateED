using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.Models.ViewModels
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
}
