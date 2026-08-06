using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    // How well a learner matches a career's entry requirements.
    public enum CareerMatchVerdict
    {
        NoData = 0,        // learner has no published report card yet
        Qualifies = 1,     // APS and every compulsory subject requirement met
        Close = 2,         // takes the right subjects but short on APS or a level
        MissingSubjects = 3 // does not take one or more compulsory subjects
    }

    // One subject on the learner's latest report, with its NSC level.
    public class SubjectLevel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public decimal FinalMark { get; set; }
        public int Level { get; set; }
        public bool IsLifeOrientation { get; set; }
        public bool CountedInAps { get; set; }
    }

    // The learner's computed APS and the subject levels behind it.
    public class ApsResult
    {
        public bool HasReportCard { get; set; }
        public bool IsProvisional { get; set; } // fewer than 6 APS-eligible subjects
        public int Aps { get; set; }
        public string Term { get; set; }
        public int AcademicYear { get; set; }
        public DateTime? ReportGeneratedAt { get; set; }
        public List<SubjectLevel> Subjects { get; set; }

        // SubjectId -> achievement level, across every subject on the report.
        public Dictionary<int, int> LevelBySubjectId { get; set; }

        public ApsResult()
        {
            Subjects = new List<SubjectLevel>();
            LevelBySubjectId = new Dictionary<int, int>();
        }
    }

    // One unmet requirement, phrased for the learner.
    public class RequirementGap
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int RequiredLevel { get; set; }
        public int CurrentLevel { get; set; } // -1 when the subject is not taken
        public bool NotTaken { get; set; }
    }

    // The result of matching one career against one learner.
    public class CareerMatchResult
    {
        public Career Career { get; set; }
        public CareerMatchVerdict Verdict { get; set; }
        public int MinimumAps { get; set; }
        public int StudentAps { get; set; }
        public bool ApsMet { get; set; }
        public List<RequirementGap> Gaps { get; set; }

        public CareerMatchResult()
        {
            Gaps = new List<RequirementGap>();
        }
    }

    // One subject worth working on to move toward a career, with the raw
    // mark increase actually needed — the answer to "which gap should I
    // close first," not just "a gap exists."
    public class PrioritySubjectResult
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int CurrentLevel { get; set; } // -1 when the subject is not taken
        public int? RequiredLevel { get; set; } // null for a pure APS-lift candidate
        public int MarksNeeded { get; set; }
        public bool Blocked { get; set; } // true when no mark change can fix this
        public string Reason { get; set; }
    }

    // A career's gap subjects, split into what can be acted on (ranked by
    // least effort first) and what cannot (a compulsory subject the learner
    // was never registered for).
    public class PriorityBreakdown
    {
        public List<PrioritySubjectResult> Ranked { get; set; }
        public List<PrioritySubjectResult> Blocked { get; set; }

        public PriorityBreakdown()
        {
            Ranked = new List<PrioritySubjectResult>();
            Blocked = new List<PrioritySubjectResult>();
        }
    }

    // One career in a side-by-side comparison, paired with the learner's
    // match against it.
    public class CareerCompareEntry
    {
        public Career Career { get; set; }
        public CareerMatchResult Match { get; set; }
    }

    // One compared career's requirement for one subject row — or the absence
    // of one, marked explicitly rather than left blank, so "not required by
    // this career" is never mistaken for "requirement unknown."
    public class CompareCell
    {
        public bool Required { get; set; }
        public int MinimumLevel { get; set; }
        public bool IsCompulsory { get; set; }
        public bool Met { get; set; }
    }

    // One subject row across every compared career — the union of what any
    // of them require, so a subject only one career needs still gets a row.
    public class CompareSubjectRow
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int StudentLevel { get; set; } // -1 when the learner does not take it
        public List<CompareCell> Cells { get; set; } // same order as CareerComparison.Entries

        public CompareSubjectRow() { Cells = new List<CompareCell>(); }
    }

    // The full comparison: careers ordered closest-to-qualifying first, and
    // every subject either compares.
    public class CareerComparison
    {
        public List<CareerCompareEntry> Entries { get; set; }
        public List<CompareSubjectRow> SubjectRows { get; set; }

        public CareerComparison()
        {
            Entries = new List<CareerCompareEntry>();
            SubjectRows = new List<CompareSubjectRow>();
        }
    }

    // One mark threshold and the NSC level it earns. Exposed as data (not
    // buried in if/else) so the same bands can be handed to a client — e.g.
    // the admission-outcome simulator — instead of being retyped in JavaScript
    // and risking the two copies drifting apart.
    public class AchievementBand
    {
        public decimal MinMark { get; set; }
        public int Level { get; set; }

        public AchievementBand(decimal minMark, int level)
        {
            MinMark = minMark;
            Level = level;
        }
    }

    // Pure NSC/APS calculation helpers — no database, so unit-testable.
    public static class ApsCalculator
    {
        // South African NSC achievement levels 1-7 from a percentage mark.
        // Ordered highest threshold first; the lowest band (0) always matches,
        // so this never falls through.
        public static readonly IReadOnlyList<AchievementBand> Bands = new List<AchievementBand>
        {
            new AchievementBand(80, 7),
            new AchievementBand(70, 6),
            new AchievementBand(60, 5),
            new AchievementBand(50, 4),
            new AchievementBand(40, 3),
            new AchievementBand(30, 2),
            new AchievementBand(0, 1),
        };

        public static int MarkToLevel(decimal mark)
        {
            foreach (var band in Bands)
                if (mark >= band.MinMark) return band.Level;
            return 1; // unreachable — the 0% band always matches — but keeps the method total
        }

        // Life Orientation is excluded from the APS by convention.
        public static bool IsLifeOrientation(string subjectName)
        {
            if (string.IsNullOrWhiteSpace(subjectName)) return false;
            return subjectName.IndexOf("Life Orientation", StringComparison.OrdinalIgnoreCase) >= 0
                || subjectName.IndexOf("Life Orientat", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(subjectName.Trim(), "LO", StringComparison.OrdinalIgnoreCase);
        }

        // APS = sum of the best six subject levels, excluding Life Orientation.
        public static int CalculateAps(IEnumerable<SubjectLevel> subjects)
        {
            var eligible = subjects
                .Where(s => !s.IsLifeOrientation)
                .OrderByDescending(s => s.Level)
                .Take(6)
                .ToList();

            foreach (var s in eligible) s.CountedInAps = true;
            return eligible.Sum(s => s.Level);
        }
    }

    public class CareerGuidanceService
    {
        private readonly ElevateEDContext _context;

        public CareerGuidanceService(ElevateEDContext context)
        {
            _context = context;
        }

        // Build the learner's APS from their latest published report card.
        public ApsResult CalculateApsForStudent(int studentId)
        {
            var report = _context.StudentReportCards
                .Include(r => r.Subjects.Select(s => s.Subject))
                .Where(r => r.StudentId == studentId && r.IsPublished)
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            var result = new ApsResult();
            if (report == null)
            {
                result.HasReportCard = false;
                return result;
            }

            result.HasReportCard = true;
            result.Term = report.Term;
            result.AcademicYear = report.AcademicYear;
            result.ReportGeneratedAt = report.GeneratedAt;

            foreach (var rs in report.Subjects)
            {
                var name = rs.Subject?.Name ?? "Unknown";
                var level = ApsCalculator.MarkToLevel(rs.FinalMark);
                result.Subjects.Add(new SubjectLevel
                {
                    SubjectId = rs.SubjectId,
                    SubjectName = name,
                    FinalMark = rs.FinalMark,
                    Level = level,
                    IsLifeOrientation = ApsCalculator.IsLifeOrientation(name)
                });

                // Keep the best level if the same subject appears twice.
                if (!result.LevelBySubjectId.ContainsKey(rs.SubjectId) || result.LevelBySubjectId[rs.SubjectId] < level)
                    result.LevelBySubjectId[rs.SubjectId] = level;
            }

            result.Aps = ApsCalculator.CalculateAps(result.Subjects);
            result.IsProvisional = result.Subjects.Count(s => !s.IsLifeOrientation) < 6;
            return result;
        }

        // Match a single career against a computed APS result.
        public CareerMatchResult EvaluateCareer(Career career, ApsResult aps)
        {
            var match = new CareerMatchResult
            {
                Career = career,
                MinimumAps = career.MinimumAps,
                StudentAps = aps.Aps
            };

            if (!aps.HasReportCard)
            {
                match.Verdict = CareerMatchVerdict.NoData;
                return match;
            }

            match.ApsMet = aps.Aps >= career.MinimumAps;

            bool missingSubject = false;
            foreach (var req in career.SubjectRequirements.Where(r => r.IsCompulsory))
            {
                int currentLevel;
                bool taken = aps.LevelBySubjectId.TryGetValue(req.SubjectId, out currentLevel);

                if (!taken)
                {
                    missingSubject = true;
                    match.Gaps.Add(new RequirementGap
                    {
                        SubjectId = req.SubjectId,
                        SubjectName = req.Subject?.Name ?? "Subject",
                        RequiredLevel = req.MinimumLevel,
                        CurrentLevel = -1,
                        NotTaken = true
                    });
                }
                else if (currentLevel < req.MinimumLevel)
                {
                    match.Gaps.Add(new RequirementGap
                    {
                        SubjectId = req.SubjectId,
                        SubjectName = req.Subject?.Name ?? "Subject",
                        RequiredLevel = req.MinimumLevel,
                        CurrentLevel = currentLevel,
                        NotTaken = false
                    });
                }
            }

            if (missingSubject)
                match.Verdict = CareerMatchVerdict.MissingSubjects;
            else if (match.ApsMet && !match.Gaps.Any())
                match.Verdict = CareerMatchVerdict.Qualifies;
            else
                match.Verdict = CareerMatchVerdict.Close;

            return match;
        }

        // For a career the learner does not yet qualify for, rank the gap
        // subjects by how many raw marks it would actually take to fix each
        // one — the question a learner has right after "you're not there
        // yet": which subject is worth the least effort to raise first.
        //
        // Three kinds of gap, each handled differently:
        //  - a compulsory subject the learner takes but is below the required
        //    level: effort = marks to reach that level;
        //  - a compulsory subject the learner was never registered for: no
        //    mark change fixes this, so it is set aside as Blocked rather
        //    than given a nonsensical effort figure;
        //  - if the APS itself is still short even once every subject
        //    requirement is met, the cheapest currently-counted subjects to
        //    lift by one level are offered too, since raising any of them
        //    moves the APS toward the minimum.
        public PriorityBreakdown RankPrioritySubjects(Career career, ApsResult aps, CareerMatchResult match)
        {
            var breakdown = new PriorityBreakdown();
            var covered = new HashSet<int>();

            foreach (var gap in match.Gaps.Where(g => g.NotTaken))
            {
                breakdown.Blocked.Add(new PrioritySubjectResult
                {
                    SubjectId = gap.SubjectId,
                    SubjectName = gap.SubjectName,
                    CurrentLevel = -1,
                    RequiredLevel = gap.RequiredLevel,
                    MarksNeeded = 0,
                    Blocked = true,
                    Reason = "Not one of your subjects — a mark change can't fix this"
                });
                covered.Add(gap.SubjectId);
            }

            foreach (var gap in match.Gaps.Where(g => !g.NotTaken))
            {
                var subj = aps.Subjects.FirstOrDefault(s => s.SubjectId == gap.SubjectId);
                var marksNeeded = MarksToReachLevel(subj?.FinalMark ?? 0, gap.RequiredLevel);
                breakdown.Ranked.Add(new PrioritySubjectResult
                {
                    SubjectId = gap.SubjectId,
                    SubjectName = gap.SubjectName,
                    CurrentLevel = gap.CurrentLevel,
                    RequiredLevel = gap.RequiredLevel,
                    MarksNeeded = marksNeeded,
                    Blocked = false,
                    Reason = "Needs level " + gap.RequiredLevel + " for this career"
                });
                covered.Add(gap.SubjectId);
            }

            if (!match.ApsMet)
            {
                var apsLiftCandidates = aps.Subjects
                    .Where(s => s.CountedInAps && s.Level < 7 && !covered.Contains(s.SubjectId))
                    .Select(s => new PrioritySubjectResult
                    {
                        SubjectId = s.SubjectId,
                        SubjectName = s.SubjectName,
                        CurrentLevel = s.Level,
                        RequiredLevel = s.Level + 1,
                        MarksNeeded = MarksToReachLevel(s.FinalMark, s.Level + 1),
                        Blocked = false,
                        Reason = "Raising this would lift your APS"
                    });
                breakdown.Ranked.AddRange(apsLiftCandidates);
            }

            breakdown.Ranked = breakdown.Ranked.OrderBy(r => r.MarksNeeded).ToList();
            return breakdown;
        }

        // The minimum raw mark increase to reach a given NSC level, using the
        // same band table Calculate Admission Score and the simulator use.
        private static int MarksToReachLevel(decimal currentMark, int targetLevel)
        {
            var band = ApsCalculator.Bands.FirstOrDefault(b => b.Level == targetLevel);
            if (band == null) return 0;
            var needed = band.MinMark - currentMark;
            return needed > 0 ? (int)Math.Ceiling(needed) : 0;
        }

        // Lines up 2-3 careers side by side: each one evaluated exactly as
        // Evaluate Career Eligibility already does (no new matching rules),
        // then reconciled into one subject-requirement table so a subject
        // required by only one career still gets a row, marked explicitly as
        // not required by the others rather than left blank.
        public CareerComparison BuildComparison(List<Career> careers, ApsResult aps)
        {
            var entries = careers
                .Select(c => new CareerCompareEntry { Career = c, Match = EvaluateCareer(c, aps) })
                .OrderBy(e => VerdictRank(e.Match.Verdict))
                .ThenBy(e => e.Match.Gaps.Count)
                .ThenBy(e => e.Career.Name)
                .ToList();

            var subjectIds = entries
                .SelectMany(e => e.Career.SubjectRequirements.Select(r => r.SubjectId))
                .Distinct()
                .ToList();

            var rows = new List<CompareSubjectRow>();
            foreach (var subjectId in subjectIds)
            {
                var anyRequirement = entries
                    .SelectMany(e => e.Career.SubjectRequirements)
                    .First(r => r.SubjectId == subjectId);

                int studentLevel;
                bool taken = aps.LevelBySubjectId.TryGetValue(subjectId, out studentLevel);

                var row = new CompareSubjectRow
                {
                    SubjectId = subjectId,
                    SubjectName = anyRequirement.Subject?.Name ?? "Subject",
                    StudentLevel = taken ? studentLevel : -1
                };

                foreach (var entry in entries)
                {
                    var req = entry.Career.SubjectRequirements.FirstOrDefault(r => r.SubjectId == subjectId);
                    if (req == null)
                    {
                        row.Cells.Add(new CompareCell { Required = false });
                    }
                    else
                    {
                        row.Cells.Add(new CompareCell
                        {
                            Required = true,
                            MinimumLevel = req.MinimumLevel,
                            IsCompulsory = req.IsCompulsory,
                            Met = taken && studentLevel >= req.MinimumLevel
                        });
                    }
                }
                rows.Add(row);
            }

            // Subjects at least one career requires compulsorily surface first,
            // since those are the ones actually gating a verdict.
            rows = rows
                .OrderByDescending(r => r.Cells.Any(c => c.Required && c.IsCompulsory))
                .ThenBy(r => r.SubjectName)
                .ToList();

            return new CareerComparison { Entries = entries, SubjectRows = rows };
        }

        private static int VerdictRank(CareerMatchVerdict verdict)
        {
            switch (verdict)
            {
                case CareerMatchVerdict.Qualifies: return 0;
                case CareerMatchVerdict.Close: return 1;
                case CareerMatchVerdict.MissingSubjects: return 2;
                default: return 3;
            }
        }
    }
}
