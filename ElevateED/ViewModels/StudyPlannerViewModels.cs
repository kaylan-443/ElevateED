using System;
using System.Collections.Generic;
using ElevateED.Models;

namespace ElevateED.ViewModels
{
    public class StudyPlanCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Missed { get; set; }
        public int Planned { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public bool IsActive { get; set; }
        public string GoalCareerName { get; set; }
        public int? GoalCareerId { get; set; }
    }

    public class SubjectHours
    {
        public string SubjectName { get; set; }
        public double PlannedHours { get; set; }
        public double CompletedHours { get; set; }
    }

    public class ProgressStats
    {
        public int Completed { get; set; }
        public int Missed { get; set; }
        public int Planned { get; set; }
        public int Total { get; set; }
        public double AdherencePercent { get; set; } // completed / decided (completed+missed)
        public int StreakDays { get; set; }
        public double PlannedHours { get; set; }
        public double CompletedHours { get; set; }
        public List<SubjectHours> BySubject { get; set; }

        public ProgressStats()
        {
            BySubject = new List<SubjectHours>();
        }
    }

    public class PlannerIndexViewModel
    {
        public string StudentName { get; set; }
        public List<StudyPlanCardViewModel> Plans { get; set; }
        public StudyPlanCardViewModel ActiveCard { get; set; }
        public ProgressStats Progress { get; set; }
        public int? ActivePlanId { get; set; }

        public PlannerIndexViewModel()
        {
            Plans = new List<StudyPlanCardViewModel>();
        }
    }

    public class DayAvailabilityViewModel
    {
        public int DayOfWeek { get; set; }   // 0=Sunday .. 6=Saturday
        public string DayName { get; set; }
        public bool Enabled { get; set; }
        public string Start { get; set; }     // "HH:mm"
        public string End { get; set; }
    }

    public class GoalCareerOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GapSummary { get; set; } // e.g. "Mathematics +1 level, Physical Sciences +2"
    }

    public class PlannerSetupViewModel
    {
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? TargetExamTimetableId { get; set; }
        public string TimetableName { get; set; }
        public bool HasTimetable { get; set; }
        public List<Subject> Subjects { get; set; }
        public List<DayAvailabilityViewModel> Availability { get; set; }

        // Career goal picker (from the learner's shortlist).
        public List<GoalCareerOption> GoalOptions { get; set; }
        public int? PreselectedGoalCareerId { get; set; }

        public PlannerSetupViewModel()
        {
            Subjects = new List<Subject>();
            Availability = new List<DayAvailabilityViewModel>();
            GoalOptions = new List<GoalCareerOption>();
        }
    }

    // Posted from the setup wizard.
    public class DaySlotInput
    {
        public int DayOfWeek { get; set; }
        public bool Enabled { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
    }

    public class PlannerSetupPost
    {
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public int? TargetExamTimetableId { get; set; }
        public int? GoalCareerId { get; set; }
        public List<int> SubjectIds { get; set; }
        public List<DaySlotInput> Availability { get; set; }

        public PlannerSetupPost()
        {
            SubjectIds = new List<int>();
            Availability = new List<DaySlotInput>();
        }
    }

    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public List<StudySession> Sessions { get; set; }
        public List<ExamSession> Exams { get; set; }

        public CalendarDayViewModel()
        {
            Sessions = new List<StudySession>();
            Exams = new List<ExamSession>();
        }
    }

    public class PlannerCalendarViewModel
    {
        public StudyPlan Plan { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public DateTime? PrevWeek { get; set; }
        public DateTime? NextWeek { get; set; }
        public List<CalendarDayViewModel> Days { get; set; }
        public ProgressStats Progress { get; set; }

        public PlannerCalendarViewModel()
        {
            Days = new List<CalendarDayViewModel>();
        }
    }
}
