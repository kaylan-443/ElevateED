namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVirtualLabSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.VirtualLabAssignments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExperimentId = c.Int(nullable: false),
                        ClassId = c.Int(nullable: false),
                        SubjectId = c.Int(),
                        DueDate = c.DateTime(nullable: false),
                        Instructions = c.String(),
                        TaskRequirements = c.String(),
                        AssignedBy = c.Int(nullable: false),
                        AssignedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Classes", t => t.ClassId)
                .ForeignKey("dbo.VirtualLabExperiments", t => t.ExperimentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.AssignedBy)
                .Index(t => t.ExperimentId)
                .Index(t => t.ClassId)
                .Index(t => t.SubjectId)
                .Index(t => t.AssignedBy);
            
            CreateTable(
                "dbo.VirtualLabExperiments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(nullable: false),
                        Instructions = c.String(nullable: false),
                        LabType = c.Int(nullable: false),
                        Difficulty = c.Int(nullable: false),
                        DurationMinutes = c.Int(nullable: false),
                        GradeLevel = c.String(nullable: false, maxLength: 20),
                        LearningObjectives = c.String(),
                        PreLabQuestions = c.String(),
                        PostLabQuestions = c.String(),
                        EquipmentList = c.String(),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.CreatedBy, cascadeDelete: true)
                .Index(t => t.CreatedBy);
            
            CreateTable(
                "dbo.VirtualLabResults",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        AssignmentId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Observations = c.String(),
                        Conclusions = c.String(),
                        Answers = c.String(),
                        ExperimentData = c.String(),
                        ScreenshotPath = c.String(),
                        Score = c.Decimal(precision: 18, scale: 2),
                        TeacherFeedback = c.String(),
                        GradedBy = c.Int(),
                        GradedAt = c.DateTime(),
                        StartedAt = c.DateTime(nullable: false),
                        CompletedAt = c.DateTime(),
                        Status = c.String(),
                        SessionId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.VirtualLabAssignments", t => t.AssignmentId)
                .ForeignKey("dbo.Teachers", t => t.GradedBy)
                .ForeignKey("dbo.VirtualLabSessions", t => t.SessionId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AssignmentId)
                .Index(t => t.StudentId)
                .Index(t => t.GradedBy)
                .Index(t => t.SessionId);
            
            CreateTable(
                "dbo.VirtualLabSessions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SessionToken = c.String(),
                        CurrentState = c.String(),
                        InteractionsLog = c.String(),
                        LastActivityAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VirtualLabAssignments", "AssignedBy", "dbo.Teachers");
            DropForeignKey("dbo.VirtualLabAssignments", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.VirtualLabResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.VirtualLabResults", "SessionId", "dbo.VirtualLabSessions");
            DropForeignKey("dbo.VirtualLabResults", "GradedBy", "dbo.Teachers");
            DropForeignKey("dbo.VirtualLabResults", "AssignmentId", "dbo.VirtualLabAssignments");
            DropForeignKey("dbo.VirtualLabAssignments", "ExperimentId", "dbo.VirtualLabExperiments");
            DropForeignKey("dbo.VirtualLabExperiments", "CreatedBy", "dbo.Teachers");
            DropForeignKey("dbo.VirtualLabAssignments", "ClassId", "dbo.Classes");
            DropIndex("dbo.VirtualLabResults", new[] { "SessionId" });
            DropIndex("dbo.VirtualLabResults", new[] { "GradedBy" });
            DropIndex("dbo.VirtualLabResults", new[] { "StudentId" });
            DropIndex("dbo.VirtualLabResults", new[] { "AssignmentId" });
            DropIndex("dbo.VirtualLabExperiments", new[] { "CreatedBy" });
            DropIndex("dbo.VirtualLabAssignments", new[] { "AssignedBy" });
            DropIndex("dbo.VirtualLabAssignments", new[] { "SubjectId" });
            DropIndex("dbo.VirtualLabAssignments", new[] { "ClassId" });
            DropIndex("dbo.VirtualLabAssignments", new[] { "ExperimentId" });
            DropTable("dbo.VirtualLabSessions");
            DropTable("dbo.VirtualLabResults");
            DropTable("dbo.VirtualLabExperiments");
            DropTable("dbo.VirtualLabAssignments");
        }
    }
}
