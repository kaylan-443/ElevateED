using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ElevateED.Services
{
    public interface IExamTimetableService
    {
        // Timetable management
        ExamTimetable CreateTimetable(string name, int academicYear, int numberOfWeeks, DateTime startDate, int adminId);
        ExamTimetable GetTimetable(int id);
        List<ExamTimetable> GetAllTimetables();
        bool SendTeacherNotifications(int timetableId);
        TimetableGenerationResult GenerateTimetable(int timetableId);
        bool DistributeTimetable(int timetableId);
        bool DeleteTimetable(int id);

        // Teacher duration submission
        bool SubmitTeacherDurations(int notificationId, bool hasPaper1, decimal? paper1Duration,
                                     bool hasPaper2, decimal? paper2Duration,
                                     bool hasPaper3, decimal? paper3Duration);
        List<TeacherExamNotification> GetPendingNotificationsForTeacher(int teacherId, int timetableId);
        bool AllTeachersResponded(int timetableId);

        // Session management
        ExamSession GetExamSession(int id);
        bool EditExamSession(int id, DateTime examDate, TimeSpan startTime, TimeSpan endTime,
                             decimal durationHours, string venue, string invigilator, int modifiedBy);
        List<ExamSession> GetExamSessions(int timetableId);

        // Student/Teacher views
        List<ExamSession> GetExamSessionsForStudent(int timetableId, int gradeId, int? streamId);
        List<ExamSession> GetExamSessionsForStudent(int timetableId, int gradeId, int? streamId, int? classId);
        List<ExamSession> GetExamSessionsForTeacher(int timetableId, int teacherId);
    }

    public class TimetableGenerationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SessionsGenerated { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public TimetableGenerationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

}
