using System.Data.Entity;

namespace ElevateED.Models
{
    public class ElevateEDContext : DbContext
    {
        public ElevateEDContext() : base("ElevateEDConnection")
        {
            Database.SetInitializer<ElevateEDContext>(null);
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
        public DbSet<ExtraClass> ExtraClasses { get; set; }
        public DbSet<ExtraClassBooking> ExtraClassBookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<TransportRoute> TransportRoutes { get; set; }
        public DbSet<RouteTracking> RouteTrackings { get; set; }
        public DbSet<EmergencyAlert> EmergencyAlerts { get; set; }
        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<HomeworkSubmission> HomeworkSubmissions { get; set; }
        public DbSet<ClassworkSubmission> ClassworkSubmissions { get; set; }

        // Exam Timetable
        public DbSet<ExamTimetable> ExamTimetables { get; set; }
        public DbSet<ExamSession> ExamSessions { get; set; }
        public DbSet<TeacherExamNotification> TeacherExamNotifications { get; set; }
        public DbSet<Trip> Trips { get; set; }

        // Math Solver (from friend)
        public DbSet<MathSolverHistory> MathSolverHistory { get; set; }

        // Announcement System (AI-Powered)
        public DbSet<AnnouncementTemplate> AnnouncementTemplates { get; set; }
        public DbSet<AnnouncementGeneratorSession> AnnouncementGeneratorSessions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Extra Classes configurations
            modelBuilder.Configurations.Add(new ExtraClassConfiguration());
            modelBuilder.Configurations.Add(new ExtraClassBookingConfiguration());
            modelBuilder.Configurations.Add(new PaymentConfiguration());
            modelBuilder.Configurations.Add(new TransportRouteConfiguration());
            modelBuilder.Configurations.Add(new RouteTrackingConfiguration());
            modelBuilder.Configurations.Add(new EmergencyAlertConfiguration());

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

            base.OnModelCreating(modelBuilder);
        }
    }
}