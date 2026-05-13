// Add this to your ViewModels file (e.g., ExtraClassViewModels.cs)

using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.ViewModels
{
    // ============================================
    // ADMIN EXTRA CLASS VIEW MODELS
    // ============================================

    public class AdminExtraClassDashboardViewModel
    {
        public List<ExtraClass> ExtraClasses { get; set; }
        public List<StrugglingSubjectViewModel> StrugglingSubjects { get; set; }
        public AIRecommendationViewModel AIRecommendation { get; set; }
        public int TotalClasses { get; set; }
        public int TotalEnrollments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRating { get; set; }
    }

    public class StrugglingSubjectViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int GradeId { get; set; }
        public string GradeName { get; set; }
        public decimal AverageMark { get; set; }
        public decimal PassRate { get; set; }
        public int StudentCount { get; set; }
        public string Recommendation { get; set; }
    }

    public class AIRecommendationViewModel
    {
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public decimal AverageMark { get; set; }
        public string RecommendedAction { get; set; }
        public List<string> EstimatedTopics { get; set; }
        public decimal PredictedImprovement { get; set; }
    }

    public class CreateExtraClassViewModel
    {
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        public int GradeId { get; set; }

        public int? TeacherId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        public string Schedule { get; set; }

        public string Venue { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, 100)]
        public int Capacity { get; set; }

        public int DurationWeeks { get; set; }

        public bool IsAIGenerated { get; set; }

        public List<SelectListItem> SubjectsList { get; set; }
        public List<SelectListItem> GradesList { get; set; }
        public List<SelectListItem> TeachersList { get; set; }
    }

    public class ExtraClassDetailsViewModel
    {
        public ExtraClass ExtraClass { get; set; }
        public List<ExtraClassEnrollment> Enrollments { get; set; }
        public List<ExtraClassAttendanceSession> AttendanceSessions { get; set; }
        public List<ExtraClassFeedback> Feedbacks { get; set; }
        public ExtraClassAIRecommendation AIRecommendation { get; set; }
        public decimal AttendanceRate { get; set; }
        public decimal AverageRating { get; set; }
        public List<SelectListItem> AvailableTeachers { get; set; }

        // ADD THESE MISSING PROPERTIES
        public int TotalEnrolled { get; set; }
        public int TotalPaid { get; set; }
    }

    public class ExtraClassAnalyticsViewModel
    {
        public int TotalClasses { get; set; }
        public int TotalEnrollments { get; set; }
        public int TotalPaidEnrollments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageAttendanceRate { get; set; }
        public decimal AverageRating { get; set; }
        public List<ClassPerformanceViewModel> ClassPerformance { get; set; }
    }

    public class ClassPerformanceViewModel
    {
        public string ClassName { get; set; }
        public int EnrollmentCount { get; set; }
        public decimal AttendanceRate { get; set; }
        public decimal AverageRating { get; set; }
        public decimal Revenue { get; set; }
    }

    // ============================================
    // TEACHER EXTRA CLASS VIEW MODELS
    // ============================================

    public class TeacherExtraClassDashboardViewModel
    {
        public List<ExtraClass> MyClasses { get; set; }
        public List<ExtraClassAttendanceSession> ActiveSessions { get; set; }
        public int TotalStudents { get; set; }
        public int TotalSessions { get; set; }
        public decimal AverageAttendance { get; set; }
    }

    public class StartAttendanceSessionViewModel
    {
        [Required]
        public int ExtraClassId { get; set; }

        public int SessionNumber { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime SessionDate { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        public string TopicsToCover { get; set; }

        public List<SelectListItem> ExtraClasses { get; set; }

        // ADD THIS MISSING PROPERTY
        public string ClassName { get; set; }
    }

    public class ActiveAttendanceSessionViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public string QRCode { get; set; }
        public DateTime QRCodeExpiry { get; set; }
        public DateTime SessionDate { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public bool IsExpired { get; set; }
        public string TimeRemaining
        {
            get
            {
                if (IsExpired) return "Expired";
                var remaining = QRCodeExpiry - DateTime.Now;
                if (remaining.TotalMinutes < 1)
                    return $"{remaining.Seconds}s remaining";
                return $"{remaining.Minutes}m {remaining.Seconds}s remaining";
            }
        }
        public double AttendancePercentage => TotalStudents > 0 ? (double)PresentCount / TotalStudents * 100 : 0;
    }

    public class EndSessionViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public DateTime SessionDate { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendancePercentage { get; set; }

        [StringLength(500)]
        public string TopicsCovered { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class ExtraClassAttendanceRecordViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public DateTime SessionDate { get; set; }
        public List<AttendanceRecordItem> Records { get; set; }
    }

    public class AttendanceRecordItem
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? ScanTime { get; set; }
    }

    public class TeacherAIRecommendationViewModel
    {
        public int ExtraClassId { get; set; }
        public string ClassName { get; set; }
        public ExtraClassAIRecommendation Recommendation { get; set; }
        public List<string> EasyTopics { get; set; }
        public List<string> DifficultTopics { get; set; }
        public List<string> SuggestedOrder { get; set; }
        public List<string> CommonMistakes { get; set; }
        public bool HasAppliedRecommendation { get; set; }
    }

    // ============================================
    // STUDENT EXTRA CLASS VIEW MODELS
    // ============================================

    public class StudentExtraClassViewModel
    {
        public List<ExtraClass> AvailableClasses { get; set; }
        public List<ExtraClassEnrollment> MyEnrollments { get; set; }
        public List<ExtraClassAttendanceRecord> MyAttendance { get; set; }
        public List<ExtraClassFeedback> MyFeedbacks { get; set; }
        public decimal AverageRating { get; set; }
    }

    public class StudentExtraClassDetailViewModel
    {
        public ExtraClass ExtraClass { get; set; }
        public bool IsEnrolled { get; set; }
        public bool IsPaid { get; set; }
        public ExtraClassEnrollment Enrollment { get; set; }
        public List<ExtraClassAttendanceRecord> AttendanceRecords { get; set; }
        public int PresentCount { get; set; }
        public int TotalSessions { get; set; }
        public double AttendancePercentage { get; set; }
        public ExtraClassFeedback MyFeedback { get; set; }
        public bool CanProvideFeedback { get; set; }
    }

    public class StudentAttendanceViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public DateTime SessionDate { get; set; }
        public string QRCode { get; set; }
        public DateTime QRCodeExpiry { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? ScanTime { get; set; }
        public bool IsExpired => QRCodeExpiry < DateTime.Now;
    }

    public class ScanQRCodeViewModel
    {
        [Required]
        [StringLength(50)]
        public string QRCode { get; set; }

        public int SessionId { get; set; }
        public string ClassName { get; set; }
    }

    public class SubmitFeedbackViewModel
    {
        [Required]
        public int ExtraClassId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(500)]
        public string Comment { get; set; }
    }

    // ============================================
    // TEACHER CLASS DETAILS VIEW MODEL
    // ============================================

    public class TeacherClassDetailsViewModel
    {
        public ExtraClass ExtraClass { get; set; }
        public List<Student> EnrolledStudents { get; set; }
        public List<ExtraClassAttendanceSession> AttendanceSessions { get; set; }
        public List<ExtraClassFeedback> Feedbacks { get; set; }
        public ExtraClassAIRecommendation AIRecommendation { get; set; }
        public int TotalEnrolled { get; set; }
        public int TotalPaid { get; set; }
        public decimal AverageRating { get; set; }
    }

    // ============================================
    // SESSION HISTORY VIEW MODEL
    // ============================================

    public class SessionHistoryViewModel
    {
        public int SessionId { get; set; }
        public int SessionNumber { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string TopicsCovered { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public double AttendancePercentage { get; set; }
    }

    // ============================================
    // STRUGGLING TOPICS RESULT (Internal use)
    // ============================================

    public class StrugglingTopicsResult
    {
        public List<string> EasyTopics { get; set; }
        public List<string> DifficultTopics { get; set; }
    }
}