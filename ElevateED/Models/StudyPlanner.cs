using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public enum StudySessionStatus
    {
        Planned = 0,
        Completed = 1,
        Missed = 2,
        Skipped = 3
    }

    // A learner's personal study plan over a date range, optionally tied to an
    // exam timetable so revision ramps up toward exam dates.
    public class StudyPlan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // Optional link to the exam timetable that drives revision urgency.
        public int? TargetExamTimetableId { get; set; }

        // Optional career goal: subjects blocking this career get extra study time.
        public int? GoalCareerId { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? GeneratedAt { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("TargetExamTimetableId")]
        public virtual ExamTimetable TargetExamTimetable { get; set; }

        [ForeignKey("GoalCareerId")]
        public virtual Career GoalCareer { get; set; }

        public virtual ICollection<StudyAvailabilitySlot> AvailabilitySlots { get; set; }
        public virtual ICollection<StudySession> Sessions { get; set; }

        public StudyPlan()
        {
            IsActive = true;
            CreatedAt = DateTime.Now;
            AvailabilitySlots = new HashSet<StudyAvailabilitySlot>();
            Sessions = new HashSet<StudySession>();
        }
    }

    // A recurring weekly window when the learner is free to study,
    // e.g. Monday 18:00-20:00.
    public class StudyAvailabilitySlot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudyPlanId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        [ForeignKey("StudyPlanId")]
        public virtual StudyPlan StudyPlan { get; set; }
    }

    // A single generated study block for one subject on one date.
    public class StudySession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudyPlanId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [Required]
        public DateTime SessionDate { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public StudySessionStatus Status { get; set; }

        // Auto-filled hint, e.g. "Final revision for Mathematics P1 (in 2 days)".
        [StringLength(200)]
        public string FocusNote { get; set; }

        // Optional link to the exam this session is revising for.
        public int? LinkedExamSessionId { get; set; }

        public DateTime? CompletedAt { get; set; }

        [ForeignKey("StudyPlanId")]
        public virtual StudyPlan StudyPlan { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        [ForeignKey("LinkedExamSessionId")]
        public virtual ExamSession LinkedExamSession { get; set; }

        public StudySession()
        {
            Status = StudySessionStatus.Planned;
        }
    }
}
