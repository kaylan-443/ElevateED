namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAnnouncementsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Announcements",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Content = c.String(nullable: false),
                        TargetAudience = c.String(nullable: false),
                        TargetGrade = c.String(),
                        TargetClass = c.String(),
                        AnnouncementType = c.String(),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ExpiryDate = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ApplicationUsers", t => t.CreatedBy, cascadeDelete: true)
                .Index(t => t.CreatedBy);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Announcements", "CreatedBy", "dbo.ApplicationUsers");
            DropIndex("dbo.Announcements", new[] { "CreatedBy" });
            DropTable("dbo.Announcements");
        }
    }
}
