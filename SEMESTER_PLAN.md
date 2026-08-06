# ElevateED Semester Plan

Stack: ASP.NET MVC 5, EF6 code-first (migrations), ASP.NET Identity, roles: Admin, Principal, Teacher, Student, Driver.
Context: South African school (CAPS) — grades, streams, Home Language, promotion/progression rules, NSC-style marks.

## Full semester scope (7 modules)

| # | Module | Increment | Status |
|---|--------|-----------|--------|
| 1 | Exam timetable generator | 1 | Done (see WORKFLOW_NOTES.md for refinement backlog) |
| 2 | Career guidance | 2 | Planned below |
| 3 | Smart study planner | 2 | Planned below |
| 4 | Online library | 3 (suggested) | Builds on existing `PastPaper` + `StudyMaterial` models |
| 5 | AI mentor | 3 (suggested) | Needs Claude/LLM API key decision |
| 6 | Donation & online store | 4 (suggested) | Needs payment approach decision (PayFast sandbox vs pledge/EFT-reference) |
| 7 | Virtual lab | 4 (suggested) | Embed PhET simulations rather than building physics engines |

Suggested pairing logic: Library + AI Mentor together (the mentor can recommend library material); Store + Virtual Lab together (both are mostly standalone). Reorder freely if the group assigns differently.

---

## Increment 2, Module A: Career Guidance

### Core idea (the "wow" factor)
Don't build a static careers brochure. Use the data the system already has:
**compute each student's APS (Admission Point Score) from their published report cards and match it against real career/course requirements.**

APS calculation (standard SA convention):
- Convert each subject final mark to an NSC achievement level:
  80–100 → 7, 70–79 → 6, 60–69 → 5, 50–59 → 4, 40–49 → 3, 30–39 → 2, 0–29 → 1.
- APS = sum of levels of the best 6 subjects, excluding Life Orientation.
- Source: `StudentReportCardSubject.FinalMark` from the latest **published** `StudentReportCard`.

### Data model (new migration: `AddCareerGuidance`)
- `CareerField` — Id, Name (e.g. Engineering, Health Sciences, Commerce, Law, IT, Education, Arts), Description.
- `Career` — Id, CareerFieldId, Name, Description, MinimumAps, TypicalQualification (e.g. "BEng Mechanical"), WhereToStudy (free text), IsActive.
- `CareerSubjectRequirement` — Id, CareerId, SubjectId, MinimumLevel (1–7), IsCompulsory. One career → many subject requirements (e.g. Medicine: Maths ≥5, Physical Sciences ≥5, Life Sciences ≥5).
- `InterestQuestion` — Id, Text, CareerFieldId (which field a "yes/agree" points to), IsActive. Simple Likert quiz (~20 questions), RIASEC-lite: each question weighted toward a field.
- `StudentInterestResult` — Id, StudentId, TakenAt, TopField1Id, TopField2Id, TopField3Id, RawScoresJson.
- `StudentCareerBookmark` — Id, StudentId, CareerId, CreatedAt.

### Features by role
Student (`StudentCareerController`, following the StudentExtraClass/StudentQuiz naming pattern):
1. **My APS dashboard** — current APS, per-subject levels, trend vs previous term.
2. **Career explorer** — browse/filter careers by field; each career shows a match verdict:
   - ✅ Qualifies (APS + all subject requirements met)
   - ⚠️ Close (shows the gap, e.g. "Mathematics needs level 5, you are at level 4 — raise your mark to 60%")
   - ❌ Not on current subjects (missing a compulsory subject entirely)
3. **Interest quiz** — answer questions, get top 3 career fields, see careers in those fields ranked by match.
4. **Bookmarks** — save careers to "My Career Shortlist".

Admin / LO teacher (`AdminCareerController`):
- CRUD for fields, careers, subject requirements, quiz questions.
- Seed data in `Migrations/Configuration.cs`: ~8 fields, ~30 well-known SA careers with realistic APS/subject requirements, ~20 quiz questions. Seeding matters — the demo dies if the career table is empty.
- Report: distribution of student interest results per field (nice chart for markers).

### Build order
1. Models + migration + seed data.
2. APS calculation service (`Services/ApsCalculator.cs` or static helper) + unit-testable logic: mark→level, best-6-excluding-LO. Handle students with no published report card (show "APS available after your first published report").
3. Student career explorer + match logic.
4. Interest quiz.
5. Admin CRUD + interest analytics.

### Edge cases to handle
- Student has fewer than 6 subjects with marks → compute APS over what exists, flag as provisional.
- Life Orientation identification: match by subject name containing "Life Orientation" or add an `IsExcludedFromAps` flag on `Subject` (flag is cleaner).
- No published report card yet → quiz still works; explorer shows requirements without a verdict.

---

## Increment 2, Module B: Smart Study Planner

### Core idea (the "wow" factor)
The planner is "smart" because it uses three existing data sources:
1. **Exam dates** from published `ExamSession`s for the student's class/grade (Increment 1's module!).
2. **Weak subjects** from `StudentReportCardSubject.FinalMark` (lower mark → more study time).
3. **Homework due dates** from the existing Homework module.

The demo story: "the timetable module we built last increment now drives each learner's personal study schedule."

### Data model (new migration: `AddStudyPlanner`)
- `StudyPlan` — Id, StudentId, Name, StartDate, EndDate, TargetExamTimetableId (nullable FK to `ExamTimetable`), IsActive, CreatedAt, GeneratedAt.
- `StudyAvailabilitySlot` — Id, StudyPlanId, DayOfWeek, StartTime, EndTime. (The learner's weekly free-time template, e.g. Mon 18:00–20:00.)
- `StudySession` — Id, StudyPlanId, SubjectId, SessionDate, StartTime, EndTime, Status (Planned/Completed/Missed/Skipped), FocusNote (auto-filled, e.g. "Revise for Mathematics P1 on 03 Nov"), LinkedExamSessionId (nullable), CompletedAt.

### Generation algorithm (`Services/StudyPlanGenerator.cs`)
For each availability slot between plan start and end date, assign a subject by priority score:

```
priority(subject) = examUrgency × weakness
examUrgency  = 1 + max(0, (14 - daysUntilNextExam)) / 14      // ramps up in the last 2 weeks
weakness     = (100 - latestFinalMark) / 100                  // 40% learner gets 0.6, 80% learner gets 0.2
                (default 0.5 if no mark exists)
```

Rules:
- Round-robin with weighting — never schedule the same subject in more than 2 consecutive slots (variety = retention).
- The last 1–2 sessions before an exam date are locked to that exam's subject ("final revision").
- Subjects whose exams have passed drop out of the rotation automatically.
- Split slots longer than 2 hours into multiple sessions with different subjects.
- Regeneration only rewrites sessions with Status = Planned and dates in the future — completed history is preserved.

### Features
Student (`StudentPlannerController`):
1. **Setup wizard** — pick active exam timetable (or free-form date range), tick weekly availability slots, choose subjects (default: all subjects for their class), generate.
2. **Weekly calendar view** — sessions colour-coded by subject; exam days from `ExamSession` shown as fixed blocks so learners see study + exams in one view.
3. **Tick off sessions** — mark Completed / Missed; missed sessions can be one-click rescheduled into the next free slot.
4. **Progress dashboard** — adherence % (completed/planned), hours per subject vs. planned, streak counter (consecutive days with a completed session).
5. **Regenerate** — after new marks or timetable changes.

Optional (only if time allows): teacher read-only view of class-level adherence stats — do NOT let it grow into a monitoring feature this increment.

### Build order
1. Models + migration.
2. Generator service (pure logic first — testable without UI).
3. Setup wizard + generate.
4. Calendar view + complete/missed actions.
5. Progress dashboard.
6. Polish: reschedule-missed, regenerate.

### Edge cases
- No published exam timetable → planner falls back to weakness-only weighting over a chosen date range.
- No marks yet → equal weighting across subjects.
- Availability slot overlapping an exam session on the same day → skip or shorten that slot.
- Timetable republished/changed after generation → show a "timetable changed, regenerate?" banner (compare `ExamTimetable.DistributedAt` vs `StudyPlan.GeneratedAt`).

---

## Later increments — planning notes

### Online Library (builds on PastPaper + StudyMaterial)
- Unify into a `LibraryItem` concept: type (past paper / textbook PDF / notes / video link), subject, grade, year, uploader, download counter.
- Search + filter, "most downloaded", teacher upload with admin approval.
- Optional: physical book catalogue with borrow/return and due-date tracking (nice admin feature, cheap to build).

### AI Mentor
- Chat UI (student-facing) backed by the Claude API; key stored in Web.config appSettings, server-side calls only.
- The differentiator: **ground the prompt in the student's own data** — upcoming exams, weakest subjects, adherence to their study plan. "You have Maths P1 in 5 days and your last mark was 52% — want a revision checklist?"
- Guardrails: refuse to do homework verbatim, redirect to explanation; log conversations for the demo.
- Decision needed early: who pays for the API key; build a canned-response fallback mode so the demo never depends on connectivity.

### Donation & Online Store
- Catalogue (uniforms, stationery, fundraiser items), cart, order with reference number.
- Payments: recommend **pledge/EFT-reference flow** (order gets a reference, admin marks paid) OR PayFast sandbox if the markers require a real gateway. Avoid storing card data entirely.
- Donations: cause-based campaigns (e.g. "Library fund") with progress bars; donor can be anonymous/external (no login) — needs a public-facing page.

### Virtual Lab
- Don't build simulations — embed **PhET Interactive Simulations** (free, iframe-embeddable, offline-downloadable) mapped to subject + grade.
- Teacher assigns a lab (simulation + worksheet questions); student completes and submits answers via the existing submission pattern; teacher marks.
- This reuses the Homework/Submission mental model, so it is cheaper than it sounds.

---

## New feature ideas to pitch to the group (increments 3/4 fillers)

1. **Parent portal** — parents' contact details already exist on `Student`; a read-only login for report cards, attendance, announcements, and fee/donation status. High marks-per-effort ratio.
2. **Peer tutoring matchmaker** — analytics already identifies top performers and at-risk learners per subject; auto-suggest tutor/tutee pairs and book them into the existing ExtraClass module. Great "modules working together" story.
3. **Bursary & scholarship tracker** — pairs naturally with Career Guidance: bursaries with closing dates, field, minimum APS; students see bursaries they qualify for; deadline reminders.
4. **Gamification layer on the study planner** — badges (7-day streak, 100% adherence week), class leaderboard (opt-in). Cheap once the planner exists.
5. **Matric countdown dashboard** — for Grade 12s: days to finals, per-subject readiness (mark trend × planner adherence). Mostly a view over existing data.
6. **QR-code attendance** — student ID cards with QR; teacher scans to populate the existing `AttendanceSession`. Demo-friendly.

## Demo/marks strategy for Increment 2
- Lead with integration: Increment 1's exam timetable powering the study planner; the marks module powering APS.
- Seed realistic data (careers, one published timetable, marks for 2–3 demo students at different performance levels) so every screen has content.
- Show the three student personas: strong student (qualifies for Medicine), average (gap analysis shows what to improve), no-marks-yet (provisional/fallback flows work).
