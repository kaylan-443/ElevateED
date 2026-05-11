namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAttendanceSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AttendanceRecords",
                c => new
                    {
                        AttendanceRecordId = c.Int(nullable: false, identity: true),
                        AttendanceSessionId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        IsPresent = c.Boolean(nullable: false),
                        MarkedAt = c.DateTime(),
                        IsManualOverride = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.AttendanceRecordId)
                .ForeignKey("dbo.AttendanceSessions", t => t.AttendanceSessionId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AttendanceSessionId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.AttendanceSessions",
                c => new
                    {
                        AttendanceSessionId = c.Int(nullable: false, identity: true),
                        ClassId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        SessionDate = c.DateTime(nullable: false),
                        OTPCode = c.String(nullable: false, maxLength: 6),
                        OTPExpiry = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.AttendanceSessionId)
                .ForeignKey("dbo.Classes", t => t.ClassId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.ClassId)
                .Index(t => t.TeacherId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AttendanceRecords", "StudentId", "dbo.Students");
            DropForeignKey("dbo.AttendanceRecords", "AttendanceSessionId", "dbo.AttendanceSessions");
            DropForeignKey("dbo.AttendanceSessions", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.AttendanceSessions", "ClassId", "dbo.Classes");
            DropIndex("dbo.AttendanceSessions", new[] { "TeacherId" });
            DropIndex("dbo.AttendanceSessions", new[] { "ClassId" });
            DropIndex("dbo.AttendanceRecords", new[] { "StudentId" });
            DropIndex("dbo.AttendanceRecords", new[] { "AttendanceSessionId" });
            DropTable("dbo.AttendanceSessions");
            DropTable("dbo.AttendanceRecords");
        }
    }
}
