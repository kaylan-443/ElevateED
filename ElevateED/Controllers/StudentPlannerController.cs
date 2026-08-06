using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Services;
using ElevateED.ViewModels;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentPlannerController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();
        private StudyPlannerService _service;
        private ExamReadinessService _readiness;

        public StudentPlannerController()
        {
            _service = new StudyPlannerService(_context);
            _readiness = new ExamReadinessService(_context);
        }

        // ============================================
        // HOME / PROGRESS DASHBOARD
        // ============================================
        public ActionResult Index()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var plans = _context.StudyPlans
                .Where(p => p.StudentId == student.Id)
                .OrderByDescending(p => p.IsActive)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();

            var vm = new PlannerIndexViewModel { StudentName = student.FullName };

            foreach (var p in plans)
                vm.Plans.Add(BuildCard(p));

            var active = plans.FirstOrDefault(p => p.IsActive) ?? plans.FirstOrDefault();
            if (active != null)
            {
                vm.ActivePlanId = active.Id;
                vm.ActiveCard = vm.Plans.First(c => c.Id == active.Id);
                vm.Progress = BuildProgress(active.Id);
            }

            return View(vm);
        }

        // ============================================
        // SETUP WIZARD
        // ============================================
        public ActionResult Setup(int? goalCareerId)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var timetable = _service.GetActiveTimetable();
            var subjects = _service.GetCandidateSubjects(student);

            var vm = new PlannerSetupViewModel
            {
                Name = timetable != null ? "Study plan for " + timetable.Name : "My study plan",
                StartDate = DateTime.Today,
                EndDate = timetable != null ? timetable.EndDate : DateTime.Today.AddDays(28),
                TargetExamTimetableId = timetable?.Id,
                TimetableName = timetable?.Name,
                HasTimetable = timetable != null,
                Subjects = subjects,
                Availability = BuildDefaultAvailability(),
                GoalOptions = BuildGoalOptions(student, goalCareerId),
                PreselectedGoalCareerId = goalCareerId
            };

            return View(vm);
        }

        // Careers the learner can pick as a goal: their shortlist, plus whichever
        // career they arrived from (via "Plan my studies toward this").
        private List<GoalCareerOption> BuildGoalOptions(Student student, int? extraCareerId)
        {
            var careerIds = _context.StudentCareerBookmarks
                .Where(b => b.StudentId == student.Id)
                .Select(b => b.CareerId)
                .ToList();
            if (extraCareerId.HasValue && !careerIds.Contains(extraCareerId.Value))
                careerIds.Add(extraCareerId.Value);
            if (!careerIds.Any()) return new List<GoalCareerOption>();

            var careers = _context.Careers
                .Include(c => c.SubjectRequirements.Select(r => r.Subject))
                .Where(c => careerIds.Contains(c.Id) && c.IsActive)
                .ToList();

            var careerService = new CareerGuidanceService(_context);
            var aps = careerService.CalculateApsForStudent(student.Id);

            return careers.Select(c =>
            {
                var match = careerService.EvaluateCareer(c, aps);
                string summary;
                if (!aps.HasReportCard)
                    summary = "APS pending";
                else if (match.Verdict == CareerMatchVerdict.Qualifies)
                    summary = "You already qualify — keep it up";
                else if (match.Gaps.Any())
                    summary = string.Join(", ", match.Gaps
                        .Where(g => !g.NotTaken)
                        .Take(3)
                        .Select(g => g.SubjectName + " +" + (g.RequiredLevel - g.CurrentLevel) + " level" + ((g.RequiredLevel - g.CurrentLevel) > 1 ? "s" : "")));
                else
                    summary = "APS " + match.StudentAps + " of " + c.MinimumAps + " needed";
                if (string.IsNullOrEmpty(summary)) summary = "Requirements to check";

                return new GoalCareerOption { Id = c.Id, Name = c.Name, GapSummary = summary };
            })
            .OrderBy(o => o.Name)
            .ToList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PlannerSetupPost model)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            DateTime start, end;
            if (!DateTime.TryParse(model.StartDate, out start)) start = DateTime.Today;
            if (!DateTime.TryParse(model.EndDate, out end)) end = start.AddDays(28);
            if (end < start) end = start.AddDays(28);

            if (model.SubjectIds == null || !model.SubjectIds.Any())
            {
                TempData["ErrorMessage"] = "Pick at least one subject to study.";
                return RedirectToAction("Setup");
            }

            var enabledDays = (model.Availability ?? new List<DaySlotInput>())
                .Where(d => d.Enabled)
                .ToList();
            if (!enabledDays.Any())
            {
                TempData["ErrorMessage"] = "Choose at least one day and time you can study.";
                return RedirectToAction("Setup");
            }

            // Deactivate previous active plans — one active plan at a time.
            foreach (var old in _context.StudyPlans.Where(p => p.StudentId == student.Id && p.IsActive))
                old.IsActive = false;

            // Only accept a goal career that actually exists and is active.
            int? goalCareerId = null;
            if (model.GoalCareerId.HasValue && _context.Careers.Any(c => c.Id == model.GoalCareerId.Value && c.IsActive))
                goalCareerId = model.GoalCareerId;

            var plan = new StudyPlan
            {
                StudentId = student.Id,
                Name = string.IsNullOrWhiteSpace(model.Name) ? "My study plan" : model.Name.Trim(),
                StartDate = start,
                EndDate = end,
                TargetExamTimetableId = model.TargetExamTimetableId,
                GoalCareerId = goalCareerId,
                IsActive = true
            };

            foreach (var d in enabledDays)
            {
                TimeSpan s, e;
                if (!TimeSpan.TryParse(d.Start, out s)) s = new TimeSpan(16, 0, 0);
                if (!TimeSpan.TryParse(d.End, out e)) e = new TimeSpan(18, 0, 0);
                if (e <= s) continue;
                plan.AvailabilitySlots.Add(new StudyAvailabilitySlot
                {
                    DayOfWeek = (DayOfWeek)d.DayOfWeek,
                    StartTime = s,
                    EndTime = e
                });
            }

            if (!plan.AvailabilitySlots.Any())
            {
                TempData["ErrorMessage"] = "Your study times were invalid — end time must be after start time.";
                return RedirectToAction("Setup");
            }

            _context.StudyPlans.Add(plan);
            _context.SaveChanges();

            // Generate the schedule.
            var timetable = plan.TargetExamTimetableId.HasValue
                ? _context.ExamTimetables.Find(plan.TargetExamTimetableId.Value)
                : _service.GetActiveTimetable();
            var subjectInfo = _service.BuildSubjectInfo(student, model.SubjectIds, start, end, timetable, goalCareerId);
            var sessions = StudyPlanGenerator.Generate(start, end, plan.AvailabilitySlots.ToList(), subjectInfo);
            foreach (var sess in sessions)
            {
                sess.StudyPlanId = plan.Id;
                _context.StudySessions.Add(sess);
            }
            plan.GeneratedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Your study plan is ready — " + sessions.Count + " sessions scheduled.";
            return RedirectToAction("Calendar", new { id = plan.Id });
        }

        // ============================================
        // WEEKLY CALENDAR
        // ============================================
        public ActionResult Calendar(int id, string week)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var plan = _context.StudyPlans
                .FirstOrDefault(p => p.Id == id && p.StudentId == student.Id);
            if (plan == null) return HttpNotFound();

            // Determine the week to show (Monday start).
            DateTime weekStart;
            if (string.IsNullOrEmpty(week) || !DateTime.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out weekStart))
            {
                var anchor = DateTime.Today < plan.StartDate ? plan.StartDate : DateTime.Today;
                weekStart = StartOfWeek(anchor);
            }
            weekStart = StartOfWeek(weekStart);
            var weekEnd = weekStart.AddDays(6);

            var sessions = _context.StudySessions
                .Include(s => s.Subject)
                .Include(s => s.LinkedExamSession)
                .Where(s => s.StudyPlanId == plan.Id && s.SessionDate >= weekStart && s.SessionDate <= weekEnd)
                .ToList();

            // Overlay the learner's exams for the same week.
            var exams = LoadExamsForWeek(student, weekStart, weekEnd);

            var vm = new PlannerCalendarViewModel
            {
                Plan = plan,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                Progress = BuildProgress(plan.Id)
            };

            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                vm.Days.Add(new CalendarDayViewModel
                {
                    Date = date,
                    Sessions = sessions.Where(s => s.SessionDate.Date == date.Date).OrderBy(s => s.StartTime).ToList(),
                    Exams = exams.Where(e => e.ExamDate.Date == date.Date).OrderBy(e => e.StartTime).ToList()
                });
            }

            var prev = weekStart.AddDays(-7);
            var next = weekStart.AddDays(7);
            vm.PrevWeek = prev.AddDays(6) >= StartOfWeek(plan.StartDate) ? prev : (DateTime?)null;
            vm.NextWeek = next <= plan.EndDate ? next : (DateTime?)null;

            return View(vm);
        }

        // ============================================
        // SESSION STATUS ACTIONS
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSession(int sessionId, string status, string returnWeek)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var session = _context.StudySessions
                .FirstOrDefault(s => s.Id == sessionId && s.StudyPlan.StudentId == student.Id);
            if (session != null)
            {
                if (status == "completed")
                {
                    session.Status = StudySessionStatus.Completed;
                    session.CompletedAt = DateTime.Now;
                }
                else if (status == "missed")
                {
                    session.Status = StudySessionStatus.Missed;
                    session.CompletedAt = null;
                }
                else if (status == "planned")
                {
                    session.Status = StudySessionStatus.Planned;
                    session.CompletedAt = null;
                }
                _context.SaveChanges();
            }

            return RedirectToAction("Calendar", new { id = session?.StudyPlanId, week = returnWeek });
        }

        // Reschedule a missed session into the next free future study block.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reschedule(int sessionId, string returnWeek)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var session = _context.StudySessions
                .Include(s => s.StudyPlan.AvailabilitySlots)
                .FirstOrDefault(s => s.Id == sessionId && s.StudyPlan.StudentId == student.Id);
            if (session == null) return RedirectToAction("Index");

            var plan = session.StudyPlan;
            var slots = plan.AvailabilitySlots.ToList();
            var existing = _context.StudySessions
                .Where(s => s.StudyPlanId == plan.Id)
                .Select(s => new { s.SessionDate, s.StartTime, s.EndTime })
                .ToList();

            // Search forward for the first availability slot with no session
            // already overlapping it. An overlap test, not a start-time
            // equality test: the generator splits long slots into sub-blocks
            // whose start times differ from the slot's own, and those must
            // still count as occupying it.
            DateTime? foundDate = null;
            TimeSpan foundStart = TimeSpan.Zero, foundEnd = TimeSpan.Zero;
            for (var date = DateTime.Today.AddDays(1); date <= plan.EndDate; date = date.AddDays(1))
            {
                var daySlots = slots.Where(sl => sl.DayOfWeek == date.DayOfWeek).OrderBy(sl => sl.StartTime);
                foreach (var sl in daySlots)
                {
                    bool taken = existing.Any(e => e.SessionDate.Date == date.Date
                        && e.StartTime < sl.EndTime && e.EndTime > sl.StartTime);
                    if (!taken)
                    {
                        foundDate = date;
                        foundStart = sl.StartTime;
                        // Same 2-hour ceiling the generator applies when it
                        // splits long slots — a reschedule shouldn't produce
                        // a session longer than generation ever would.
                        var maxEnd = sl.StartTime + TimeSpan.FromHours(2);
                        foundEnd = sl.EndTime < maxEnd ? sl.EndTime : maxEnd;
                        break;
                    }
                }
                if (foundDate.HasValue) break;
            }

            if (foundDate.HasValue)
            {
                session.Status = StudySessionStatus.Missed; // keep the miss on record
                _context.StudySessions.Add(new StudySession
                {
                    StudyPlanId = plan.Id,
                    SubjectId = session.SubjectId,
                    SessionDate = foundDate.Value,
                    StartTime = foundStart,
                    EndTime = foundEnd,
                    Status = StudySessionStatus.Planned,
                    FocusNote = "Rescheduled: " + (session.FocusNote ?? "study session")
                });
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Rescheduled to " + foundDate.Value.ToString("ddd dd MMM") + ".";
            }
            else
            {
                TempData["ErrorMessage"] = "No free study slot left to reschedule into.";
            }

            return RedirectToAction("Calendar", new { id = plan.Id, week = returnWeek });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Regenerate(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var plan = _context.StudyPlans
                .FirstOrDefault(p => p.Id == id && p.StudentId == student.Id);
            if (plan == null) return HttpNotFound();

            // Reuse the subjects already in this plan.
            var subjectIds = _context.StudySessions
                .Where(s => s.StudyPlanId == plan.Id)
                .Select(s => s.SubjectId)
                .Distinct()
                .ToList();

            if (!subjectIds.Any())
            {
                TempData["ErrorMessage"] = "This plan has no subjects to regenerate.";
                return RedirectToAction("Calendar", new { id = plan.Id });
            }

            _service.Regenerate(plan, subjectIds);
            TempData["SuccessMessage"] = "Future sessions regenerated. Completed and missed sessions were kept.";
            return RedirectToAction("Calendar", new { id = plan.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var plan = _context.StudyPlans
                .Include(p => p.Sessions)
                .Include(p => p.AvailabilitySlots)
                .FirstOrDefault(p => p.Id == id && p.StudentId == student.Id);
            if (plan != null)
            {
                _context.StudySessions.RemoveRange(plan.Sessions);
                _context.StudyAvailabilitySlots.RemoveRange(plan.AvailabilitySlots);
                _context.StudyPlans.Remove(plan);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Study plan deleted.";
            }
            return RedirectToAction("Index");
        }

        // ============================================
        // EXAM READINESS
        // ============================================
        public ActionResult Readiness()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            SetStudentViewBag(student);

            var timetable = _service.GetActiveTimetable();
            var vm = new StudentReadinessViewModel
            {
                StudentName = student.FullName,
                HasActiveTimetable = timetable != null,
                TimetableName = timetable?.Name
            };
            if (timetable == null) return View(vm);

            var grade = _context.Grades.FirstOrDefault(g => g.Name == student.Grade);
            if (grade == null) return View(vm);

            var examService = new ExamTimetableService();
            var exams = examService
                .GetExamSessionsForStudent(timetable.Id, grade.Id, student.StreamId, student.ClassId)
                .Where(e => e.ExamDate.Date >= DateTime.Today)
                .OrderBy(e => e.ExamDate)
                .ThenBy(e => e.StartTime)
                .ToList();

            vm.Exams = exams.Select(e => _readiness.ForStudent(student, e)).ToList();
            return View(vm);
        }

        // ============================================
        // HELPERS
        // ============================================
        private StudyPlanCardViewModel BuildCard(StudyPlan p)
        {
            var counts = _context.StudySessions
                .Where(s => s.StudyPlanId == p.Id)
                .GroupBy(s => s.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToList();

            int Get(StudySessionStatus st) => counts.FirstOrDefault(c => c.Key == st)?.Count ?? 0;

            return new StudyPlanCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Completed = Get(StudySessionStatus.Completed),
                Missed = Get(StudySessionStatus.Missed),
                Planned = Get(StudySessionStatus.Planned),
                Total = counts.Sum(c => c.Count),
                GeneratedAt = p.GeneratedAt,
                IsActive = p.IsActive,
                GoalCareerName = p.GoalCareer?.Name,
                GoalCareerId = p.GoalCareerId
            };
        }

        private ProgressStats BuildProgress(int planId)
        {
            var sessions = _context.StudySessions
                .Include(s => s.Subject)
                .Where(s => s.StudyPlanId == planId)
                .ToList();

            var stats = new ProgressStats
            {
                Completed = sessions.Count(s => s.Status == StudySessionStatus.Completed),
                Missed = sessions.Count(s => s.Status == StudySessionStatus.Missed),
                Planned = sessions.Count(s => s.Status == StudySessionStatus.Planned),
                Total = sessions.Count
            };

            int decided = stats.Completed + stats.Missed;
            stats.AdherencePercent = decided > 0 ? Math.Round((double)stats.Completed / decided * 100, 0) : 0;

            stats.PlannedHours = Math.Round(sessions.Sum(s => (s.EndTime - s.StartTime).TotalHours), 1);
            stats.CompletedHours = Math.Round(sessions.Where(s => s.Status == StudySessionStatus.Completed)
                .Sum(s => (s.EndTime - s.StartTime).TotalHours), 1);

            stats.BySubject = sessions
                .GroupBy(s => s.Subject?.Name ?? "Unknown")
                .Select(g => new SubjectHours
                {
                    SubjectName = g.Key,
                    PlannedHours = Math.Round(g.Sum(s => (s.EndTime - s.StartTime).TotalHours), 1),
                    CompletedHours = Math.Round(g.Where(s => s.Status == StudySessionStatus.Completed)
                        .Sum(s => (s.EndTime - s.StartTime).TotalHours), 1)
                })
                .OrderByDescending(x => x.PlannedHours)
                .ToList();

            // Streak: consecutive days up to today with at least one completed session.
            var completedDays = new HashSet<DateTime>(sessions
                .Where(s => s.Status == StudySessionStatus.Completed)
                .Select(s => s.SessionDate.Date));
            int streak = 0;
            for (var d = DateTime.Today; ; d = d.AddDays(-1))
            {
                if (completedDays.Contains(d)) streak++;
                else break;
            }
            stats.StreakDays = streak;

            return stats;
        }

        private List<ExamSession> LoadExamsForWeek(Student student, DateTime weekStart, DateTime weekEnd)
        {
            var timetable = _service.GetActiveTimetable();
            var grade = _context.Grades.FirstOrDefault(g => g.Name == student.Grade);
            if (timetable == null || grade == null) return new List<ExamSession>();

            var svc = new ExamTimetableService();
            return svc.GetExamSessionsForStudent(timetable.Id, grade.Id, student.StreamId, student.ClassId)
                .Where(e => e.ExamDate.Date >= weekStart.Date && e.ExamDate.Date <= weekEnd.Date)
                .ToList();
        }

        private List<DayAvailabilityViewModel> BuildDefaultAvailability()
        {
            var names = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            var list = new List<DayAvailabilityViewModel>();
            // Order Monday-first for display.
            int[] order = { 1, 2, 3, 4, 5, 6, 0 };
            foreach (var dow in order)
            {
                bool weekday = dow >= 1 && dow <= 5;
                list.Add(new DayAvailabilityViewModel
                {
                    DayOfWeek = dow,
                    DayName = names[dow],
                    Enabled = weekday,
                    Start = weekday ? "16:00" : "09:00",
                    End = weekday ? "18:00" : "11:00"
                });
            }
            return list;
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        private void SetStudentViewBag(Student student)
        {
            ViewBag.ActivePage = "planner";
            ViewBag.StudentFirstName = student.FirstName;
            ViewBag.StudentFullName = student.FullName;
            ViewBag.StudentNumber = User.Identity.Name;
        }

        private Student GetCurrentStudent()
        {
            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return null;

            return _context.Students
                .Include(s => s.Class)
                .Include(s => s.Class.Grade)
                .FirstOrDefault(s => s.UserId == user.Id);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}
