namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddHomeworkAndClasswork : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Classworks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Instructions = c.String(nullable: false),
                        Subject = c.String(nullable: false),
                        Grade = c.String(nullable: false),
                        ClassName = c.String(nullable: false),
                        FilePath = c.String(nullable: false),
                        FileName = c.String(),
                        FileType = c.String(),
                        FileSize = c.Long(nullable: false),
                        UploadedBy = c.Int(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.UploadedBy, cascadeDelete: true)
                .Index(t => t.UploadedBy);
            
            CreateTable(
                "dbo.Homework",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Instructions = c.String(nullable: false),
                        Subject = c.String(nullable: false),
                        Grade = c.String(nullable: false),
                        ClassName = c.String(nullable: false),
                        FilePath = c.String(nullable: false),
                        FileName = c.String(),
                        FileType = c.String(),
                        FileSize = c.Long(nullable: false),
                        DueDate = c.DateTime(nullable: false),
                        UploadedBy = c.Int(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.UploadedBy, cascadeDelete: true)
                .Index(t => t.UploadedBy);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Homework", "UploadedBy", "dbo.Teachers");
            DropForeignKey("dbo.Classworks", "UploadedBy", "dbo.Teachers");
            DropIndex("dbo.Homework", new[] { "UploadedBy" });
            DropIndex("dbo.Classworks", new[] { "UploadedBy" });
            DropTable("dbo.Homework");
            DropTable("dbo.Classworks");
        }
    }
}
