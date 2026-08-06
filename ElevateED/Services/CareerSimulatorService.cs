using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ElevateED.Models;

namespace ElevateED.Services
{
    // One subject the simulator can slide, with its actual mark and any
    // saved target.
    public class SimSubject
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public decimal ActualMark { get; set; }
        public decimal? SavedTarget { get; set; }
        public bool IsLifeOrientation { get; set; }
    }

    // One requirement row, flattened to plain data. Deliberately not the raw
    // EF entity: CareerSubjectRequirement carries navigation properties back
    // to Career and Subject, and handing that graph to a JSON serialiser
    // risks a circular-reference failure (or silently dragging in unrelated
    // data) the moment lazy loading touches those navigations.
    public class SimRequirement
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int MinimumLevel { get; set; }
        public bool IsCompulsory { get; set; }
    }

    // A career on the learner's shortlist, with its requirements and its
    // verdict against the learner's real, current marks — the baseline the
    // client compares every projection against.
    public class SimCareer
    {
        public int CareerId { get; set; }
        public string CareerName { get; set; }
        public int MinimumAps { get; set; }
        public List<SimRequirement> Requirements { get; set; }
        public CareerMatchVerdict CurrentVerdict { get; set; }

        public SimCareer() { Requirements = new List<SimRequirement>(); }
    }

    // Everything the client needs to run the simulation itself: the learner's
    // subjects, the official mark bands (so the client never hardcodes them),
    // and the shortlisted careers to react against.
    public class SimulatorPayload
    {
        public bool HasReportCard { get; set; }
        public int CurrentAps { get; set; }
        public List<SimSubject> Subjects { get; set; }
        public List<AchievementBand> Bands { get; set; }
        public List<SimCareer> Careers { get; set; }

        public SimulatorPayload()
        {
            Subjects = new List<SimSubject>();
            Bands = new List<AchievementBand>();
            Careers = new List<SimCareer>();
        }
    }

    // Result of recomputing one career against a projection — used by both
    // the authoritative server recompute and to build the client's baseline.
    public class ProjectedCareerResult
    {
        public int CareerId { get; set; }
        public string CareerName { get; set; }
        public CareerMatchVerdict Verdict { get; set; }
        public List<RequirementGap> Gaps { get; set; }

        public ProjectedCareerResult() { Gaps = new List<RequirementGap>(); }
    }

    // The full authoritative result of one recompute: the APS the projection
    // produces, plus every bookmarked career's verdict against it.
    public class SimulationResult
    {
        public int Aps { get; set; }
        public List<ProjectedCareerResult> Careers { get; set; }

        public SimulationResult() { Careers = new List<ProjectedCareerResult>(); }
    }

    // Builds the simulator's data and performs the authoritative recompute.
    // The client mirrors this same arithmetic for instant feedback, but
    // anything actually saved is verified here — the client's numbers are a
    // preview, never the record of truth.
    public class CareerSimulatorService
    {
        private readonly ElevateEDContext _context;
        private readonly CareerGuidanceService _careerService;

        public CareerSimulatorService(ElevateEDContext context)
        {
            _context = context;
            _careerService = new CareerGuidanceService(context);
        }

        public SimulatorPayload BuildPayload(Student student)
        {
            var payload = new SimulatorPayload { Bands = ApsCalculator.Bands.ToList() };

            var report = _context.StudentReportCards
                .Include(r => r.Subjects.Select(s => s.Subject))
                .Where(r => r.StudentId == student.Id && r.IsPublished)
                .OrderByDescending(r => r.AcademicYear)
                .ThenByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            if (report == null)
            {
                payload.HasReportCard = false;
                return payload;
            }
            payload.HasReportCard = true;

            var targets = _context.StudentMarkTargets
                .Where(t => t.StudentId == student.Id)
                .ToDictionary(t => t.SubjectId, t => t.TargetMark);

            var baselineLevels = new List<SubjectLevel>();
            foreach (var rs in report.Subjects)
            {
                var name = rs.Subject?.Name ?? "Unknown";
                var isLo = ApsCalculator.IsLifeOrientation(name);

                payload.Subjects.Add(new SimSubject
                {
                    SubjectId = rs.SubjectId,
                    SubjectName = name,
                    ActualMark = rs.FinalMark,
                    IsLifeOrientation = isLo,
                    SavedTarget = targets.ContainsKey(rs.SubjectId) ? targets[rs.SubjectId] : (decimal?)null
                });

                baselineLevels.Add(new SubjectLevel
                {
                    SubjectId = rs.SubjectId,
                    SubjectName = name,
                    FinalMark = rs.FinalMark,
                    Level = ApsCalculator.MarkToLevel(rs.FinalMark),
                    IsLifeOrientation = isLo
                });
            }

            payload.CurrentAps = ApsCalculator.CalculateAps(baselineLevels);

            var levelBySubject = baselineLevels
                .GroupBy(s => s.SubjectId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.Level));
            var baselineAps = new ApsResult
            {
                HasReportCard = true,
                Aps = payload.CurrentAps,
                LevelBySubjectId = levelBySubject
            };

            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();

            var careers = _context.Careers
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => bookmarkedIds.Contains(c.Id) && c.IsActive)
                .ToList();

            foreach (var c in careers)
            {
                var match = _careerService.EvaluateCareer(c, baselineAps);
                payload.Careers.Add(new SimCareer
                {
                    CareerId = c.Id,
                    CareerName = c.Name,
                    MinimumAps = c.MinimumAps,
                    Requirements = c.SubjectRequirements.Select(r => new SimRequirement
                    {
                        SubjectId = r.SubjectId,
                        SubjectName = r.Subject?.Name ?? "Subject",
                        MinimumLevel = r.MinimumLevel,
                        IsCompulsory = r.IsCompulsory
                    }).ToList(),
                    CurrentVerdict = match.Verdict
                });
            }

            return payload;
        }

        // The authoritative recompute: given a projected mark per subject
        // (subjects the learner does not take are simply absent), rebuild the
        // learner's APS and re-evaluate every bookmarked career against it.
        // This reuses ApsCalculator and EvaluateCareer exactly as the real
        // Career Guidance pages do — no verdict logic is duplicated here.
        public SimulationResult Recompute(Student student, Dictionary<int, decimal> projectedMarks)
        {
            var subjectNames = _context.Subjects
                .Where(s => projectedMarks.Keys.Contains(s.Id))
                .ToDictionary(s => s.Id, s => s.Name);

            // Callers must supply the current value of every subject's slider,
            // not just the one that moved — this recomputes APS from the
            // complete projected set, exactly as the client-side preview does.
            var levels = projectedMarks.Select(kv => new SubjectLevel
            {
                SubjectId = kv.Key,
                SubjectName = subjectNames.ContainsKey(kv.Key) ? subjectNames[kv.Key] : "Unknown",
                FinalMark = kv.Value,
                Level = ApsCalculator.MarkToLevel(kv.Value),
                IsLifeOrientation = subjectNames.ContainsKey(kv.Key) && ApsCalculator.IsLifeOrientation(subjectNames[kv.Key])
            }).ToList();

            var aps = new ApsResult
            {
                HasReportCard = true,
                Aps = ApsCalculator.CalculateAps(levels),
                LevelBySubjectId = levels
                    .GroupBy(l => l.SubjectId)
                    .ToDictionary(g => g.Key, g => g.Max(l => l.Level))
            };

            var bookmarkedIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();

            var careers = _context.Careers
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => bookmarkedIds.Contains(c.Id) && c.IsActive)
                .ToList();

            var result = new SimulationResult { Aps = aps.Aps };
            foreach (var c in careers)
            {
                var match = _careerService.EvaluateCareer(c, aps);
                result.Careers.Add(new ProjectedCareerResult
                {
                    CareerId = c.Id,
                    CareerName = c.Name,
                    Verdict = match.Verdict,
                    Gaps = match.Gaps
                });
            }
            return result;
        }

        // Upsert a target mark — replaces any previous target for the subject
        // rather than accumulating a history of past experiments.
        public void SaveTarget(int studentId, int subjectId, decimal targetMark)
        {
            var existing = _context.StudentMarkTargets
                .FirstOrDefault(t => t.StudentId == studentId && t.SubjectId == subjectId);

            if (existing != null)
            {
                existing.TargetMark = targetMark;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _context.StudentMarkTargets.Add(new StudentMarkTarget
                {
                    StudentId = studentId,
                    SubjectId = subjectId,
                    TargetMark = targetMark
                });
            }
            _context.SaveChanges();
        }
    }
}
