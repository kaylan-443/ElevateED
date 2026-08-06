using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    // How prepared a learner is for one specific exam.
    public enum ReadinessBand
    {
        NoPlan = 0,    // learner has no study plan covering the subject
        Urgent = 1,    // needs immediate attention
        AtRisk = 2,    // behind, but recoverable
        OnTrack = 3    // prepared
    }

    // One learner's readiness for one exam.
    public class ReadinessResult
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public DateTime ExamDate { get; set; }
        public int DaysUntilExam { get; set; }

        public bool HasMark { get; set; }
        public decimal LatestMark { get; set; }

        public bool HasPlan { get; set; }
        public int SessionsCompleted { get; set; }
        public int SessionsMissed { get; set; }
        public int SessionsRemaining { get; set; }

        // 0-100. Combines the latest mark with revision actually done.
        public double Score { get; set; }
        public ReadinessBand Band { get; set; }

        // True when the exam is close and the learner is still in the worst band.
        public bool NeedsImmediateAttention { get; set; }

        public string BandLabel
        {
            get
            {
                switch (Band)
                {
                    case ReadinessBand.OnTrack: return "On track";
                    case ReadinessBand.AtRisk: return "At risk";
                    case ReadinessBand.Urgent: return "Urgent";
                    default: return "Not planning";
                }
            }
        }
    }

    // Pure readiness scoring — no database, so it can be unit-tested directly.
    public static class ReadinessCalculator
    {
        // An exam is "imminent" inside this many days.
        public const int ImminentDays = 7;

        // Weighting between what the learner already knows (the mark) and
        // what they have done about it since (revision adherence).
        private const double MarkWeight = 0.6;
        private const double AdherenceWeight = 0.4;

        // Adherence over the sessions the learner has actually reached.
        // Returns null when nothing has been decided yet.
        public static double? Adherence(int completed, int missed)
        {
            int decided = completed + missed;
            if (decided <= 0) return null;
            return (double)completed / decided;
        }

        // Combine mark and adherence into a single 0-100 readiness score.
        // Where one signal is absent the other carries the result, rather than
        // penalising a learner for data the school has not produced yet.
        public static double Score(decimal? latestMark, double? adherence)
        {
            bool hasMark = latestMark.HasValue;
            bool hasAdherence = adherence.HasValue;

            if (!hasMark && !hasAdherence) return 50.0; // nothing known — neutral
            if (!hasAdherence) return Clamp((double)latestMark.Value);
            if (!hasMark) return Clamp(adherence.Value * 100.0);

            double combined = MarkWeight * (double)latestMark.Value
                            + AdherenceWeight * (adherence.Value * 100.0);
            return Clamp(combined);
        }

        // Banding tightens as the exam approaches: the same score that is merely
        // "at risk" three weeks out is urgent in the final week.
        public static ReadinessBand Band(double score, int daysUntilExam)
        {
            bool imminent = daysUntilExam <= ImminentDays;
            double urgentBelow = imminent ? 55.0 : 40.0;
            double atRiskBelow = imminent ? 70.0 : 60.0;

            if (score < urgentBelow) return ReadinessBand.Urgent;
            if (score < atRiskBelow) return ReadinessBand.AtRisk;
            return ReadinessBand.OnTrack;
        }

        private static double Clamp(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return Math.Round(v, 1);
        }
    }

    // Context-bound service: gathers marks and study sessions, then applies
    // ReadinessCalculator per learner and aggregates across a class.
    public class ExamReadinessService
    {
        private readonly ElevateEDContext _context;

        public ExamReadinessService(ElevateEDContext context)
        {
            _context = context;
        }

        // Readiness for one learner against one exam session.
        public ReadinessResult ForStudent(Student student, ExamSession exam)
        {
            var result = new ReadinessResult
            {
                StudentId = student.Id,
                StudentName = student.FullName,
                SubjectId = exam.SubjectId,
                SubjectName = exam.Subject?.Name ?? "Subject",
                ExamDate = exam.ExamDate,
                DaysUntilExam = (exam.ExamDate.Date - DateTime.Today).Days
            };

            // Latest published mark in the exam's subject.
            var latestReport = _context.StudentReportCards
                .Include(r => r.Subjects)
                .Where(r => r.StudentId == student.Id && r.IsPublished)
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            decimal? mark = null;
            if (latestReport != null)
            {
                var subjectRow = latestReport.Subjects.FirstOrDefault(s => s.SubjectId == exam.SubjectId);
                if (subjectRow != null)
                {
                    mark = subjectRow.FinalMark;
                    result.HasMark = true;
                    result.LatestMark = subjectRow.FinalMark;
                }
            }

            // Study sessions for this subject, up to the exam date, on the
            // learner's active plan.
            var activePlan = _context.StudyPlans
                .Where(p => p.StudentId == student.Id && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            double? adherence = null;
            if (activePlan != null)
            {
                var sessions = _context.StudySessions
                    .Where(s => s.StudyPlanId == activePlan.Id
                        && s.SubjectId == exam.SubjectId
                        && s.SessionDate <= exam.ExamDate)
                    .ToList();

                if (sessions.Any())
                {
                    result.HasPlan = true;
                    result.SessionsCompleted = sessions.Count(s => s.Status == StudySessionStatus.Completed);
                    result.SessionsMissed = sessions.Count(s => s.Status == StudySessionStatus.Missed);
                    result.SessionsRemaining = sessions.Count(s => s.Status == StudySessionStatus.Planned);
                    adherence = ReadinessCalculator.Adherence(result.SessionsCompleted, result.SessionsMissed);
                }
            }

            if (!result.HasPlan)
            {
                // No plan covering this subject is a different condition from
                // being unprepared despite one, so it gets its own band.
                result.Band = ReadinessBand.NoPlan;
                result.Score = ReadinessCalculator.Score(mark, null);
                return result;
            }

            result.Score = ReadinessCalculator.Score(mark, adherence);
            result.Band = ReadinessCalculator.Band(result.Score, result.DaysUntilExam);
            result.NeedsImmediateAttention =
                result.Band == ReadinessBand.Urgent
                && result.DaysUntilExam >= 0
                && result.DaysUntilExam <= ReadinessCalculator.ImminentDays;

            return result;
        }

        // Readiness for every learner in a class against one exam.
        public List<ReadinessResult> ForClass(int classId, ExamSession exam)
        {
            var students = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .Where(s => s.ClassId == classId && s.IsActive)
                .ToList();

            return students
                .Select(s => ForStudent(s, exam))
                .OrderBy(r => (int)r.Band)          // Urgent first, NoPlan separated below
                .ThenBy(r => r.Score)
                .ThenBy(r => r.StudentName)
                .ToList();
        }
    }
}
