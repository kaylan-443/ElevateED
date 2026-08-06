using System;
using System.Collections.Generic;
using System.Linq;
using ElevateED.Models;
using ElevateED.Services;

namespace ElevateED.ViewModels
{
    // A class the teacher may review, with the subject they teach it for.
    public class TeachableClassOption
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int UpcomingExamCount { get; set; }
    }

    // An exam the teacher may review a class against.
    public class ReviewableExamOption
    {
        public int ExamSessionId { get; set; }
        public string SubjectName { get; set; }
        public int PaperNumber { get; set; }
        public DateTime ExamDate { get; set; }
        public int DaysUntil { get; set; }

        public string Label
        {
            get
            {
                var paper = PaperNumber > 0 ? " P" + PaperNumber : "";
                return SubjectName + paper + " — " + ExamDate.ToString("ddd dd MMM");
            }
        }
    }

    // Landing page: pick a class and exam to review.
    public class ReadinessIndexViewModel
    {
        public string TeacherName { get; set; }
        public List<TeachableClassOption> Classes { get; set; }
        public bool HasActiveTimetable { get; set; }
        public string TimetableName { get; set; }

        public ReadinessIndexViewModel()
        {
            Classes = new List<TeachableClassOption>();
        }
    }

    // The class readiness dashboard for one exam.
    public class ClassReadinessViewModel
    {
        public string ClassName { get; set; }
        public int ClassId { get; set; }
        public ReviewableExamOption Exam { get; set; }
        public List<ReviewableExamOption> OtherExams { get; set; }

        // Every learner, already ordered worst-first by the service.
        public List<ReadinessResult> Results { get; set; }

        public ClassReadinessViewModel()
        {
            Results = new List<ReadinessResult>();
            OtherExams = new List<ReviewableExamOption>();
        }

        // Learners who have a plan, split by how prepared they are.
        public List<ReadinessResult> Urgent =>
            Results.Where(r => r.Band == ReadinessBand.Urgent).ToList();

        public List<ReadinessResult> AtRisk =>
            Results.Where(r => r.Band == ReadinessBand.AtRisk).ToList();

        public List<ReadinessResult> OnTrack =>
            Results.Where(r => r.Band == ReadinessBand.OnTrack).ToList();

        // Not the same as being at risk: these learners never started planning.
        public List<ReadinessResult> NotPlanning =>
            Results.Where(r => r.Band == ReadinessBand.NoPlan).ToList();

        // The shorter list the teacher should act on first.
        public List<ReadinessResult> ImmediateAttention =>
            Results.Where(r => r.NeedsImmediateAttention).ToList();

        public int TotalLearners => Results.Count;

        public int PlanningCount => Results.Count(r => r.HasPlan);

        public double AverageScore => Results.Any(r => r.HasPlan)
            ? Math.Round(Results.Where(r => r.HasPlan).Average(r => r.Score), 1)
            : 0;

        // Percentage of the class in a band, for the distribution bar.
        public double Percent(int count) =>
            TotalLearners > 0 ? Math.Round((double)count / TotalLearners * 100, 1) : 0;
    }

    // Drill-down into one learner's readiness.
    public class LearnerReadinessDetailViewModel
    {
        public ReadinessResult Result { get; set; }
        public string ClassName { get; set; }
        public int ClassId { get; set; }
        public int ExamSessionId { get; set; }
        public List<StudySession> Sessions { get; set; }

        public LearnerReadinessDetailViewModel()
        {
            Sessions = new List<StudySession>();
        }
    }
}
