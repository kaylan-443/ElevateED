using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ElevateED.Models
{
    public enum ExamTimetableStatus
    {
        Draft = 0,
        AwaitingTeacherInput = 1,
        Generated = 2,
        Distributed = 3,
        Archived = 4
    }

    public enum ExamSessionStatus
    {
        Proposed = 0,
        Approved = 1,
        Published = 2,
        Rejected = 3
    }

    public class ExamTimetable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        [Required]
        public int NumberOfWeeks { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public ExamTimetableStatus Status { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? GeneratedAt { get; set; }

        public DateTime? DistributedAt { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual ApplicationUser Admin { get; set; }

        public virtual ICollection<ExamSession> ExamSessions { get; set; }
        public virtual ICollection<TeacherExamNotification> TeacherNotifications { get; set; }

        public ExamTimetable()
        {
            CreatedAt = DateTime.Now;
            Status = ExamTimetableStatus.Draft;
            IsActive = true;
            ExamSessions = new HashSet<ExamSession>();
            TeacherNotifications = new HashSet<TeacherExamNotification>();
        }
    }

    public class TeacherExamNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExamTimetableId { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        public int GradeId { get; set; }

        public bool HasPaper1 { get; set; }
        public decimal? Paper1Duration { get; set; }

        public bool HasPaper2 { get; set; }
        public decimal? Paper2Duration { get; set; }

        public bool HasPaper3 { get; set; }
        public decimal? Paper3Duration { get; set; }

        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime NotifiedAt { get; set; }

        [ForeignKey("ExamTimetableId")]
        public virtual ExamTimetable ExamTimetable { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }
    }

    public class ExamSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExamTimetableId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        public int GradeId { get; set; }

        public int? StreamId { get; set; }

        public int? CreatedByTeacherId { get; set; }

        [Required]
        public int PaperNumber { get; set; }

        [Required]
        public DateTime ExamDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        public decimal DurationHours { get; set; }

        public int WeekNumber { get; set; }
        public string Venue { get; set; }
        public string Invigilator { get; set; }
        public string Notes { get; set; }
        public ExamSessionStatus Status { get; set; }
        public DateTime? ProposedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool IsActive { get; set; }

        [ForeignKey("ExamTimetableId")]
        public virtual ExamTimetable ExamTimetable { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }

        [ForeignKey("StreamId")]
        public virtual Stream Stream { get; set; }

        [ForeignKey("CreatedByTeacherId")]
        public virtual Teacher CreatedByTeacher { get; set; }

        public virtual ICollection<ExamSessionClass> ExamSessionClasses { get; set; }

        public ExamSession()
        {
            IsActive = true;
            Status = ExamSessionStatus.Approved;
            ExamSessionClasses = new HashSet<ExamSessionClass>();
        }
    }

    public class ExamSessionClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExamSessionId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ExamSessionId")]
        public virtual ExamSession ExamSession { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }
    }
}
