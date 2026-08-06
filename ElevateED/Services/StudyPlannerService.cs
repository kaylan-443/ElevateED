using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    // Details about one upcoming exam for a subject.
    public class ExamInfo
    {
        public int ExamSessionId { get; set; }
        public DateTime Date { get; set; }
        public int PaperNumber { get; set; }
    }

    // Everything the generator needs to know about one subject.
    public class SubjectStudyInfo
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }

        // 0.05 (very strong) .. 1.0 (very weak). Drives how much time it gets.
        public double Weakness { get; set; }

        // 1.0 = no boost. Raised when this subject blocks the learner's career goal.
        public double GoalBoost { get; set; }

        // Set when boosted, e.g. "Medicine" — surfaces on the session's focus note.
        public string GoalCareerName { get; set; }

        // 1.0 = no boost. Raised when the learner has set themselves a target
        // mark in this subject that they have not reached yet.
        public double TargetBoost { get; set; }

        // The target itself, when one is set — surfaces on the focus note.
        public decimal? TargetMark { get; set; }

        // Upcoming exams for this subject, sorted ascending by date.
        public List<ExamInfo> UpcomingExams { get; set; }

        public SubjectStudyInfo()
        {
            Weakness = 0.5;
            GoalBoost = 1.0;
            TargetBoost = 1.0;
            UpcomingExams = new List<ExamInfo>();
        }

        public ExamInfo NextExamOnOrAfter(DateTime date)
        {
            return UpcomingExams
                .Where(e => e.Date.Date >= date.Date)
                .OrderBy(e => e.Date)
                .FirstOrDefault();
        }

        // True once every known exam for this subject is in the past.
        public bool AllExamsPassed(DateTime date)
        {
            return UpcomingExams.Any() && UpcomingExams.All(e => e.Date.Date < date.Date);
        }
    }

    // A concrete study block (before a subject is assigned to it).
    internal class PlannerBlock
    {
        public DateTime Date;
        public TimeSpan Start;
        public TimeSpan End;
        public int? ReservedSubjectId;      // set during final-revision reservation
        public int? ReservedExamSessionId;
    }

    // Pure scheduling logic — no database, so it can be unit-tested directly.
    public static class StudyPlanGenerator
    {
        private const double MaxBlockHours = 2.0;

        // Weighting: urgency ramps up over the final two weeks before an exam.
        public static double ExamUrgency(SubjectStudyInfo subject, DateTime onDate)
        {
            var next = subject.NextExamOnOrAfter(onDate);
            if (next == null) return 1.0; // no exam scheduled — baseline urgency
            int daysUntil = (next.Date.Date - onDate.Date).Days;
            return 1.0 + Math.Max(0, (14 - daysUntil)) / 14.0;
        }

        // A career goal and a self-set target mark are independent signals, so
        // they stack — but the combined boost is capped, otherwise a subject
        // carrying both could crowd every other subject off the timetable.
        public const double MaxCombinedBoost = 2.5;

        public static double CombinedBoost(SubjectStudyInfo subject)
        {
            return Math.Min(MaxCombinedBoost, subject.GoalBoost * subject.TargetBoost);
        }

        public static double Priority(SubjectStudyInfo subject, DateTime onDate)
        {
            return ExamUrgency(subject, onDate) * subject.Weakness * CombinedBoost(subject);
        }

        // Build the ordered study sessions for a plan.
        public static List<StudySession> Generate(
            DateTime startDate,
            DateTime endDate,
            IEnumerable<StudyAvailabilitySlot> slots,
            List<SubjectStudyInfo> subjects)
        {
            var result = new List<StudySession>();
            if (subjects == null || !subjects.Any()) return result;

            var slotsByDay = slots
                .GroupBy(s => s.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());

            // 1. Expand availability into concrete blocks across the date range.
            var blocks = new List<PlannerBlock>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!slotsByDay.ContainsKey(date.DayOfWeek)) continue;
                foreach (var slot in slotsByDay[date.DayOfWeek])
                    blocks.AddRange(SplitSlot(date, slot));
            }
            blocks = blocks.OrderBy(b => b.Date).ThenBy(b => b.Start).ToList();
            if (!blocks.Any()) return result;

            // 2. Reserve the last up-to-2 blocks before each exam for final revision.
            ReserveFinalRevision(blocks, subjects);

            // 3. Fill every block, weighting toward urgent + weak subjects,
            //    but never the same subject more than twice in a row.
            int lastSubject = -1, lastSubjectRun = 0;
            foreach (var block in blocks)
            {
                SubjectStudyInfo chosen = null;
                ExamInfo linkedExam = null;

                if (block.ReservedSubjectId.HasValue)
                {
                    chosen = subjects.FirstOrDefault(s => s.SubjectId == block.ReservedSubjectId.Value);
                    if (chosen != null && block.ReservedExamSessionId.HasValue)
                        linkedExam = chosen.UpcomingExams.FirstOrDefault(e => e.ExamSessionId == block.ReservedExamSessionId.Value);
                }

                if (chosen == null)
                {
                    // Candidates: skip subjects whose exams have all passed.
                    var candidates = subjects.Where(s => !s.AllExamsPassed(block.Date)).ToList();
                    if (!candidates.Any()) candidates = subjects.ToList();

                    // Enforce the no-more-than-2-in-a-row rule.
                    if (lastSubjectRun >= 2 && candidates.Count > 1)
                        candidates = candidates.Where(s => s.SubjectId != lastSubject).ToList();

                    chosen = candidates
                        .OrderByDescending(s => Priority(s, block.Date))
                        .ThenBy(s => s.SubjectId)
                        .First();

                    linkedExam = chosen.NextExamOnOrAfter(block.Date);
                }

                if (chosen.SubjectId == lastSubject) lastSubjectRun++;
                else { lastSubject = chosen.SubjectId; lastSubjectRun = 1; }

                result.Add(new StudySession
                {
                    SubjectId = chosen.SubjectId,
                    SessionDate = block.Date,
                    StartTime = block.Start,
                    EndTime = block.End,
                    Status = StudySessionStatus.Planned,
                    LinkedExamSessionId = block.ReservedSubjectId.HasValue ? block.ReservedExamSessionId : null,
                    FocusNote = BuildFocusNote(chosen, block.Date, linkedExam, block.ReservedSubjectId.HasValue)
                });
            }

            return result;
        }

        private static IEnumerable<PlannerBlock> SplitSlot(DateTime date, StudyAvailabilitySlot slot)
        {
            var duration = slot.EndTime - slot.StartTime;
            if (duration <= TimeSpan.Zero) yield break;

            int count = (int)Math.Ceiling(duration.TotalHours / MaxBlockHours);
            if (count < 1) count = 1;
            var per = TimeSpan.FromTicks(duration.Ticks / count);

            for (int i = 0; i < count; i++)
            {
                var start = slot.StartTime + TimeSpan.FromTicks(per.Ticks * i);
                var end = (i == count - 1) ? slot.EndTime : start + per;
                yield return new PlannerBlock { Date = date, Start = start, End = end };
            }
        }

        private static void ReserveFinalRevision(List<PlannerBlock> blocks, List<SubjectStudyInfo> subjects)
        {
            // For each exam, lock the latest up-to-2 free blocks before it.
            var exams = subjects
                .SelectMany(s => s.UpcomingExams.Select(e => new { Subject = s, Exam = e }))
                .OrderBy(x => x.Exam.Date)
                .ToList();

            foreach (var item in exams)
            {
                var freeBefore = blocks
                    .Where(b => !b.ReservedSubjectId.HasValue && b.Date.Date < item.Exam.Date.Date)
                    .OrderByDescending(b => b.Date).ThenByDescending(b => b.Start)
                    .Take(2)
                    .ToList();

                foreach (var b in freeBefore)
                {
                    b.ReservedSubjectId = item.Subject.SubjectId;
                    b.ReservedExamSessionId = item.Exam.ExamSessionId;
                }
            }
        }

        private static string BuildFocusNote(SubjectStudyInfo subject, DateTime date, ExamInfo linkedExam, bool isFinalRevision)
        {
            string goal = !string.IsNullOrEmpty(subject.GoalCareerName)
                ? " · 🎯 " + subject.GoalCareerName
                : "";

            // Only mention the target when it actually changed the weighting —
            // a target already reached shouldn't imply work still to do.
            if (subject.TargetMark.HasValue && subject.TargetBoost > 1.0)
                goal += " · aiming for " + subject.TargetMark.Value.ToString("0") + "%";

            if (linkedExam != null)
            {
                int days = (linkedExam.Date.Date - date.Date).Days;
                string when = days <= 0 ? "today" : days == 1 ? "tomorrow" : "in " + days + " days";
                string paper = linkedExam.PaperNumber > 0 ? " P" + linkedExam.PaperNumber : "";
                if (isFinalRevision)
                    return "Final revision for " + subject.SubjectName + paper + " (exam " + when + ")" + goal;
                return "Prepare for " + subject.SubjectName + paper + " (exam " + when + ")" + goal;
            }
            return "Study " + subject.SubjectName + goal;
        }
    }

    // Context-bound service: builds generator inputs from the learner's marks and
    // exam timetable, then persists the generated plan.
    public class StudyPlannerService
    {
        private readonly ElevateEDContext _context;
        private readonly IExamTimetableService _examService;

        public StudyPlannerService(ElevateEDContext context)
        {
            _context = context;
            _examService = new ExamTimetableService();
        }

        // The distinct subjects a learner could plan for: report-card subjects,
        // plus any exam-timetable subjects for their grade.
        public List<Subject> GetCandidateSubjects(Student student)
        {
            var subjectIds = new HashSet<int>();

            var reportSubjectIds = _context.StudentReportCardSubjects
                .Where(s => s.StudentReportCard.StudentId == student.Id)
                .Select(s => s.SubjectId)
                .Distinct()
                .ToList();
            foreach (var id in reportSubjectIds) subjectIds.Add(id);

            var timetable = GetActiveTimetable();
            var grade = _context.Grades.FirstOrDefault(g => g.Name == student.Grade);
            if (timetable != null && grade != null)
            {
                var examSubjectIds = _examService
                    .GetExamSessionsForStudent(timetable.Id, grade.Id, student.StreamId, student.ClassId)
                    .Select(e => e.SubjectId).Distinct().ToList();
                foreach (var id in examSubjectIds) subjectIds.Add(id);
            }

            if (!subjectIds.Any()) return new List<Subject>();

            return _context.Subjects
                .Where(s => subjectIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToList();
        }

        public ExamTimetable GetActiveTimetable()
        {
            return _context.ExamTimetables
                .FirstOrDefault(t => t.Status == ExamTimetableStatus.Distributed && t.IsActive && t.EndDate >= DateTime.Now);
        }

        // Build the weakness + upcoming-exam profile for the chosen subjects.
        public List<SubjectStudyInfo> BuildSubjectInfo(Student student, List<int> subjectIds, DateTime startDate, DateTime endDate, ExamTimetable timetable)
        {
            return BuildSubjectInfo(student, subjectIds, startDate, endDate, timetable, null);
        }

        // Overload with an optional career goal: subjects where the learner falls
        // short of the career's requirements get a priority boost.
        public List<SubjectStudyInfo> BuildSubjectInfo(Student student, List<int> subjectIds, DateTime startDate, DateTime endDate, ExamTimetable timetable, int? goalCareerId)
        {
            // Latest published mark per subject → weakness.
            var latestReport = _context.StudentReportCards
                .Include(r => r.Subjects)
                .Where(r => r.StudentId == student.Id && r.IsPublished)
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            var markBySubject = new Dictionary<int, decimal>();
            if (latestReport != null)
                foreach (var s in latestReport.Subjects)
                    if (!markBySubject.ContainsKey(s.SubjectId))
                        markBySubject[s.SubjectId] = s.FinalMark;

            // Upcoming exams per subject within the plan window.
            var examsBySubject = new Dictionary<int, List<ExamInfo>>();
            var grade = _context.Grades.FirstOrDefault(g => g.Name == student.Grade);
            if (timetable != null && grade != null)
            {
                var sessions = _examService.GetExamSessionsForStudent(timetable.Id, grade.Id, student.StreamId, student.ClassId);
                foreach (var s in sessions.Where(e => e.ExamDate.Date >= startDate.Date && e.ExamDate.Date <= endDate.Date.AddDays(1)))
                {
                    if (!examsBySubject.ContainsKey(s.SubjectId))
                        examsBySubject[s.SubjectId] = new List<ExamInfo>();
                    examsBySubject[s.SubjectId].Add(new ExamInfo
                    {
                        ExamSessionId = s.Id,
                        Date = s.ExamDate,
                        PaperNumber = s.PaperNumber
                    });
                }
            }

            // Career goal: boost subjects where the learner is below the career's
            // required level. Boost grows with how many levels they are short.
            var goalBoostBySubject = new Dictionary<int, double>();
            string goalCareerName = null;
            if (goalCareerId.HasValue)
            {
                var goalCareer = _context.Careers
                    .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                    .FirstOrDefault(c => c.Id == goalCareerId.Value);
                if (goalCareer != null)
                {
                    goalCareerName = goalCareer.Name;
                    var careerService = new CareerGuidanceService(_context);
                    var aps = careerService.CalculateApsForStudent(student.Id);
                    foreach (var req in goalCareer.SubjectRequirements.Where(r => r.IsCompulsory))
                    {
                        int level;
                        bool taken = aps.LevelBySubjectId.TryGetValue(req.SubjectId, out level);
                        // No report card yet: treat every required subject as a mild boost.
                        int shortfall = !aps.HasReportCard ? 1
                            : taken ? Math.Max(0, req.MinimumLevel - level)
                            : 0; // subject not taken — can't schedule what they don't have
                        if (shortfall > 0)
                            goalBoostBySubject[req.SubjectId] = Math.Min(2.0, 1.0 + 0.4 * shortfall);
                    }
                }
            }

            // Target marks the learner set for themselves in the what-if
            // simulator. A target they have not reached yet earns extra study
            // time, scaled to how far short of it they currently are.
            var targetBySubject = _context.StudentMarkTargets
                .Where(t => t.StudentId == student.Id && subjectIds.Contains(t.SubjectId))
                .ToDictionary(t => t.SubjectId, t => t.TargetMark);

            var subjects = _context.Subjects.Where(s => subjectIds.Contains(s.Id)).ToList();
            var result = new List<SubjectStudyInfo>();
            foreach (var subj in subjects)
            {
                double weakness = 0.5;
                if (markBySubject.ContainsKey(subj.Id))
                    weakness = Math.Max(0.05, Math.Min(1.0, (double)(100 - markBySubject[subj.Id]) / 100.0));

                bool boosted = goalBoostBySubject.ContainsKey(subj.Id);

                decimal? target = targetBySubject.ContainsKey(subj.Id)
                    ? targetBySubject[subj.Id]
                    : (decimal?)null;
                double targetBoost = TargetBoostFor(
                    target,
                    markBySubject.ContainsKey(subj.Id) ? markBySubject[subj.Id] : (decimal?)null);

                result.Add(new SubjectStudyInfo
                {
                    SubjectId = subj.Id,
                    SubjectName = subj.Name,
                    Weakness = weakness,
                    GoalBoost = boosted ? goalBoostBySubject[subj.Id] : 1.0,
                    GoalCareerName = boosted ? goalCareerName : null,
                    TargetMark = target,
                    TargetBoost = targetBoost,
                    UpcomingExams = examsBySubject.ContainsKey(subj.Id)
                        ? examsBySubject[subj.Id].OrderBy(e => e.Date).ToList()
                        : new List<ExamInfo>()
                });
            }
            return result;
        }

        // How much extra weight a self-set target earns. Full strength at
        // MarksForFullBoost short of the target, nothing once it is reached.
        private const double MarksForFullBoost = 20.0;

        public static double TargetBoostFor(decimal? targetMark, decimal? currentMark)
        {
            if (!targetMark.HasValue) return 1.0;

            // A target set before any mark is published still says the learner
            // cares about the subject, but there is no shortfall to size it by.
            if (!currentMark.HasValue) return 1.3;

            double shortfall = (double)(targetMark.Value - currentMark.Value);
            if (shortfall <= 0) return 1.0; // already at or past the target

            return 1.0 + Math.Min(1.0, shortfall / MarksForFullBoost);
        }

        // Regenerate future planned sessions for a plan, preserving history.
        public void Regenerate(StudyPlan plan, List<int> subjectIds)
        {
            var timetable = plan.TargetExamTimetableId.HasValue
                ? _context.ExamTimetables.Find(plan.TargetExamTimetableId.Value)
                : GetActiveTimetable();

            var student = _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.Id == plan.StudentId);
            if (student == null) return;

            // Only rewrite future, still-planned sessions. Keep completed/missed history.
            var today = DateTime.Today;
            var toRemove = _context.StudySessions
                .Where(s => s.StudyPlanId == plan.Id
                    && s.Status == StudySessionStatus.Planned
                    && s.SessionDate >= today)
                .ToList();
            _context.StudySessions.RemoveRange(toRemove);

            var effectiveStart = today > plan.StartDate ? today : plan.StartDate;
            var slots = _context.StudyAvailabilitySlots.Where(s => s.StudyPlanId == plan.Id).ToList();
            var subjectInfo = BuildSubjectInfo(student, subjectIds, effectiveStart, plan.EndDate, timetable, plan.GoalCareerId);

            var sessions = StudyPlanGenerator.Generate(effectiveStart, plan.EndDate, slots, subjectInfo);
            foreach (var s in sessions)
            {
                s.StudyPlanId = plan.Id;
                _context.StudySessions.Add(s);
            }

            plan.GeneratedAt = DateTime.Now;
            _context.SaveChanges();
        }
    }
}
