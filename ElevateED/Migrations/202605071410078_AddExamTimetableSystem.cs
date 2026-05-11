namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddExamTimetableSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ClassworkSubmissions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ClassworkId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Content = c.String(),
                        FilePath = c.String(),
                        FileName = c.String(),
                        FileType = c.String(),
                        FileSize = c.Long(nullable: false),
                        SubmittedAt = c.DateTime(nullable: false),
                        Grade = c.Decimal(precision: 18, scale: 2),
                        TeacherFeedback = c.String(),
                        GradedBy = c.Int(),
                        GradedAt = c.DateTime(),
                        Status = c.Int(nullable: false),
                        IsLate = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Classworks", t => t.ClassworkId, cascadeDelete: true)
                .ForeignKey("dbo.Teachers", t => t.GradedBy)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.ClassworkId)
                .Index(t => t.StudentId)
                .Index(t => t.GradedBy);
            
            CreateTable(
                "dbo.HomeworkSubmissions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        HomeworkId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Content = c.String(),
                        FilePath = c.String(),
                        FileName = c.String(),
                        FileType = c.String(),
                        FileSize = c.Long(nullable: false),
                        SubmittedAt = c.DateTime(nullable: false),
                        Grade = c.Decimal(precision: 18, scale: 2),
                        TeacherFeedback = c.String(),
                        GradedBy = c.Int(),
                        GradedAt = c.DateTime(),
                        Status = c.Int(nullable: false),
                        IsLate = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.GradedBy)
                .ForeignKey("dbo.Homework", t => t.HomeworkId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.HomeworkId)
                .Index(t => t.StudentId)
                .Index(t => t.GradedBy);
            
            CreateTable(
                "dbo.ExamSessions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExamTimetableId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        GradeId = c.Int(nullable: false),
                        StreamId = c.Int(),
                        PaperNumber = c.Int(nullable: false),
                        ExamDate = c.DateTime(nullable: false),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        DurationHours = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WeekNumber = c.Int(nullable: false),
                        Venue = c.String(),
                        Invigilator = c.String(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExamTimetables", t => t.ExamTimetableId, cascadeDelete: true)
                .ForeignKey("dbo.Grades", t => t.GradeId)
                .ForeignKey("dbo.Streams", t => t.StreamId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.ExamTimetableId)
                .Index(t => t.SubjectId)
                .Index(t => t.GradeId)
                .Index(t => t.StreamId);
            
            CreateTable(
                "dbo.ExamTimetables",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        AcademicYear = c.Int(nullable: false),
                        NumberOfWeeks = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        GeneratedAt = c.DateTime(),
                        DistributedAt = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ApplicationUsers", t => t.CreatedBy, cascadeDelete: true)
                .Index(t => t.CreatedBy);
            
            CreateTable(
                "dbo.TeacherExamNotifications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExamTimetableId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        GradeId = c.Int(nullable: false),
                        HasPaper1 = c.Boolean(nullable: false),
                        Paper1Duration = c.Decimal(precision: 18, scale: 2),
                        HasPaper2 = c.Boolean(nullable: false),
                        Paper2Duration = c.Decimal(precision: 18, scale: 2),
                        HasPaper3 = c.Boolean(nullable: false),
                        Paper3Duration = c.Decimal(precision: 18, scale: 2),
                        IsSubmitted = c.Boolean(nullable: false),
                        SubmittedAt = c.DateTime(),
                        NotifiedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExamTimetables", t => t.ExamTimetableId, cascadeDelete: true)
                .ForeignKey("dbo.Grades", t => t.GradeId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.ExamTimetableId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId)
                .Index(t => t.GradeId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ExamSessions", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.ExamSessions", "StreamId", "dbo.Streams");
            DropForeignKey("dbo.ExamSessions", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.ExamSessions", "ExamTimetableId", "dbo.ExamTimetables");
            DropForeignKey("dbo.TeacherExamNotifications", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherExamNotifications", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherExamNotifications", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.TeacherExamNotifications", "ExamTimetableId", "dbo.ExamTimetables");
            DropForeignKey("dbo.ExamTimetables", "CreatedBy", "dbo.ApplicationUsers");
            DropForeignKey("dbo.HomeworkSubmissions", "StudentId", "dbo.Students");
            DropForeignKey("dbo.HomeworkSubmissions", "HomeworkId", "dbo.Homework");
            DropForeignKey("dbo.HomeworkSubmissions", "GradedBy", "dbo.Teachers");
            DropForeignKey("dbo.ClassworkSubmissions", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ClassworkSubmissions", "GradedBy", "dbo.Teachers");
            DropForeignKey("dbo.ClassworkSubmissions", "ClassworkId", "dbo.Classworks");
            DropIndex("dbo.TeacherExamNotifications", new[] { "GradeId" });
            DropIndex("dbo.TeacherExamNotifications", new[] { "SubjectId" });
            DropIndex("dbo.TeacherExamNotifications", new[] { "TeacherId" });
            DropIndex("dbo.TeacherExamNotifications", new[] { "ExamTimetableId" });
            DropIndex("dbo.ExamTimetables", new[] { "CreatedBy" });
            DropIndex("dbo.ExamSessions", new[] { "StreamId" });
            DropIndex("dbo.ExamSessions", new[] { "GradeId" });
            DropIndex("dbo.ExamSessions", new[] { "SubjectId" });
            DropIndex("dbo.ExamSessions", new[] { "ExamTimetableId" });
            DropIndex("dbo.HomeworkSubmissions", new[] { "GradedBy" });
            DropIndex("dbo.HomeworkSubmissions", new[] { "StudentId" });
            DropIndex("dbo.HomeworkSubmissions", new[] { "HomeworkId" });
            DropIndex("dbo.ClassworkSubmissions", new[] { "GradedBy" });
            DropIndex("dbo.ClassworkSubmissions", new[] { "StudentId" });
            DropIndex("dbo.ClassworkSubmissions", new[] { "ClassworkId" });
            DropTable("dbo.TeacherExamNotifications");
            DropTable("dbo.ExamTimetables");
            DropTable("dbo.ExamSessions");
            DropTable("dbo.HomeworkSubmissions");
            DropTable("dbo.ClassworkSubmissions");
        }
    }
}
