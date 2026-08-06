# ElevateED Workflow Notes

## Priority Workstreams

### 1. Exam Timetable

Current issue:
- The existing exam timetable flow is too admin-driven.
- Teachers currently only submit exam durations.
- Exam sessions are linked mainly to a grade and subject, which is not enough for real school scheduling.

Improved flow:
- Admin creates an exam cycle, for example "June Exams 2026".
- Teachers propose exam sessions from their own calendar.
- Each proposed exam includes subject, paper number, date, start time, duration, venue, notes, and target classes.
- Admin reviews proposed sessions, resolves clashes, approves them, and publishes the timetable.
- Students view only the timetable for their class or grade.
- Teachers view their own exam calendar.
- Admin views the full school exam calendar.

Important scheduling rule:
- One exam session may apply to multiple classes at the same time.
- Example: Grade 12A and Grade 12B may both write Mathematics Paper 1 at the same time because the school uses one examiner or one common paper.
- The system must therefore support one exam event connected to multiple classes.

Basic clash checks:
- A class should not have two exams at the same date and time.
- A venue should not be double-booked.
- A teacher/examiner should not be assigned to two duties at the same time.

### 2. Analytics Page

Current issue:
- The analytics page is mainly attendance-focused.

Improved direction:
- Keep attendance analytics.
- Add academic analytics once exam marks are captured.
- Show class averages, subject averages, pass rates, at-risk learners, and top performers.
- Allow admin to view whole-school analytics.
- Allow teachers to view analytics for their own subjects and classes.
- Add a separate Principal Analytics page with whole-school academic power.
- Principal analytics should use the new report card and exam timetable data.
- Principal should see pass rates, promotion/progression rates, not-promoted learners, subject performance, class performance, teacher performance, pending mark approvals, report card totals, and exam timetable activity.
- Teacher dashboard should show teacher-specific academic data: draft marks, submitted marks, approved marks, returned marks, average learner mark, learner pass rate, upcoming exams, and subject/class performance.
- Existing Analytics page now combines attendance with academic marks/report summaries so it no longer reads as attendance-only.

### 3. Exam Marks and Report Cards

Goal:
- Teachers capture marks per assessment, not only final exam marks.
- Assessment types include class tests, assignments, projects, practicals, and exams.
- Teachers submit captured marks for approval.
- Admin approves submitted marks.
- Once marks are approved, the system automatically regenerates report cards for that class, term, and year.

Report card calculations:
- Term mark from approved non-exam assessments.
- Exam mark from approved exam assessments.
- Final mark from term mark and exam mark.
- Pass/fail status.
- Class average.
- Highest mark.
- Lowest mark.
- Learner performance trend compared with the previous report.
- Report card improvement note.

Current implementation note:
- A first working version exists under the Marks area.
- Teacher: create assessment, capture marks, submit for approval.
- Admin: review submitted learner marks, approve or return them, and view generated report cards.
- Student: view own generated report cards.
- Report cards now include subject-by-subject results instead of only one blended mark.
- Overall final mark is calculated from the learner's subject final marks.
- Principal can configure promotion rules for Promoted, Progressed, and Not Promoted.
- Promotion rules are set per grade.
- Each grade rule can include subjects learners are not allowed to fail, such as Home Language.
- Report cards include a promotion decision.
- Report cards can be opened in a printable PDF layout and saved as PDF from the browser.
- Principal role is supported for academic approval/report review, while Admin keeps system setup responsibilities.
