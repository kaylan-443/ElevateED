namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIssueReportingSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Issues",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(nullable: false),
                        Category = c.Int(nullable: false),
                        Priority = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(),
                        ResolvedAt = c.DateTime(),
                        ResolvedBy = c.Int(),
                        ResolutionNotes = c.String(),
                        AdminResponse = c.String(),
                        IsAnonymous = c.Boolean(nullable: false),
                        AttachmentsPath = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.StudentId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Issues", "StudentId", "dbo.Students");
            DropIndex("dbo.Issues", new[] { "StudentId" });
            DropTable("dbo.Issues");
        }
    }
}
