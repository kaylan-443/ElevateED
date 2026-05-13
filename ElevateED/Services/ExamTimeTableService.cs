using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;

namespace ElevateED.Services
{
    public class ExamTimetableService : IExamTimetableService
    {
        private readonly ElevateEDContext _context;
        private readonly TimeSpan DAY_START = new TimeSpan(9, 0, 0);
        private readonly TimeSpan DAY_END = new TimeSpan(14, 15, 0);
        private const decimal MIN_DURATION = 1;
        private const decimal MAX_DURATION = 3;

        public ExamTimetableService()
        {
            _context = new ElevateEDContext();
        }

        // ============================================
        // TIMETABLE MANAGEMENT
        // ============================================

        public ExamTimetable CreateTimetable(string name, int academicYear, int numberOfWeeks, DateTime startDate, int adminId)
        {
            try
            {
                var endDate = startDate.AddDays((numberOfWeeks * 7) - 1);

                var timetable = new ExamTimetable
                {
                    Name = name,
                    AcademicYear = academicYear,
                    NumberOfWeeks = numberOfWeeks,
                    StartDate = startDate,
                    EndDate = endDate,
                    CreatedBy = adminId,
                    Status = ExamTimetableStatus.Draft,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.ExamTimetables.Add(timetable);
                _context.SaveChanges();

                return timetable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateTimetable: {ex.Message}");
                return null;
            }
        }

        public ExamTimetable GetTimetable(int id)
        {
            try
            {
                return _context.ExamTimetables.Find(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetTimetable: {ex.Message}");
                return null;
            }
        }

        public List<ExamTimetable> GetAllTimetables()
        {
            try
            {
                return _context.ExamTimetables
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllTimetables: {ex.Message}");
                return new List<ExamTimetable>();
            }
        }

        // ============================================
        // TEACHER NOTIFICATIONS
        // ============================================

        public bool SendTeacherNotifications(int timetableId)
        {
            try
            {
                var timetable = _context.ExamTimetables.Find(timetableId);
                if (timetable == null || timetable.Status != ExamTimetableStatus.Draft)
                    return false;

                // Get unique teacher-subject-grade combinations
                var teacherSubjects = _context.TeacherSubjectAssignments
                    .Include("Teacher")
                    .Include("Subject")
                    .Include("Class")
                    .Include("Class.Grade")
                    .Where(t => t.IsActive)
                    .Select(t => new {
                        t.TeacherId,
                        t.SubjectId,
                        GradeId = t.Class.GradeId
                    })
                    .Distinct()
                    .ToList();

                if (!teacherSubjects.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No teacher-subject assignments found.");
                    return false;
                }

                foreach (var item in teacherSubjects)
                {
                    var existing = _context.TeacherExamNotifications
                        .FirstOrDefault(n => n.ExamTimetableId == timetableId
                            && n.TeacherId == item.TeacherId
                            && n.SubjectId == item.SubjectId
                            && n.GradeId == item.GradeId);

                    if (existing == null)
                    {
                        var notification = new TeacherExamNotification
                        {
                            ExamTimetableId = timetableId,
                            TeacherId = item.TeacherId,
                            SubjectId = item.SubjectId,
                            GradeId = item.GradeId,
                            HasPaper1 = true,
                            HasPaper2 = false,
                            HasPaper3 = false,
                            IsSubmitted = false,
                            NotifiedAt = DateTime.Now
                        };
                        _context.TeacherExamNotifications.Add(notification);
                    }
                }

                timetable.Status = ExamTimetableStatus.AwaitingTeacherInput;
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SendTeacherNotifications: {ex.Message}");
                return false;
            }
        }

        public bool SubmitTeacherDurations(int notificationId, bool hasPaper1, decimal? paper1Duration,
                                            bool hasPaper2, decimal? paper2Duration,
                                            bool hasPaper3, decimal? paper3Duration)
        {
            try
            {
                var notification = _context.TeacherExamNotifications.Find(notificationId);
                if (notification == null || notification.IsSubmitted)
                    return false;

                // Validate durations
                if (hasPaper1 && (!paper1Duration.HasValue || paper1Duration < MIN_DURATION || paper1Duration > MAX_DURATION))
                    return false;
                if (hasPaper2 && (!paper2Duration.HasValue || paper2Duration < MIN_DURATION || paper2Duration > MAX_DURATION))
                    return false;
                if (hasPaper3 && (!paper3Duration.HasValue || paper3Duration < MIN_DURATION || paper3Duration > MAX_DURATION))
                    return false;

                notification.HasPaper1 = hasPaper1;
                notification.Paper1Duration = paper1Duration;
                notification.HasPaper2 = hasPaper2;
                notification.Paper2Duration = paper2Duration;
                notification.HasPaper3 = hasPaper3;
                notification.Paper3Duration = paper3Duration;
                notification.IsSubmitted = true;
                notification.SubmittedAt = DateTime.Now;

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SubmitTeacherDurations: {ex.Message}");
                return false;
            }
        }

        public List<TeacherExamNotification> GetPendingNotificationsForTeacher(int teacherId, int timetableId)
        {
            try
            {
                return _context.TeacherExamNotifications
                    .Include("Subject")
                    .Include("Grade")
                    .Where(n => n.ExamTimetableId == timetableId
                        && n.TeacherId == teacherId
                        && !n.IsSubmitted)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetPendingNotificationsForTeacher: {ex.Message}");
                return new List<TeacherExamNotification>();
            }
        }

        public bool AllTeachersResponded(int timetableId)
        {
            try
            {
                var pendingCount = _context.TeacherExamNotifications
                    .Count(n => n.ExamTimetableId == timetableId && !n.IsSubmitted);
                return pendingCount == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AllTeachersResponded: {ex.Message}");
                return false;
            }
        }

        // ============================================
        // GENERATE TIMETABLE
        // ============================================

        public TimetableGenerationResult GenerateTimetable(int timetableId)
        {
            var result = new TimetableGenerationResult();

            try
            {
                var timetable = _context.ExamTimetables.Find(timetableId);
                if (timetable == null)
                {
                    result.Success = false;
                    result.Message = "Timetable not found";
                    return result;
                }

                if (timetable.Status != ExamTimetableStatus.AwaitingTeacherInput)
                {
                    result.Success = false;
                    result.Message = "Cannot generate: Timetable is not in 'Awaiting Teacher Input' status";
                    return result;
                }

                if (!AllTeachersResponded(timetableId))
                {
                    result.Success = false;
                    result.Message = "Please wait for all teachers to submit exam durations";
                    return result;
                }

                // Remove existing sessions
                var existingSessions = _context.ExamSessions.Where(s => s.ExamTimetableId == timetableId);
                _context.ExamSessions.RemoveRange(existingSessions);
                _context.SaveChanges();

                var notifications = _context.TeacherExamNotifications
                    .Include(n => n.Subject)
                    .Include(n => n.Grade)
                    .Where(n => n.ExamTimetableId == timetableId && n.IsSubmitted)
                    .ToList();

                if (!notifications.Any())
                {
                    result.Success = false;
                    result.Message = "No teacher submissions found";
                    return result;
                }

                // Group by Subject + Grade
                var subjectGradeGroups = notifications
                    .GroupBy(n => new {
                        n.SubjectId,
                        SubjectName = n.Subject.Name,
                        n.GradeId,
                        GradeName = n.Grade.Name
                    })
                    .Select(g => new
                    {
                        g.Key.SubjectId,
                        g.Key.SubjectName,
                        g.Key.GradeId,
                        g.Key.GradeName,
                        GradeLevel = g.First().Grade.Level,
                        HasPaper1 = g.First().HasPaper1,
                        Paper1Duration = g.First().Paper1Duration,
                        HasPaper2 = g.First().HasPaper2,
                        Paper2Duration = g.First().Paper2Duration,
                        HasPaper3 = g.First().HasPaper3,
                        Paper3Duration = g.First().Paper3Duration
                    })
                    .ToList();

                var grades = _context.Grades.OrderBy(g => g.Level).ToList();

                // Track used dates per Grade
                var gradeSchedule = new Dictionary<int, HashSet<DateTime>>();
                foreach (var grade in grades)
                {
                    gradeSchedule[grade.Id] = new HashSet<DateTime>();
                }

                // Generate exam dates (Monday to Friday only)
                var examDates = new List<DateTime>();
                var currentDate = timetable.StartDate;
                int totalDays = timetable.NumberOfWeeks * 5;

                while (examDates.Count < totalDays && currentDate <= timetable.EndDate)
                {
                    if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                    {
                        examDates.Add(currentDate);
                    }
                    currentDate = currentDate.AddDays(1);
                }

                if (examDates.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No valid exam dates found";
                    return result;
                }

                int sessionsGenerated = 0;
                var startTimes = new List<TimeSpan>
                {
                    new TimeSpan(9, 0, 0),
                    new TimeSpan(11, 0, 0),
                    new TimeSpan(13, 0, 0)
                };
                int timeSlotIndex = 0;

                // Schedule exams for each grade
                foreach (var grade in grades.OrderBy(g => g.Level))
                {
                    var gradeSubjects = subjectGradeGroups
                        .Where(sg => sg.GradeId == grade.Id)
                        .OrderBy(sg => sg.SubjectName)
                        .ToList();

                    if (!gradeSubjects.Any()) continue;

                    var usedDates = gradeSchedule[grade.Id];
                    int dateIndex = 0;

                    foreach (var subject in gradeSubjects)
                    {
                        var papers = new List<(int PaperNumber, decimal? Duration)>();
                        if (subject.HasPaper1) papers.Add((1, subject.Paper1Duration));
                        if (subject.HasPaper2) papers.Add((2, subject.Paper2Duration));
                        if (subject.HasPaper3) papers.Add((3, subject.Paper3Duration));

                        foreach (var paper in papers)
                        {
                            DateTime? selectedDate = null;

                            for (int i = dateIndex; i < examDates.Count; i++)
                            {
                                if (!usedDates.Contains(examDates[i]))
                                {
                                    selectedDate = examDates[i];
                                    usedDates.Add(examDates[i]);
                                    dateIndex = i + 1;
                                    break;
                                }
                            }

                            if (selectedDate == null)
                            {
                                for (int i = 0; i < dateIndex && i < examDates.Count; i++)
                                {
                                    if (!usedDates.Contains(examDates[i]))
                                    {
                                        selectedDate = examDates[i];
                                        usedDates.Add(examDates[i]);
                                        dateIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            if (selectedDate.HasValue)
                            {
                                var startTime = startTimes[timeSlotIndex % startTimes.Count];
                                var duration = paper.Duration ?? 1;
                                var endTime = startTime.Add(TimeSpan.FromHours((double)duration));

                                var session = new ExamSession
                                {
                                    ExamTimetableId = timetableId,
                                    SubjectId = subject.SubjectId,
                                    GradeId = grade.Id,
                                    StreamId = null,
                                    PaperNumber = paper.PaperNumber,
                                    ExamDate = selectedDate.Value,
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    DurationHours = duration,
                                    WeekNumber = (examDates.IndexOf(selectedDate.Value) / 5) + 1,
                                    Venue = GetVenueForGrade(grade.Level),
                                    Invigilator = "To be assigned",
                                    IsActive = true
                                };

                                _context.ExamSessions.Add(session);
                                sessionsGenerated++;
                                timeSlotIndex++;
                            }
                            else
                            {
                                result.Warnings.Add($"Could not schedule {subject.SubjectName} Grade {subject.GradeName} Paper {paper.PaperNumber}");
                            }
                        }
                    }
                }

                timetable.Status = ExamTimetableStatus.Generated;
                timetable.GeneratedAt = DateTime.Now;
                _context.SaveChanges();

                result.Success = true;
                result.SessionsGenerated = sessionsGenerated;
                result.Message = $"Timetable generated with {sessionsGenerated} sessions";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
            }

            return result;
        }

        private string GetVenueForGrade(int gradeLevel)
        {
            switch (gradeLevel)
            {
                case 8: return "Grade 8 Block";
                case 9: return "Grade 9 Block";
                case 10: return "Science Block";
                case 11: return "Commerce Block";
                case 12: return "Matric Centre";
                default: return "Main Hall";
            }
        }

        // ============================================
        // DISTRIBUTE AND DELETE
        // ============================================

        public bool DistributeTimetable(int timetableId)
        {
            try
            {
                var timetable = _context.ExamTimetables.Find(timetableId);
                if (timetable == null || timetable.Status != ExamTimetableStatus.Generated)
                    return false;

                timetable.Status = ExamTimetableStatus.Distributed;
                timetable.DistributedAt = DateTime.Now;
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DistributeTimetable: {ex.Message}");
                return false;
            }
        }

        public bool DeleteTimetable(int id)
        {
            try
            {
                var timetable = _context.ExamTimetables.Find(id);
                if (timetable == null)
                    return false;

                if (timetable.Status >= ExamTimetableStatus.Distributed)
                    return false;

                var notifications = _context.TeacherExamNotifications.Where(n => n.ExamTimetableId == id);
                _context.TeacherExamNotifications.RemoveRange(notifications);

                var sessions = _context.ExamSessions.Where(s => s.ExamTimetableId == id);
                _context.ExamSessions.RemoveRange(sessions);

                _context.ExamTimetables.Remove(timetable);
                _context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteTimetable: {ex.Message}");
                return false;
            }
        }

        // ============================================
        // SESSION MANAGEMENT
        // ============================================

        public ExamSession GetExamSession(int id)
        {
            try
            {
                return _context.ExamSessions
                    .Include("Subject")
                    .Include("Grade")
                    .Include("Stream")
                    .FirstOrDefault(s => s.Id == id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetExamSession: {ex.Message}");
                return null;
            }
        }

        public bool EditExamSession(int id, DateTime examDate, TimeSpan startTime, TimeSpan endTime,
                                     decimal durationHours, string venue, string invigilator, int modifiedBy)
        {
            try
            {
                var session = _context.ExamSessions.Find(id);
                if (session == null)
                    return false;

                if (startTime < DAY_START || endTime > DAY_END)
                    return false;
                if (durationHours < MIN_DURATION || durationHours > MAX_DURATION)
                    return false;

                session.ExamDate = examDate;
                session.StartTime = startTime;
                session.EndTime = endTime;
                session.DurationHours = durationHours;
                session.Venue = venue;
                session.Invigilator = invigilator;

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EditExamSession: {ex.Message}");
                return false;
            }
        }

        public List<ExamSession> GetExamSessions(int timetableId)
        {
            try
            {
                return _context.ExamSessions
                    .Include("Subject")
                    .Include("Grade")
                    .Include("Stream")
                    .Where(s => s.ExamTimetableId == timetableId && s.IsActive)
                    .OrderBy(s => s.ExamDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetExamSessions: {ex.Message}");
                return new List<ExamSession>();
            }
        }

        // ============================================
        // STUDENT AND TEACHER VIEWS
        // ============================================

        public List<ExamSession> GetExamSessionsForStudent(int timetableId, int gradeId, int? streamId)
        {
            return GetExamSessionsForStudent(timetableId, gradeId, streamId, null);
        }

        public List<ExamSession> GetExamSessionsForStudent(int timetableId, int gradeId, int? streamId, int? classId)
        {
            try
            {
                // Students only ever see published sessions.
                var query = _context.ExamSessions
                    .Include(s => s.Subject)
                    .Include(s => s.Grade)
                    .Include(s => s.ExamSessionClasses.Select(c => c.Class))
                    .Where(s => s.ExamTimetableId == timetableId
                        && s.GradeId == gradeId
                        && s.IsActive
                        && s.Status == ExamSessionStatus.Published);

                if (classId.HasValue)
                {
                    query = query.Where(s =>
                        !s.ExamSessionClasses.Any()
                        || s.ExamSessionClasses.Any(c => c.ClassId == classId.Value));
                }

                return query
                    .OrderBy(s => s.ExamDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetExamSessionsForStudent: {ex.Message}");
                return new List<ExamSession>();
            }
        }

        public List<ExamSession> GetExamSessionsForTeacher(int timetableId, int teacherId)
        {
            try
            {
                var teacherAssignments = _context.TeacherSubjectAssignments
                    .Where(t => t.TeacherId == teacherId && t.IsActive)
                    .Select(t => new { t.SubjectId, t.ClassId })
                    .ToList();

                var teacherSubjects = teacherAssignments
                    .Select(t => t.SubjectId)
                    .Distinct()
                    .ToList();

                var teacherClasses = teacherAssignments
                    .Select(t => t.ClassId)
                    .Distinct()
                    .ToList();

                // Teachers see published sessions for subjects/classes they teach, plus their
                // own approved or published proposals (so they can prepare even before the
                // principal publishes the whole cycle).
                return _context.ExamSessions
                    .Include(s => s.Subject)
                    .Include(s => s.Grade)
                    .Include(s => s.ExamSessionClasses.Select(c => c.Class))
                    .Where(s => s.ExamTimetableId == timetableId
                        && s.IsActive
                        && ((s.CreatedByTeacherId == teacherId
                                && (s.Status == ExamSessionStatus.Approved || s.Status == ExamSessionStatus.Published))
                            || (s.Status == ExamSessionStatus.Published
                                && teacherSubjects.Contains(s.SubjectId)
                                && (!s.ExamSessionClasses.Any()
                                    || s.ExamSessionClasses.Any(c => teacherClasses.Contains(c.ClassId))))))
                    .OrderBy(s => s.ExamDate)
                    .ThenBy(s => s.StartTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetExamSessionsForTeacher: {ex.Message}");
                return new List<ExamSession>();
            }
        }
    }
}
