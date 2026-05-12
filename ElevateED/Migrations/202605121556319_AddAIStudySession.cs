namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAIStudySession : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AIStudySessions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        OriginalFileName = c.String(maxLength: 255),
                        ExtractedText = c.String(),
                        GeneratedContent = c.String(),
                        ContentType = c.String(maxLength: 50),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.StudentId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AIStudySessions", "StudentId", "dbo.Students");
            DropIndex("dbo.AIStudySessions", new[] { "StudentId" });
            DropTable("dbo.AIStudySessions");
        }
    }
}
