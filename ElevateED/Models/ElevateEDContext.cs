using System.Data.Entity;

namespace ElevateED.Models
{
    public class ElevateEDContext : DbContext
    {
        public ElevateEDContext() : base("ElevateEDConnection")
        {
            this.Configuration.ProxyCreationEnabled = false;
            this.Configuration.LazyLoadingEnabled = false;
        }

        // Existing DbSets
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<TeacherSubjectAssignment> TeacherSubjectAssignments { get; set; }
        public DbSet<PastPaper> PastPapers { get; set; }
        public DbSet<TimeTable> TimeTables { get; set; }
        public DbSet<StudyMaterial> StudyMaterials { get; set; }
        public DbSet<ClassRegister> ClassRegisters { get; set; }
        public DbSet<Homework> Homeworks { get; set; }
        public DbSet<Classwork> Classworks { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Issue> Issues { get; set; }
        public DbSet<PodcastHistory> PodcastHistories { get; set; }
        public DbSet<AIStudySession> AIStudySessions { get; set; }
        public DbSet<AIStudyOutput> AIStudyOutputs { get; set; }

        // NEW DbSets
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Stream> Streams { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<ApplicationCycle> ApplicationCycles { get; set; }
        public DbSet<TeacherSubjectQualification> TeacherSubjectQualifications { get; set; }
        public DbSet<TeacherGradeAssignment> TeacherGradeAssignments { get; set; }
        public DbSet<QuizQuestion> QuizQuestions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<QuizAnswer> QuizAnswers { get; set; }

        // Extra Classes - Updated Models
        public DbSet<ExtraClass> ExtraClasses { get; set; }
        public DbSet<ExtraClassEnrollment> ExtraClassEnrollments { get; set; }
        public DbSet<ExtraClassAttendanceSession> ExtraClassAttendanceSessions { get; set; }
        public DbSet<ExtraClassAttendanceRecord> ExtraClassAttendanceRecords { get; set; }
        public DbSet<ExtraClassFeedback> ExtraClassFeedbacks { get; set; }
        public DbSet<ExtraClassAIRecommendation> ExtraClassAIRecommendations { get; set; }

        // Keep for backward compatibility (will be deprecated)
        public DbSet<ExtraClassBooking> ExtraClassBookings { get; set; }
        public DbSet<Payment> Payments { get; set; }

        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<HomeworkSubmission> HomeworkSubmissions { get; set; }
        public DbSet<ClassworkSubmission> ClassworkSubmissions { get; set; }

        // Exam Timetable
        public DbSet<ExamTimetable> ExamTimetables { get; set; }
        public DbSet<ExamSession> ExamSessions { get; set; }
        public DbSet<ExamSessionClass> ExamSessionClasses { get; set; }
        public DbSet<TeacherExamNotification> TeacherExamNotifications { get; set; }
        public DbSet<Assessment> Assessments { get; set; }
        public DbSet<AssessmentMark> AssessmentMarks { get; set; }
        public DbSet<StudentReportCard> StudentReportCards { get; set; }
        public DbSet<StudentReportCardSubject> StudentReportCardSubjects { get; set; }
        public DbSet<PromotionRule> PromotionRules { get; set; }
        public DbSet<PromotionRuleRequiredSubject> PromotionRuleRequiredSubjects { get; set; }

        // Math Solver (from friend)
        public DbSet<MathSolverHistory> MathSolverHistory { get; set; }

        // Announcement System (AI-Powered)
        public DbSet<AnnouncementTemplate> AnnouncementTemplates { get; set; }
        public DbSet<AnnouncementGeneratorSession> AnnouncementGeneratorSessions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // ============================================
            // EXTRA CLASS CONFIGURATIONS (UPDATED)
            // ============================================

            // ExtraClass configurations
            modelBuilder.Entity<ExtraClass>()
                .HasRequired(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExtraClass>()
                .HasRequired(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExtraClass>()
                .HasOptional(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .WillCascadeOnDelete(false);

            // ExtraClassEnrollment configurations
            modelBuilder.Entity<ExtraClassEnrollment>()
                .HasRequired(e => e.ExtraClass)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.ExtraClassId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExtraClassEnrollment>()
                .HasRequired(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .WillCascadeOnDelete(false);

            // ExtraClassAttendanceSession configurations
            modelBuilder.Entity<ExtraClassAttendanceSession>()
                .HasRequired(s => s.ExtraClass)
                .WithMany(c => c.AttendanceSessions)
                .HasForeignKey(s => s.ExtraClassId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExtraClassAttendanceSession>()
                .HasRequired(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .WillCascadeOnDelete(false);

            // ExtraClassAttendanceRecord configurations
            modelBuilder.Entity<ExtraClassAttendanceRecord>()
                .HasRequired(r => r.AttendanceSession)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(r => r.AttendanceSessionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExtraClassAttendanceRecord>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            // ExtraClassFeedback configurations
            modelBuilder.Entity<ExtraClassFeedback>()
                .HasRequired(f => f.ExtraClass)
                .WithMany(c => c.Feedbacks)
                .HasForeignKey(f => f.ExtraClassId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExtraClassFeedback>()
                .HasRequired(f => f.Student)
                .WithMany()
                .HasForeignKey(f => f.StudentId)
                .WillCascadeOnDelete(false);

            // ExtraClassAIRecommendation configurations
            modelBuilder.Entity<ExtraClassAIRecommendation>()
                .HasRequired(r => r.ExtraClass)
                .WithMany(c => c.AIRecommendations)
                .HasForeignKey(r => r.ExtraClassId)
                .WillCascadeOnDelete(true);

            // Keep old ExtraClassBooking configuration for backward compatibility
            modelBuilder.Entity<ExtraClassBooking>()
                .HasRequired(b => b.ExtraClass)
                .WithMany(e => e.Bookings)
                .HasForeignKey(b => b.ExtraClassId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExtraClassBooking>()
                .HasRequired(b => b.Student)
                .WithMany()
                .HasForeignKey(b => b.StudentId)
                .WillCascadeOnDelete(false);

            // Payment configuration (keep for backward compatibility)
            modelBuilder.Entity<Payment>()
                .HasRequired(p => p.Booking)
                .WithMany()
                .HasForeignKey(p => p.BookingId)
                .WillCascadeOnDelete(false);

            // Configure Many-to-Many: Grade <-> Subject (Core Subjects)
            modelBuilder.Entity<Grade>()
                .HasMany(g => g.CoreSubjects)
                .WithMany()
                .Map(m =>
                {
                    m.MapLeftKey("GradeId");
                    m.MapRightKey("SubjectId");
                    m.ToTable("GradeCoreSubjects");
                });

            // Configure Many-to-Many: Stream <-> Subject (Elective Subjects)
            modelBuilder.Entity<Stream>()
                .HasMany(s => s.ElectiveSubjects)
                .WithMany()
                .Map(m =>
                {
                    m.MapLeftKey("StreamId");
                    m.MapRightKey("SubjectId");
                    m.ToTable("StreamElectiveSubjects");
                });

            // Configure Many-to-Many: Stream <-> Subject (Technology Subjects)
            modelBuilder.Entity<Stream>()
                .HasMany(s => s.TechnologySubjects)
                .WithMany()
                .Map(m =>
                {
                    m.MapLeftKey("StreamId");
                    m.MapRightKey("SubjectId");
                    m.ToTable("StreamTechnologySubjects");
                });

            // Configure ClassTeacher relationship (one-to-one/one-to-zero)
            modelBuilder.Entity<Class>()
                .HasOptional(c => c.ClassTeacher)
                .WithMany()
                .HasForeignKey(c => c.ClassTeacherId)
                .WillCascadeOnDelete(false);

            // Apply Student configuration
            modelBuilder.Configurations.Add(new StudentConfiguration());

            // Quiz configurations
            modelBuilder.Configurations.Add(new QuizQuestionConfiguration());
            modelBuilder.Configurations.Add(new QuizAttemptConfiguration());
            modelBuilder.Configurations.Add(new QuizAnswerConfiguration());

            // Attendance configurations
            modelBuilder.Entity<AttendanceSession>()
                .HasRequired(s => s.Class)
                .WithMany()
                .HasForeignKey(s => s.ClassId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AttendanceRecord>()
                .HasRequired(r => r.AttendanceSession)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(r => r.AttendanceSessionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AttendanceRecord>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            // Math Solver configuration (from friend)
            modelBuilder.Entity<MathSolverHistory>()
                .HasRequired(h => h.Student)
                .WithMany()
                .HasForeignKey(h => h.StudentId)
                .WillCascadeOnDelete(false);

            // ============================================
            // EXAM TIMETABLE CONFIGURATIONS
            // ============================================

            modelBuilder.Entity<TeacherExamNotification>()
                .HasRequired(t => t.ExamTimetable)
                .WithMany(e => e.TeacherNotifications)
                .HasForeignKey(t => t.ExamTimetableId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<TeacherExamNotification>()
                .HasRequired(t => t.Teacher)
                .WithMany()
                .HasForeignKey(t => t.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TeacherExamNotification>()
                .HasRequired(t => t.Subject)
                .WithMany()
                .HasForeignKey(t => t.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TeacherExamNotification>()
                .HasRequired(t => t.Grade)
                .WithMany()
                .HasForeignKey(t => t.GradeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExamSession>()
                .HasRequired(e => e.ExamTimetable)
                .WithMany(t => t.ExamSessions)
                .HasForeignKey(e => e.ExamTimetableId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExamSession>()
                .HasRequired(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExamSession>()
                .HasRequired(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExamSession>()
                .HasOptional(e => e.Stream)
                .WithMany()
                .HasForeignKey(e => e.StreamId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExamSession>()
                .HasOptional(e => e.CreatedByTeacher)
                .WithMany()
                .HasForeignKey(e => e.CreatedByTeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ExamSessionClass>()
                .HasRequired(e => e.ExamSession)
                .WithMany(e => e.ExamSessionClasses)
                .HasForeignKey(e => e.ExamSessionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ExamSessionClass>()
                .HasRequired(e => e.Class)
                .WithMany(c => c.ExamSessionClasses)
                .HasForeignKey(e => e.ClassId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Class)
                .WithMany()
                .HasForeignKey(a => a.ClassId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AssessmentMark>()
                .HasRequired(m => m.Assessment)
                .WithMany(a => a.Marks)
                .HasForeignKey(m => m.AssessmentId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<AssessmentMark>()
                .HasRequired(m => m.Student)
                .WithMany()
                .HasForeignKey(m => m.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentReportCard>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentReportCard>()
                .HasRequired(r => r.Class)
                .WithMany()
                .HasForeignKey(r => r.ClassId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentReportCard>()
                .HasOptional(r => r.ClassTeacher)
                .WithMany()
                .HasForeignKey(r => r.ClassTeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentReportCardSubject>()
                .HasRequired(s => s.StudentReportCard)
                .WithMany(r => r.Subjects)
                .HasForeignKey(s => s.StudentReportCardId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<StudentReportCardSubject>()
                .HasRequired(s => s.Subject)
                .WithMany()
                .HasForeignKey(s => s.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PromotionRule>()
                .HasOptional(r => r.Grade)
                .WithMany()
                .HasForeignKey(r => r.GradeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PromotionRuleRequiredSubject>()
                .HasRequired(r => r.PromotionRule)
                .WithMany(r => r.RequiredSubjects)
                .HasForeignKey(r => r.PromotionRuleId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PromotionRuleRequiredSubject>()
                .HasRequired(r => r.Subject)
                .WithMany()
                .HasForeignKey(r => r.SubjectId)
                .WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }
    }
}