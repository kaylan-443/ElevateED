using ElevateED.Models;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;

namespace ElevateED.Services
{
    public class AttendanceService : IAttendanceService
    {
        public AttendanceSession StartSession(int classId, int teacherId)
        {
            using (var db = new ElevateEDContext())
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var existingSession = db.AttendanceSessions
                    .FirstOrDefault(s => s.ClassId == classId
                        && s.IsActive
                        && s.SessionDate >= today
                        && s.SessionDate < tomorrow);

                if (existingSession != null)
                    return null;

                string otp = GenerateSecureOTP();
                string qrCode = GenerateQRCode();

                var session = new AttendanceSession
                {
                    ClassId = classId,
                    TeacherId = teacherId,
                    SessionDate = DateTime.Now,
                    OTPCode = otp,
                    QRCode = qrCode,
                    OTPExpiry = DateTime.Now.AddMinutes(5),
                    QRCodeExpiry = DateTime.Now.AddHours(2), // QR valid for 2 hours
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                db.AttendanceSessions.Add(session);
                db.SaveChanges();

                var students = db.Students.Where(s => s.ClassId == classId).ToList();

                foreach (var student in students)
                {
                    var record = new AttendanceRecord
                    {
                        AttendanceSessionId = session.AttendanceSessionId,
                        StudentId = student.Id,
                        IsPresent = false,
                        MarkedAt = null,
                        IsManualOverride = false
                    };
                    db.AttendanceRecords.Add(record);
                }

                db.SaveChanges();
                return session;
            }
        }

        // NEW: QR Code scanning for students
        public string ScanQRCode(string qrCode, int studentId)
        {
            using (var db = new ElevateEDContext())
            {
                var student = db.Students
                    .Include(s => s.Class)
                    .FirstOrDefault(s => s.Id == studentId);

                if (student == null || student.ClassId == null)
                    return "Student not found or not assigned to a class.";

                var session = db.AttendanceSessions
                    .Include(s => s.Class)
                    .FirstOrDefault(s => s.QRCode == qrCode
                        && s.IsActive
                        && s.QRCodeExpiry > DateTime.Now);

                if (session == null)
                    return "Invalid or expired QR code.";

                // Check if student belongs to the same class
                if (session.ClassId != student.ClassId)
                    return "This QR code is for a different class. You cannot use it.";

                // Check if student is in the same grade
                if (session.Class?.GradeId != student.Class?.GradeId)
                    return "This QR code is for a different grade.";

                var record = db.AttendanceRecords
                    .FirstOrDefault(r => r.AttendanceSessionId == session.AttendanceSessionId
                        && r.StudentId == studentId);

                if (record == null)
                    return "You are not enrolled in this class.";

                if (record.IsPresent)
                    return "You have already been marked present for this session.";

                record.IsPresent = true;
                record.MarkedAt = DateTime.Now;
                record.IsManualOverride = false;
                db.SaveChanges();

                return $"SUCCESS|Attendance marked for {session.Class?.FullName}";
            }
        }

        public bool SubmitOTP(string otpCode, int studentId)
        {
            using (var db = new ElevateEDContext())
            {
                var student = db.Students.FirstOrDefault(s => s.Id == studentId);
                if (student == null || student.ClassId == null)
                    return false;

                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.OTPCode == otpCode
                        && s.IsActive
                        && s.OTPExpiry > DateTime.Now
                        && s.ClassId == student.ClassId);

                if (session == null)
                    return false;

                var record = db.AttendanceRecords
                    .FirstOrDefault(r => r.AttendanceSessionId == session.AttendanceSessionId
                        && r.StudentId == studentId);

                if (record == null)
                    return false;

                if (record.IsPresent)
                    return false;

                record.IsPresent = true;
                record.MarkedAt = DateTime.Now;
                record.IsManualOverride = false;

                db.SaveChanges();
                return true;
            }
        }

        public EditAttendanceViewModel GetEditViewModel(int sessionId)
        {
            using (var db = new ElevateEDContext())
            {
                var session = db.AttendanceSessions
                    .Include("Class")
                    .FirstOrDefault(s => s.AttendanceSessionId == sessionId);

                if (session == null)
                    return null;

                var records = db.AttendanceRecords
                    .Include("Student")
                    .Where(r => r.AttendanceSessionId == sessionId)
                    .OrderBy(r => r.Student.FirstName)
                    .ThenBy(r => r.Student.LastName)
                    .ToList();

                var viewModel = new EditAttendanceViewModel
                {
                    SessionId = sessionId,
                    ClassName = session.Class?.FullName ?? "Unknown Class",
                    SessionDate = session.SessionDate,
                    Records = records.Select(r => new AttendanceEditRow
                    {
                        RecordId = r.AttendanceRecordId,
                        StudentId = r.StudentId,
                        FullName = r.Student.FirstName + " " + r.Student.LastName,
                        IsPresent = r.IsPresent
                    }).ToList()
                };

                return viewModel;
            }
        }

        public void SaveManualOverrides(EditAttendanceViewModel model)
        {
            using (var db = new ElevateEDContext())
            {
                foreach (var row in model.Records)
                {
                    var record = db.AttendanceRecords
                        .FirstOrDefault(r => r.AttendanceRecordId == row.RecordId);

                    if (record != null)
                    {
                        record.IsPresent = row.IsPresent;
                        record.IsManualOverride = true;
                        record.MarkedAt = DateTime.Now;
                    }
                }

                db.SaveChanges();
            }
        }

        public AnalyticsViewModel GetAnalytics(string filter, int? classId, string teacherId)
        {
            using (var db = new ElevateEDContext())
            {
                var viewModel = new AnalyticsViewModel { Filter = filter };

                List<Class> availableClasses = new List<Class>();

                if (!string.IsNullOrEmpty(teacherId))
                {
                    var teacher = db.Teachers.FirstOrDefault(t => t.User.StudentNumber == teacherId);
                    if (teacher != null)
                    {
                        var classTeacherClasses = db.Classes
                            .Include("Grade")
                            .Where(c => c.ClassTeacherId == teacher.Id)
                            .ToList();
                        availableClasses.AddRange(classTeacherClasses);

                        var subjectClasses = db.TeacherSubjectAssignments
                            .Include("Class")
                            .Include("Class.Grade")
                            .Where(a => a.TeacherId == teacher.Id && a.IsActive)
                            .Select(a => a.Class)
                            .ToList();

                        foreach (var cls in subjectClasses)
                        {
                            if (cls != null && !availableClasses.Any(c => c.Id == cls.Id))
                                availableClasses.Add(cls);
                        }
                    }
                }
                else
                {
                    availableClasses = db.Classes.Include("Grade").ToList();
                }

                viewModel.AvailableClasses = availableClasses
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.FullName + " (" + c.Grade?.Name + ")" })
                    .ToList();

                if (!classId.HasValue && availableClasses.Any())
                    classId = availableClasses.First().Id;

                viewModel.ClassId = classId;
                DateTime startDate = GetStartDate(filter);

                if (!classId.HasValue)
                    return viewModel;

                var sessions = db.AttendanceSessions
                    .Where(s => s.ClassId == classId && s.SessionDate >= startDate)
                    .Include("Class")
                    .ToList();

                viewModel.TotalSessions = sessions.Count;

                if (sessions.Count == 0)
                    return viewModel;

                var sessionIds = sessions.Select(s => s.AttendanceSessionId).ToList();
                var records = db.AttendanceRecords
                    .Where(r => sessionIds.Contains(r.AttendanceSessionId))
                    .Include("Student")
                    .ToList();

                var studentStats = records
                    .GroupBy(r => r.Student)
                    .Select(g => new LearnerStat
                    {
                        StudentId = g.Key.Id,
                        FullName = g.Key.FirstName + " " + g.Key.LastName,
                        PresentCount = g.Count(r => r.IsPresent),
                        AbsentCount = g.Count(r => !r.IsPresent),
                        AttendancePercent = viewModel.TotalSessions > 0
                            ? (double)g.Count(r => r.IsPresent) / viewModel.TotalSessions * 100
                            : 0
                    })
                    .OrderBy(s => s.AttendancePercent)
                    .ToList();

                var className = sessions.FirstOrDefault()?.Class?.FullName
                    ?? db.Classes.FirstOrDefault(c => c.Id == classId)?.FullName
                    ?? "Unknown Class";

                var classStats = new ClassAttendanceStat
                {
                    ClassName = className,
                    TotalSessions = viewModel.TotalSessions,
                    AttendanceRate = studentStats.Any() ? studentStats.Average(s => s.AttendancePercent) : 0,
                    AllLearnerStats = studentStats,
                    AtRiskLearners = studentStats.Where(s => s.IsAtRisk).ToList()
                };

                viewModel.ClassStats.Add(classStats);
                viewModel.OverallAttendanceRate = classStats.AttendanceRate;
                viewModel.AtRiskCount = classStats.AtRiskLearners.Count;

                return viewModel;
            }
        }

        private string GenerateSecureOTP()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] tokenData = new byte[4];
                rng.GetBytes(tokenData);
                int otp = (BitConverter.ToInt32(tokenData, 0) & 0x7FFFFFFF) % 1000000;
                return otp.ToString("D6");
            }
        }

        private string GenerateQRCode()
        {
            var random = new Random();
            return "ATT" + DateTime.Now.ToString("yyyyMMddHHmmss") + random.Next(1000, 9999);
        }

        private DateTime GetStartDate(string filter)
        {
            DateTime today = DateTime.Today;
            switch (filter?.ToLower())
            {
                case "daily": return today;
                case "weekly": return today.AddDays(-(int)today.DayOfWeek);
                case "monthly": return new DateTime(today.Year, today.Month, 1);
                default: return today.AddDays(-7);
            }
        }
    }
}