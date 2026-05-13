using ElevateED.Models;
using ElevateED.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Admin,Principal")]
    public class ExamApprovalController : Controller
    {
        private readonly ElevateEDContext _context = new ElevateEDContext();

        // ============================================================
        // LIST — review pending and recent proposals
        // ============================================================
        public ActionResult Index(int? cycleId, string statusFilter)
        {
            ViewBag.CycleOptions = _context.ExamTimetables
                .Where(t => t.IsActive && t.Status != ExamTimetableStatus.Archived)
                .OrderByDescending(t => t.AcademicYear)
                .ThenBy(t => t.Name)
                .ToList()
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.Name} ({t.AcademicYear})",
                    Selected = cycleId.HasValue && t.Id == cycleId.Value
                })
                .ToList();
            ViewBag.CycleId = cycleId;
            ViewBag.StatusFilter = statusFilter ?? "Proposed";

            var query = _context.ExamSessions
                .Include(s => s.Subject)
                .Include(s => s.ExamTimetable)
                .Include(s => s.CreatedByTeacher)
                .Include(s => s.ExamSessionClasses.Select(c => c.Class))
                .Where(s => s.IsActive);

            if (cycleId.HasValue) query = query.Where(s => s.ExamTimetableId == cycleId.Value);

            switch ((statusFilter ?? "Proposed").ToLowerInvariant())
            {
                case "approved": query = query.Where(s => s.Status == ExamSessionStatus.Approved); break;
                case "rejected": query = query.Where(s => s.Status == ExamSessionStatus.Rejected); break;
                case "published": query = query.Where(s => s.Status == ExamSessionStatus.Published); break;
                case "all": break;
                default: query = query.Where(s => s.Status == ExamSessionStatus.Proposed); break;
            }

            var sessions = query
                .OrderBy(s => s.ExamDate)
                .ThenBy(s => s.StartTime)
                .ToList();

            // For clash detection: compare each session against every other active session in
            // the same cycle (regardless of current filter — clashes don't care).
            var pool = _context.ExamSessions
                .Include(s => s.ExamSessionClasses)
                .Where(s => s.IsActive && s.Status != ExamSessionStatus.Rejected)
                .ToList();

            var model = sessions
                .Select(s => new ExamProposalReviewListItemViewModel
                {
                    Id = s.Id,
                    ExamCycleName = s.ExamTimetable?.Name,
                    ExamCycleId = s.ExamTimetableId,
                    SubjectName = s.Subject?.Name,
                    PaperNumber = s.PaperNumber,
                    ExamDate = s.ExamDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    DurationHours = s.DurationHours,
                    Venue = s.Venue,
                    Invigilator = s.Invigilator,
                    Notes = s.Notes,
                    ClassNames = string.Join(", ", s.ExamSessionClasses.Select(c => c.Class?.FullName).Where(n => !string.IsNullOrEmpty(n))),
                    ProposedBy = s.CreatedByTeacher?.FullName,
                    Status = s.Status,
                    ProposedAt = s.ProposedAt,
                    Clashes = DetectClashes(s, pool)
                })
                .ToList();

            return View(model);
        }

        // ============================================================
        // APPROVE — single session
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            var session = _context.ExamSessions
                .Include(s => s.ExamSessionClasses)
                .FirstOrDefault(s => s.Id == id);
            if (session == null) return HttpNotFound();

            session.Status = ExamSessionStatus.Approved;
            session.ApprovedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Exam session approved.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // REJECT — single session, optional comment
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int id, string reason)
        {
            var session = _context.ExamSessions.Find(id);
            if (session == null) return HttpNotFound();

            session.Status = ExamSessionStatus.Rejected;
            // Reuse Notes to carry the rejection reason back to the teacher.
            if (!string.IsNullOrWhiteSpace(reason))
            {
                session.Notes = $"[Principal] {reason}\n\n" + (session.Notes ?? "");
            }
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Exam session sent back to the teacher.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // PUBLISH CYCLE — flip every Approved session in this cycle to Published
        // and mark the timetable as Distributed.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Principal")]
        public ActionResult PublishCycle(int cycleId)
        {
            var cycle = _context.ExamTimetables.Find(cycleId);
            if (cycle == null) return HttpNotFound();

            var sessions = _context.ExamSessions
                .Where(s => s.ExamTimetableId == cycleId
                    && s.IsActive
                    && s.Status == ExamSessionStatus.Approved)
                .ToList();

            foreach (var s in sessions)
            {
                s.Status = ExamSessionStatus.Published;
            }
            cycle.Status = ExamTimetableStatus.Distributed;
            cycle.DistributedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Published {sessions.Count} approved session(s) — students and teachers can now see them.";
            return RedirectToAction("Index", new { cycleId = cycleId, statusFilter = "Published" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Principal")]
        public ActionResult UnpublishSession(int id)
        {
            var session = _context.ExamSessions.Find(id);
            if (session == null) return HttpNotFound();
            session.Status = ExamSessionStatus.Approved;
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Session unpublished and returned to Approved.";
            return RedirectToAction("Index", new { statusFilter = "Approved" });
        }

        // ============================================================
        // CLASH DETECTION
        // ============================================================
        // A session "clashes" with another active, non-rejected session in the SAME exam
        // cycle when their time windows overlap on the same date AND they share at least
        // one class, the same venue, or the same invigilator.
        private List<string> DetectClashes(ExamSession candidate, List<ExamSession> pool)
        {
            var clashes = new List<string>();
            var candidateClassIds = new HashSet<int>(candidate.ExamSessionClasses.Select(c => c.ClassId));

            foreach (var other in pool)
            {
                if (other.Id == candidate.Id) continue;
                if (other.ExamDate.Date != candidate.ExamDate.Date) continue;
                if (!TimesOverlap(candidate.StartTime, candidate.EndTime, other.StartTime, other.EndTime)) continue;

                // Class clash
                var otherClassIds = new HashSet<int>(other.ExamSessionClasses.Select(c => c.ClassId));
                var sharedClasses = candidateClassIds.Intersect(otherClassIds).ToList();
                if (sharedClasses.Any())
                {
                    clashes.Add($"Class clash with session #{other.Id} on {sharedClasses.Count} class(es).");
                }

                // Venue clash
                if (!string.IsNullOrWhiteSpace(candidate.Venue)
                    && !string.IsNullOrWhiteSpace(other.Venue)
                    && string.Equals(candidate.Venue.Trim(), other.Venue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    clashes.Add($"Venue '{candidate.Venue}' double-booked with session #{other.Id}.");
                }

                // Invigilator clash
                if (!string.IsNullOrWhiteSpace(candidate.Invigilator)
                    && !string.IsNullOrWhiteSpace(other.Invigilator)
                    && string.Equals(candidate.Invigilator.Trim(), other.Invigilator.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    clashes.Add($"Invigilator '{candidate.Invigilator}' assigned to session #{other.Id} at the same time.");
                }
            }

            return clashes;
        }

        private static bool TimesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        {
            return aStart < bEnd && bStart < aEnd;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context?.Dispose();
            base.Dispose(disposing);
        }
    }
}