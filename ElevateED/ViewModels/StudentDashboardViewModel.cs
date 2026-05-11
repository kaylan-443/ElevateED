using System;
using System.Collections.Generic;

namespace ElevateED.ViewModels
{
    public class StudentDashboardViewModel
    {
        // Existing properties
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string ClassTeacher { get; set; }
        public string Status { get; set; }

        // New properties
        public string FirstName => FullName?.Split(' ')[0] ?? "Student";
        public string Email { get; set; }

        // Attendance
        public double AttendancePercentage { get; set; } = 100;
        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int AbsentDays { get; set; }
        public int TotalSchoolDays { get; set; }

        // Homework
        public int PendingHomeworkCount { get; set; }
        public int OverdueHomeworkCount { get; set; }
        public List<UpcomingHomeworkItem> UpcomingHomework { get; set; } = new List<UpcomingHomeworkItem>();

        // Quiz
        public double QuizAverageScore { get; set; }
        public int TotalQuizAttempts { get; set; }
        public List<RecentQuizItem> RecentQuizzes { get; set; } = new List<RecentQuizItem>();

        // Today's Schedule
        public List<TodayClassItem> TodayClasses { get; set; } = new List<TodayClassItem>();
    }

    public class UpcomingHomeworkItem
    {
        public string Title { get; set; }
        public string SubjectName { get; set; }
        public int DaysUntil { get; set; }
    }

    public class RecentQuizItem
    {
        public string SubjectName { get; set; }
        public double Score { get; set; }
        public DateTime AttemptedAt { get; set; }
    }

    public class TodayClassItem
    {
        public string SubjectName { get; set; }
        public string TeacherName { get; set; }
        public string RoomNumber { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class StudentSettingsViewModel
    {
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string CellPhone { get; set; }
        public string PhysicalAddress { get; set; }
        public string ParentName { get; set; }
        public string ParentCellPhone { get; set; }
        public string ParentEmail { get; set; }
    }
}