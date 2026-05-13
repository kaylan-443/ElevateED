using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ElevateED.ViewModels
{
    public class CreateExamTimetableViewModel
    {
        [Required]
        [Display(Name = "Timetable Name")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Academic Year")]
        public int AcademicYear { get; set; }

        [Required]
        [Display(Name = "Number of Weeks")]
        [Range(1, 4)]
        public int NumberOfWeeks { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }
    }

    public class ExamTimetableViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int AcademicYear { get; set; }
        public int NumberOfWeeks { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ExamTimetableStatus Status { get; set; }
        public string StatusDisplay { get; set; }
        public string StatusBadgeClass { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public DateTime? DistributedAt { get; set; }
        public int TotalSessions { get; set; }
        public bool CanGenerate { get; set; }
        public bool CanDistribute { get; set; }
    }

    public class ExamTimetableDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int AcademicYear { get; set; }
        public int NumberOfWeeks { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ExamTimetableStatus Status { get; set; }
        public List<ExamSessionViewModel> ExamSessions { get; set; }
        public Dictionary<string, List<ExamSessionViewModel>> SessionsByWeek { get; set; }

        public ExamTimetableDetailViewModel()
        {
            ExamSessions = new List<ExamSessionViewModel>();
            SessionsByWeek = new Dictionary<string, List<ExamSessionViewModel>>();
        }
    }

    public class ExamSessionViewModel
    {
        public int Id { get; set; }
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string StreamName { get; set; }
        public string ClassNames { get; set; }
        public string PaperNumber { get; set; }
        public DateTime ExamDate { get; set; }
        public string ExamDateDisplay { get; set; }
        public string DayOfWeek { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public decimal DurationHours { get; set; }
        public int WeekNumber { get; set; }
        public string Venue { get; set; }
        public string Invigilator { get; set; }
        public bool IsEditable { get; set; }
    }

    public class StudentExamTimetableViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int AcademicYear { get; set; }
        public int NumberOfWeeks { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string StudentName { get; set; }
        public string GradeName { get; set; }
        public string StreamName { get; set; }
        public List<ExamSessionViewModel> ExamSessions { get; set; }
        public Dictionary<string, List<ExamSessionViewModel>> SessionsByWeek { get; set; }

        public StudentExamTimetableViewModel()
        {
            ExamSessions = new List<ExamSessionViewModel>();
            SessionsByWeek = new Dictionary<string, List<ExamSessionViewModel>>();
        }
    }

    public class TeacherPaperDurationViewModel
    {
        public int NotificationId { get; set; }
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public bool HasPaper1 { get; set; }
        public decimal? Paper1Duration { get; set; }
        public bool HasPaper2 { get; set; }
        public decimal? Paper2Duration { get; set; }
        public bool HasPaper3 { get; set; }
        public decimal? Paper3Duration { get; set; }
    }

    public class TeacherExamNotificationViewModel
    {
        public List<TeacherPaperDurationViewModel> PendingNotifications { get; set; }
        public int ExamTimetableId { get; set; }
        public string TimetableName { get; set; }
        public DateTime ResponseDeadline { get; set; }

        public TeacherExamNotificationViewModel()
        {
            PendingNotifications = new List<TeacherPaperDurationViewModel>();
        }
    }

    public class EditExamSessionViewModel
    {
        public int Id { get; set; }
        public int ExamTimetableId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ExamDate { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        [Required]
        [Range(1, 3)]
        public decimal DurationHours { get; set; }

        public string Venue { get; set; }
        public string Invigilator { get; set; }

        // Read-only display fields
        public string SubjectName { get; set; }
        public string GradeName { get; set; }
        public string StreamName { get; set; }
        public string PaperNumber { get; set; }
    }

    // ============================================================
    // New teacher-proposed exam session flow
    // ============================================================

    public class ProposeExamViewModel
    {
        public int Id { get; set; } // 0 for new

        [Required]
        [Display(Name = "Exam Cycle")]
        public int ExamTimetableId { get; set; }

        [Required]
        [Display(Name = "Subject")]
        public int SubjectId { get; set; }

        [Required]
        [Range(1, 3)]
        [Display(Name = "Paper Number")]
        public int PaperNumber { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime ExamDate { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; }

        [Required]
        [Range(0.5, 6.0)]
        [Display(Name = "Duration (hours)")]
        public decimal DurationHours { get; set; }

        [StringLength(120)]
        public string Venue { get; set; }

        [StringLength(120)]
        public string Invigilator { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        [Required(ErrorMessage = "Pick at least one class.")]
        [Display(Name = "Classes Writing")]
        public List<int> ClassIds { get; set; }

        public IEnumerable<System.Web.Mvc.SelectListItem> ExamCycleOptions { get; set; }
        public IEnumerable<System.Web.Mvc.SelectListItem> SubjectOptions { get; set; }
        public IEnumerable<ClassCheckboxOption> ClassOptions { get; set; }

        public ProposeExamViewModel()
        {
            ClassIds = new List<int>();
            ClassOptions = new List<ClassCheckboxOption>();
            ExamCycleOptions = new List<System.Web.Mvc.SelectListItem>();
            SubjectOptions = new List<System.Web.Mvc.SelectListItem>();
            DurationHours = 2;
        }
    }

    public class ClassCheckboxOption
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public string GradeName { get; set; }
        public bool Selected { get; set; }
    }

    public class MyExamProposalListItemViewModel
    {
        public int Id { get; set; }
        public string ExamCycleName { get; set; }
        public string SubjectName { get; set; }
        public int PaperNumber { get; set; }
        public DateTime ExamDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal DurationHours { get; set; }
        public string Venue { get; set; }
        public string Invigilator { get; set; }
        public string ClassNames { get; set; }
        public ExamSessionStatus Status { get; set; }
        public DateTime? ProposedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class ExamProposalReviewListItemViewModel
    {
        public int Id { get; set; }
        public string ExamCycleName { get; set; }
        public int ExamCycleId { get; set; }
        public string SubjectName { get; set; }
        public int PaperNumber { get; set; }
        public DateTime ExamDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal DurationHours { get; set; }
        public string Venue { get; set; }
        public string Invigilator { get; set; }
        public string Notes { get; set; }
        public string ClassNames { get; set; }
        public string ProposedBy { get; set; }
        public ExamSessionStatus Status { get; set; }
        public DateTime? ProposedAt { get; set; }
        public List<string> Clashes { get; set; }

        public ExamProposalReviewListItemViewModel()
        {
            Clashes = new List<string>();
        }
    }
}
