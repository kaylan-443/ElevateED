namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddPodcastHistory : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PodcastHistories",
                c => new
                {
                    PodcastHistoryId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    Title = c.String(nullable: false, maxLength: 200),
                    OriginalFileName = c.String(maxLength: 255),
                    ExtractedText = c.String(),
                    GeneratedScript = c.String(),
                    AudioUrl = c.String(),
                    Duration = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    Subject = c.String(maxLength: 100),
                    Grade = c.String(maxLength: 20),
                    Status = c.String(maxLength: 20),
                    ErrorMessage = c.String(),
                })
                .PrimaryKey(t => t.PodcastHistoryId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.PodcastHistories", "StudentId", "dbo.Students");
            DropIndex("dbo.PodcastHistories", new[] { "StudentId" });
            DropTable("dbo.PodcastHistories");
        }
    }
}