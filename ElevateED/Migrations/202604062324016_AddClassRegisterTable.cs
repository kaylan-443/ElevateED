namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddClassRegisterTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ClassRegisters",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Grade = c.String(nullable: false),
                        ClassName = c.String(nullable: false),
                        Term = c.String(nullable: false),
                        Year = c.Int(nullable: false),
                        FilePath = c.String(nullable: false),
                        Description = c.String(),
                        UploadedBy = c.Int(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.ClassRegisters");
        }
    }
}
