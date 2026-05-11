namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NormalizeAcademicStructure : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ApplicationCycles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 50),
                        AcademicYear = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        DeadlineDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        Grade8Limit = c.Int(),
                        Grade9Limit = c.Int(),
                        Grade10Limit = c.Int(),
                        Grade11Limit = c.Int(),
                        Grade12Limit = c.Int(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Grades",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 20),
                        Level = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Classes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 50),
                        FullName = c.String(nullable: false, maxLength: 50),
                        Capacity = c.Int(nullable: false),
                        GradeId = c.Int(nullable: false),
                        ClassTeacherId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.ClassTeacherId)
                .ForeignKey("dbo.Grades", t => t.GradeId, cascadeDelete: true)
                .Index(t => t.GradeId)
                .Index(t => t.ClassTeacherId);
            
            CreateTable(
                "dbo.TeacherGradeAssignments",
                c => new
                    {
                        TeacherId = c.Int(nullable: false),
                        GradeId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.TeacherId, t.GradeId })
                .ForeignKey("dbo.Grades", t => t.GradeId, cascadeDelete: true)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId)
                .Index(t => t.GradeId);
            
            CreateTable(
                "dbo.Subjects",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Code = c.String(maxLength: 50),
                        Category = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TeacherSubjectQualifications",
                c => new
                    {
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.TeacherId, t.SubjectId })
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.Streams",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.StreamElectiveSubjects",
                c => new
                    {
                        StreamId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.StreamId, t.SubjectId })
                .ForeignKey("dbo.Streams", t => t.StreamId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.StreamId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.StreamTechnologySubjects",
                c => new
                    {
                        StreamId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.StreamId, t.SubjectId })
                .ForeignKey("dbo.Streams", t => t.StreamId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.StreamId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.GradeCoreSubjects",
                c => new
                    {
                        GradeId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.GradeId, t.SubjectId })
                .ForeignKey("dbo.Grades", t => t.GradeId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.GradeId)
                .Index(t => t.SubjectId);
            
            AddColumn("dbo.Applicants", "ApplicationCycleId", c => c.Int());
            AddColumn("dbo.Applicants", "GradeApplyingForId", c => c.Int());
            AddColumn("dbo.Applicants", "StreamId", c => c.Int());
            AddColumn("dbo.Applicants", "GradeApplyingForName", c => c.String());
            AddColumn("dbo.Applicants", "StreamChoiceName", c => c.String());
            AddColumn("dbo.Classworks", "SubjectId", c => c.Int());
            AddColumn("dbo.Classworks", "ClassId", c => c.Int());
            AddColumn("dbo.Classworks", "SubjectName", c => c.String());
            AddColumn("dbo.Classworks", "GradeName", c => c.String());
            AddColumn("dbo.Classworks", "ClassNameValue", c => c.String());
            AddColumn("dbo.Homework", "SubjectId", c => c.Int());
            AddColumn("dbo.Homework", "ClassId", c => c.Int());
            AddColumn("dbo.Homework", "SubjectName", c => c.String());
            AddColumn("dbo.Homework", "GradeName", c => c.String());
            AddColumn("dbo.Homework", "ClassNameValue", c => c.String());
            AddColumn("dbo.Students", "ClassId", c => c.Int());
            AddColumn("dbo.Students", "GradeApplyingForId", c => c.Int());
            AddColumn("dbo.Students", "StreamId", c => c.Int());
            AddColumn("dbo.TeacherSubjectAssignments", "ClassId", c => c.Int(nullable: false));
            CreateIndex("dbo.Applicants", "ApplicationCycleId");
            CreateIndex("dbo.Applicants", "GradeApplyingForId");
            CreateIndex("dbo.Applicants", "StreamId");
            CreateIndex("dbo.TeacherSubjectAssignments", "ClassId");
            CreateIndex("dbo.TeacherSubjectAssignments", "SubjectId");
            CreateIndex("dbo.Students", "ClassId");
            CreateIndex("dbo.Students", "GradeApplyingForId");
            CreateIndex("dbo.Students", "StreamId");
            CreateIndex("dbo.Classworks", "SubjectId");
            CreateIndex("dbo.Classworks", "ClassId");
            CreateIndex("dbo.Homework", "SubjectId");
            CreateIndex("dbo.Homework", "ClassId");
            AddForeignKey("dbo.Applicants", "ApplicationCycleId", "dbo.ApplicationCycles", "Id");
            AddForeignKey("dbo.TeacherSubjectAssignments", "ClassId", "dbo.Classes", "Id", cascadeDelete: true);
            AddForeignKey("dbo.TeacherSubjectAssignments", "SubjectId", "dbo.Subjects", "Id", cascadeDelete: true);
            AddForeignKey("dbo.Students", "ClassId", "dbo.Classes", "Id");
            AddForeignKey("dbo.Students", "GradeApplyingForId", "dbo.Grades", "Id");
            AddForeignKey("dbo.Students", "StreamId", "dbo.Streams", "Id");
            AddForeignKey("dbo.Applicants", "GradeApplyingForId", "dbo.Grades", "Id");
            AddForeignKey("dbo.Applicants", "StreamId", "dbo.Streams", "Id");
            AddForeignKey("dbo.Classworks", "ClassId", "dbo.Classes", "Id");
            AddForeignKey("dbo.Classworks", "SubjectId", "dbo.Subjects", "Id");
            AddForeignKey("dbo.Homework", "ClassId", "dbo.Classes", "Id");
            AddForeignKey("dbo.Homework", "SubjectId", "dbo.Subjects", "Id");
            DropColumn("dbo.Applicants", "GradeApplyingFor");
            DropColumn("dbo.Applicants", "StreamChoice");
            DropColumn("dbo.Classworks", "Subject");
            DropColumn("dbo.Classworks", "Grade");
            DropColumn("dbo.Classworks", "ClassName");
            DropColumn("dbo.Teachers", "Subjects");
            DropColumn("dbo.Teachers", "Grades");
            DropColumn("dbo.Homework", "Subject");
            DropColumn("dbo.Homework", "Grade");
            DropColumn("dbo.Homework", "ClassName");
            DropColumn("dbo.Students", "Grade");
            DropColumn("dbo.Students", "ClassName");
            DropColumn("dbo.Students", "StreamChoice");
            DropColumn("dbo.TeacherSubjectAssignments", "Grade");
            DropColumn("dbo.TeacherSubjectAssignments", "ClassName");
            DropColumn("dbo.TeacherSubjectAssignments", "SubjectName");
        }
        
        public override void Down()
        {
            AddColumn("dbo.TeacherSubjectAssignments", "SubjectName", c => c.String());
            AddColumn("dbo.TeacherSubjectAssignments", "ClassName", c => c.String(nullable: false));
            AddColumn("dbo.TeacherSubjectAssignments", "Grade", c => c.String(nullable: false));
            AddColumn("dbo.Students", "StreamChoice", c => c.String());
            AddColumn("dbo.Students", "ClassName", c => c.String(nullable: false));
            AddColumn("dbo.Students", "Grade", c => c.String(nullable: false));
            AddColumn("dbo.Homework", "ClassName", c => c.String(nullable: false));
            AddColumn("dbo.Homework", "Grade", c => c.String(nullable: false));
            AddColumn("dbo.Homework", "Subject", c => c.String(nullable: false));
            AddColumn("dbo.Teachers", "Grades", c => c.String());
            AddColumn("dbo.Teachers", "Subjects", c => c.String());
            AddColumn("dbo.Classworks", "ClassName", c => c.String(nullable: false));
            AddColumn("dbo.Classworks", "Grade", c => c.String(nullable: false));
            AddColumn("dbo.Classworks", "Subject", c => c.String(nullable: false));
            AddColumn("dbo.Applicants", "StreamChoice", c => c.String());
            AddColumn("dbo.Applicants", "GradeApplyingFor", c => c.String(nullable: false));
            DropForeignKey("dbo.Homework", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Homework", "ClassId", "dbo.Classes");
            DropForeignKey("dbo.Classworks", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Classworks", "ClassId", "dbo.Classes");
            DropForeignKey("dbo.Applicants", "StreamId", "dbo.Streams");
            DropForeignKey("dbo.Applicants", "GradeApplyingForId", "dbo.Grades");
            DropForeignKey("dbo.GradeCoreSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.GradeCoreSubjects", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.Students", "StreamId", "dbo.Streams");
            DropForeignKey("dbo.StreamTechnologySubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StreamTechnologySubjects", "StreamId", "dbo.Streams");
            DropForeignKey("dbo.StreamElectiveSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StreamElectiveSubjects", "StreamId", "dbo.Streams");
            DropForeignKey("dbo.Students", "GradeApplyingForId", "dbo.Grades");
            DropForeignKey("dbo.Students", "ClassId", "dbo.Classes");
            DropForeignKey("dbo.Classes", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.Classes", "ClassTeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectQualifications", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectQualifications", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherSubjectAssignments", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherSubjectAssignments", "ClassId", "dbo.Classes");
            DropForeignKey("dbo.TeacherGradeAssignments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherGradeAssignments", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.Applicants", "ApplicationCycleId", "dbo.ApplicationCycles");
            DropIndex("dbo.GradeCoreSubjects", new[] { "SubjectId" });
            DropIndex("dbo.GradeCoreSubjects", new[] { "GradeId" });
            DropIndex("dbo.StreamTechnologySubjects", new[] { "SubjectId" });
            DropIndex("dbo.StreamTechnologySubjects", new[] { "StreamId" });
            DropIndex("dbo.StreamElectiveSubjects", new[] { "SubjectId" });
            DropIndex("dbo.StreamElectiveSubjects", new[] { "StreamId" });
            DropIndex("dbo.Homework", new[] { "ClassId" });
            DropIndex("dbo.Homework", new[] { "SubjectId" });
            DropIndex("dbo.Classworks", new[] { "ClassId" });
            DropIndex("dbo.Classworks", new[] { "SubjectId" });
            DropIndex("dbo.Students", new[] { "StreamId" });
            DropIndex("dbo.Students", new[] { "GradeApplyingForId" });
            DropIndex("dbo.Students", new[] { "ClassId" });
            DropIndex("dbo.TeacherSubjectQualifications", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectQualifications", new[] { "TeacherId" });
            DropIndex("dbo.TeacherSubjectAssignments", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectAssignments", new[] { "ClassId" });
            DropIndex("dbo.TeacherGradeAssignments", new[] { "GradeId" });
            DropIndex("dbo.TeacherGradeAssignments", new[] { "TeacherId" });
            DropIndex("dbo.Classes", new[] { "ClassTeacherId" });
            DropIndex("dbo.Classes", new[] { "GradeId" });
            DropIndex("dbo.Applicants", new[] { "StreamId" });
            DropIndex("dbo.Applicants", new[] { "GradeApplyingForId" });
            DropIndex("dbo.Applicants", new[] { "ApplicationCycleId" });
            DropColumn("dbo.TeacherSubjectAssignments", "ClassId");
            DropColumn("dbo.Students", "StreamId");
            DropColumn("dbo.Students", "GradeApplyingForId");
            DropColumn("dbo.Students", "ClassId");
            DropColumn("dbo.Homework", "ClassNameValue");
            DropColumn("dbo.Homework", "GradeName");
            DropColumn("dbo.Homework", "SubjectName");
            DropColumn("dbo.Homework", "ClassId");
            DropColumn("dbo.Homework", "SubjectId");
            DropColumn("dbo.Classworks", "ClassNameValue");
            DropColumn("dbo.Classworks", "GradeName");
            DropColumn("dbo.Classworks", "SubjectName");
            DropColumn("dbo.Classworks", "ClassId");
            DropColumn("dbo.Classworks", "SubjectId");
            DropColumn("dbo.Applicants", "StreamChoiceName");
            DropColumn("dbo.Applicants", "GradeApplyingForName");
            DropColumn("dbo.Applicants", "StreamId");
            DropColumn("dbo.Applicants", "GradeApplyingForId");
            DropColumn("dbo.Applicants", "ApplicationCycleId");
            DropTable("dbo.GradeCoreSubjects");
            DropTable("dbo.StreamTechnologySubjects");
            DropTable("dbo.StreamElectiveSubjects");
            DropTable("dbo.Streams");
            DropTable("dbo.TeacherSubjectQualifications");
            DropTable("dbo.Subjects");
            DropTable("dbo.TeacherGradeAssignments");
            DropTable("dbo.Classes");
            DropTable("dbo.Grades");
            DropTable("dbo.ApplicationCycles");
        }
    }
}
