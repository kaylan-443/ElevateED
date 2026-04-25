namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddExtraClasses : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ExtraClassBookings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        BookedAt = c.DateTime(nullable: false),
                        PaidAt = c.DateTime(),
                        PaymentReference = c.String(maxLength: 100),
                        AmountPaid = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.ExtraClassId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.ExtraClasses",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        GradeId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        ClassDate = c.DateTime(nullable: false),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        Location = c.String(maxLength: 100),
                        Fee = c.Decimal(nullable: false, precision: 18, scale: 2),
                        MaxStudents = c.Int(nullable: false),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Grades", t => t.GradeId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.GradeId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.Payments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BookingId = c.Int(nullable: false),
                        PaymentReference = c.String(nullable: false, maxLength: 100),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaymentDate = c.DateTime(nullable: false),
                        PaymentMethod = c.String(maxLength: 50),
                        Status = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClassBookings", t => t.BookingId, cascadeDelete: true)
                .Index(t => t.BookingId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Payments", "BookingId", "dbo.ExtraClassBookings");
            DropForeignKey("dbo.ExtraClassBookings", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClasses", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.ExtraClasses", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.ExtraClassBookings", "ExtraClassId", "dbo.ExtraClasses");
            DropIndex("dbo.Payments", new[] { "BookingId" });
            DropIndex("dbo.ExtraClasses", new[] { "SubjectId" });
            DropIndex("dbo.ExtraClasses", new[] { "GradeId" });
            DropIndex("dbo.ExtraClassBookings", new[] { "StudentId" });
            DropIndex("dbo.ExtraClassBookings", new[] { "ExtraClassId" });
            DropTable("dbo.Payments");
            DropTable("dbo.ExtraClasses");
            DropTable("dbo.ExtraClassBookings");
        }
    }
}
