using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ElevateED.ViewModels
{
    public class TeacherDashboardViewModel
    {
        public string StaffNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Qualification { get; set; }
        public int YearsOfExperience { get; set; }
        public int TotalStudents { get; set; }
        public int TotalClasses { get; set; }
        public int PendingTasks { get; set; }
        public int UnreadMessages { get; set; }
        public List<AnnouncementViewModel> RecentAnnouncements { get; set; }
        public List<TimetableViewModel> TodaySchedule { get; set; }

        public bool IsClassTeacher { get; set; }
        public string ClassTeacherGrade { get; set; }
        public string ClassTeacherClassName { get; set; } // e.g., "Grade 8A"
        public int ClassTeacherStudentCount { get; set; }

        public List<TeacherSubjectAssignmentViewModel> SubjectAssignments { get; set; }
    }

    public class AnnouncementViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public string Type { get; set; } // "General", "Important", "Urgent"
        public string PublishedBy { get; set; }
        public DateTime PublishedAt { get; set; }
        public string AttachmentPath { get; set; }
    }

    public class TimetableViewModel
    {
        public int Id { get; set; }
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string ClassStream { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Venue { get; set; }
        public DayOfWeek Day { get; set; }
    }

    public class TeacherProfileViewModel
    {
        public string StaffNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Qualification { get; set; }
        public int YearsOfExperience { get; set; }
        public List<string> Subjects { get; set; }
        public List<string> Classes { get; set; }
        public string ProfilePicture { get; set; }
    }

    public class ClassViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public string Grade { get; set; }
        public int StudentCount { get; set; }
        public string Subject { get; set; }
    }

    public class StudentViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Grade { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class MarksViewModel
    {
        public string Subject { get; set; }
        public string Class { get; set; }
        public List<StudentMarkViewModel> Students { get; set; }
    }

    public class StudentMarkViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public decimal? Assignment1 { get; set; }
        public decimal? Assignment2 { get; set; }
        public decimal? Test1 { get; set; }
        public decimal? Test2 { get; set; }
        public decimal? Exam { get; set; }
        public decimal? Total { get; set; }
        public string Percentage { get; set; }
        public string Grade { get; set; }
    }

    public class AttendanceViewModel
    {
        public string Class { get; set; }
        public DateTime Date { get; set; }
        public List<StudentAttendanceViewModel> Students { get; set; }
    }

    public class StudentAttendanceViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public AttendanceStatus Status { get; set; }
        public string Remarks { get; set; }
    }

    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late,
        Excused
    }
    public class TeacherSubjectAssignmentViewModel
    {
        public string SubjectName { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
    }

}