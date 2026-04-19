using System.Data.Entity;

namespace ElevateED.Models
{
    public class ElevateEDContext : DbContext
    {
        public ElevateEDContext() : base("ElevateEDConnection")
        {
            Database.SetInitializer<ElevateEDContext>(null);
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

        // NEW DbSets - ADD THESE
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Stream> Streams { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<ApplicationCycle> ApplicationCycles { get; set; }
        public DbSet<TeacherSubjectQualification> TeacherSubjectQualifications { get; set; }
        public DbSet<TeacherGradeAssignment> TeacherGradeAssignments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
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

            base.OnModelCreating(modelBuilder);
        }
    }
}