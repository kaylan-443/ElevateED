using System.Collections.Generic;
using System.Linq;
using ElevateED.Services;

namespace ElevateED.ViewModels
{
    // A learner's own readiness across every upcoming exam — ordered
    // chronologically, since this is one student's timetable, not a class
    // to triage by urgency.
    public class StudentReadinessViewModel
    {
        public string StudentName { get; set; }
        public bool HasActiveTimetable { get; set; }
        public string TimetableName { get; set; }
        public List<ReadinessResult> Exams { get; set; }

        public StudentReadinessViewModel()
        {
            Exams = new List<ReadinessResult>();
        }

        public List<ReadinessResult> ImmediateAttention =>
            Exams.Where(r => r.NeedsImmediateAttention).ToList();
    }
}
