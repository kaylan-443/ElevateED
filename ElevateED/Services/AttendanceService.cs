using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Models.ViewModels;

namespace ElevateED.Services
{
    /// <summary>
    /// Service implementation for attendance management operations.
    /// </summary>
    public class AttendanceService : IAttendanceService
    {
        /// <summary>
        /// Start a new attendance session with OTP generation.
        /// </summary>
        public AttendanceSession StartSession(int classId, string teacherId)
        {
            using (var db = new ElevateEDContext())
            {
                // Check for existing active session today
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var existingSession = db.AttendanceSessions
                    .FirstOrDefault(s => s.ClassId == classId 
                        && s.IsActive 
                        && s.SessionDate >= today 
                        && s.SessionDate < tomorrow);

                if (existingSession != null)
                    return null;

                // Generate 6-digit OTP using RNGCryptoServiceProvider
                string otp = GenerateSecureOTP();

                var session = new AttendanceSession
                {
                    ClassId = classId,
                    TeacherId = teacherId,
                    SessionDate = DateTime.Now,
                    OTPCode = otp,
                    OTPExpiry = DateTime.Now.AddMinutes(5),
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                db.AttendanceSessions.Add(session);
                db.SaveChanges();

                // Get all students in this class and create attendance records
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

        /// <summary>
        /// Submit OTP and mark student as present.
        /// </summary>
        public bool SubmitOTP(string otpCode, int studentId)
        {
            using (var db = new ElevateEDContext())
            {
                // Find student and their class
                var student = db.Students.FirstOrDefault(s => s.Id == studentId);
                if (student == null || student.ClassId == null)
                    return false;

                // Find valid active session with matching OTP
                var session = db.AttendanceSessions
                    .FirstOrDefault(s => s.OTPCode == otpCode
                        && s.IsActive
                        && s.OTPExpiry > DateTime.Now
                        && s.ClassId == student.ClassId);

                if (session == null)
                    return false;

                // Find or create attendance record
                var record = db.AttendanceRecords
                    .FirstOrDefault(r => r.AttendanceSessionId == session.AttendanceSessionId
                        && r.StudentId == studentId);

                if (record == null)
                    return false;

                // Prevent duplicate marking
                if (record.IsPresent)
                    return false;

                record.IsPresent = true;
                record.MarkedAt = DateTime.Now;
                record.IsManualOverride = false;

                db.SaveChanges();
                return true;
            }
        }

        /// <summary>
        /// Get edit view model for manual attendance override.
        /// </summary>
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
                    ClassName = session.Class.FullName,
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

        /// <summary>
        /// Save manual attendance overrides.
        /// </summary>
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

        /// <summary>
        /// Get analytics data for attendance statistics.
        /// </summary>
        public AnalyticsViewModel GetAnalytics(string filter, int? classId, string teacherId)
        {
            using (var db = new ElevateEDContext())
            {
                var viewModel = new AnalyticsViewModel { Filter = filter };

                // Get all classes (admins see all, teachers see only their classes)
                var availableClasses = db.Classes.ToList();

                // For teachers, filter to their assigned classes
                if (!string.IsNullOrEmpty(teacherId))
                {
                    var teacher = db.Teachers.FirstOrDefault(t => t.User.StudentNumber == teacherId);
                    if (teacher != null)
                    {
                        availableClasses = availableClasses
                            .Where(c => c.ClassTeacherId == teacher.Id)
                            .ToList();
                    }
                }

                viewModel.AvailableClasses = availableClasses
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.FullName })
                    .ToList();

                // Default to first class if not specified
                if (!classId.HasValue && availableClasses.Count > 0)
                    classId = availableClasses.First().Id;

                viewModel.ClassId = classId;

                // Calculate date range based on filter
                DateTime startDate = GetStartDate(filter);

                if (!classId.HasValue)
                    return viewModel;

                // Query sessions and records for the selected class and date range
                var sessions = db.AttendanceSessions
                    .Where(s => s.ClassId == classId && s.SessionDate >= startDate)
                    .Include("Class")
                    .ToList();

                viewModel.TotalSessions = sessions.Count;

                if (sessions.Count == 0)
                    return viewModel;

                var records = db.AttendanceRecords
                    .Where(r => sessions.Select(s => s.AttendanceSessionId).Contains(r.AttendanceSessionId))
                    .Include("Student")
                    .ToList();

                // Calculate per-student statistics
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

                var className = sessions.FirstOrDefault()?.Class?.FullName ?? db.Classes.FirstOrDefault(c => c.Id == classId)?.FullName;
                
                var classStats = new ClassAttendanceStat
                {
                    ClassName = className,
                    TotalSessions = viewModel.TotalSessions,
                    AttendanceRate = studentStats.Count > 0 ? studentStats.Average(s => s.AttendancePercent) : 0,
                    AllLearnerStats = studentStats,
                    AtRiskLearners = studentStats.Where(s => s.IsAtRisk).ToList()
                };

                viewModel.ClassStats.Add(classStats);
                viewModel.OverallAttendanceRate = classStats.AttendanceRate;
                viewModel.AtRiskCount = classStats.AtRiskLearners.Count;

                return viewModel;
            }
        }

        /// <summary>
        /// Generate a secure 6-digit OTP using RNGCryptoServiceProvider.
        /// </summary>
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

        /// <summary>
        /// Calculate start date based on filter period.
        /// </summary>
        private DateTime GetStartDate(string filter)
        {
            DateTime today = DateTime.Today;
            if (filter == "daily")
                return today;
            else if (filter == "weekly")
                return today.AddDays(-(int)today.DayOfWeek);
            else if (filter == "monthly")
                return new DateTime(today.Year, today.Month, 1);
            else
                return today.AddDays(-7);
        }
    }
}
