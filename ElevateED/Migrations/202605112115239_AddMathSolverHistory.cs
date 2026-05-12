namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMathSolverHistory : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.MathSolverHistories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        ProblemText = c.String(nullable: false),
                        SolutionText = c.String(),
                        StepsJson = c.String(),
                        ProblemType = c.String(),
                        SolvedAt = c.DateTime(nullable: false),
                        IsFavorite = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.MathSolverHistories", "StudentId", "dbo.Students");
            DropIndex("dbo.MathSolverHistories", new[] { "StudentId" });
            DropTable("dbo.MathSolverHistories");
        }
    }
}
