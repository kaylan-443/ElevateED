namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAIStudyWorkspace : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AIStudyOutputs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SessionId = c.Int(nullable: false),
                        ContentType = c.String(nullable: false, maxLength: 50),
                        GeneratedContent = c.String(),
                        CreatedDate = c.DateTime(nullable: false),
                        IsFavorite = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AIStudySessions", t => t.SessionId, cascadeDelete: true)
                .Index(t => t.SessionId);
            
            AddColumn("dbo.AIStudySessions", "SessionTitle", c => c.String(maxLength: 255));
            AddColumn("dbo.AIStudySessions", "LastAccessedDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.AIStudySessions", "IsActive", c => c.Boolean(nullable: false));
            DropColumn("dbo.AIStudySessions", "GeneratedContent");
            DropColumn("dbo.AIStudySessions", "ContentType");
        }
        
        public override void Down()
        {
            AddColumn("dbo.AIStudySessions", "ContentType", c => c.String(maxLength: 50));
            AddColumn("dbo.AIStudySessions", "GeneratedContent", c => c.String());
            DropForeignKey("dbo.AIStudyOutputs", "SessionId", "dbo.AIStudySessions");
            DropIndex("dbo.AIStudyOutputs", new[] { "SessionId" });
            DropColumn("dbo.AIStudySessions", "IsActive");
            DropColumn("dbo.AIStudySessions", "LastAccessedDate");
            DropColumn("dbo.AIStudySessions", "SessionTitle");
            DropTable("dbo.AIStudyOutputs");
        }
    }
}
