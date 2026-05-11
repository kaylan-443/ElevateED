namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddQuizSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.QuizAnswers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        QuizAttemptId = c.Int(nullable: false),
                        QuestionId = c.Int(nullable: false),
                        SelectedAnswer = c.String(nullable: false, maxLength: 1),
                        IsCorrect = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.QuizQuestions", t => t.QuestionId)
                .ForeignKey("dbo.QuizAttempts", t => t.QuizAttemptId, cascadeDelete: true)
                .Index(t => t.QuizAttemptId)
                .Index(t => t.QuestionId);
            
            CreateTable(
                "dbo.QuizQuestions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        QuestionText = c.String(nullable: false, maxLength: 500),
                        OptionA = c.String(nullable: false, maxLength: 200),
                        OptionB = c.String(nullable: false, maxLength: 200),
                        OptionC = c.String(nullable: false, maxLength: 200),
                        OptionD = c.String(nullable: false, maxLength: 200),
                        CorrectAnswer = c.String(nullable: false, maxLength: 1),
                        SubjectId = c.Int(nullable: false),
                        GradeId = c.Int(nullable: false),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Grades", t => t.GradeId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.CreatedBy)
                .Index(t => t.SubjectId)
                .Index(t => t.GradeId)
                .Index(t => t.CreatedBy);
            
            CreateTable(
                "dbo.QuizAttempts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        TotalQuestions = c.Int(nullable: false),
                        CorrectAnswers = c.Int(nullable: false),
                        Score = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AttemptedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.QuizAnswers", "QuizAttemptId", "dbo.QuizAttempts");
            DropForeignKey("dbo.QuizAttempts", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.QuizAttempts", "StudentId", "dbo.Students");
            DropForeignKey("dbo.QuizAnswers", "QuestionId", "dbo.QuizQuestions");
            DropForeignKey("dbo.QuizQuestions", "CreatedBy", "dbo.Teachers");
            DropForeignKey("dbo.QuizQuestions", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.QuizQuestions", "GradeId", "dbo.Grades");
            DropIndex("dbo.QuizAttempts", new[] { "SubjectId" });
            DropIndex("dbo.QuizAttempts", new[] { "StudentId" });
            DropIndex("dbo.QuizQuestions", new[] { "CreatedBy" });
            DropIndex("dbo.QuizQuestions", new[] { "GradeId" });
            DropIndex("dbo.QuizQuestions", new[] { "SubjectId" });
            DropIndex("dbo.QuizAnswers", new[] { "QuestionId" });
            DropIndex("dbo.QuizAnswers", new[] { "QuizAttemptId" });
            DropTable("dbo.QuizAttempts");
            DropTable("dbo.QuizQuestions");
            DropTable("dbo.QuizAnswers");
        }
    }
}
