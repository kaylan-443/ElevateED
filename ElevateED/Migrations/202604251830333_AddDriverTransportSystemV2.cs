namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDriverTransportSystemV2 : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.ExtraClassBookings", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.ExtraClassBookings", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClasses", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.ExtraClasses", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Payments", "BookingId", "dbo.ExtraClassBookings");
            CreateTable(
                "dbo.Drivers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(nullable: false, maxLength: 50),
                        PasswordHash = c.String(nullable: false),
                        FullName = c.String(nullable: false, maxLength: 100),
                        PhoneNumber = c.String(maxLength: 20),
                        VehicleRegistration = c.String(maxLength: 20),
                        CreatedAt = c.DateTime(nullable: false),
                        ExpiresAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedBy = c.Int(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.EmergencyAlerts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TransportRouteId = c.Int(nullable: false),
                        DriverId = c.Int(nullable: false),
                        Message = c.String(nullable: false, maxLength: 500),
                        Latitude = c.Double(nullable: false),
                        Longitude = c.Double(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        AcknowledgedAt = c.DateTime(),
                        AcknowledgedBy = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.TransportRoutes", t => t.TransportRouteId)
                .Index(t => t.TransportRouteId)
                .Index(t => t.DriverId);
            
            CreateTable(
                "dbo.TransportRoutes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        DriverId = c.Int(nullable: false),
                        StartedAt = c.DateTime(),
                        EndedAt = c.DateTime(),
                        Status = c.String(maxLength: 20),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId)
                .Index(t => t.ExtraClassId)
                .Index(t => t.DriverId);
            
            CreateTable(
                "dbo.RouteTrackings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TransportRouteId = c.Int(nullable: false),
                        Latitude = c.Double(nullable: false),
                        Longitude = c.Double(nullable: false),
                        TrackedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TransportRoutes", t => t.TransportRouteId)
                .Index(t => t.TransportRouteId);
            
            AddForeignKey("dbo.ExtraClassBookings", "ExtraClassId", "dbo.ExtraClasses", "Id");
            AddForeignKey("dbo.ExtraClassBookings", "StudentId", "dbo.Students", "Id");
            AddForeignKey("dbo.ExtraClasses", "GradeId", "dbo.Grades", "Id");
            AddForeignKey("dbo.ExtraClasses", "SubjectId", "dbo.Subjects", "Id");
            AddForeignKey("dbo.Payments", "BookingId", "dbo.ExtraClassBookings", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Payments", "BookingId", "dbo.ExtraClassBookings");
            DropForeignKey("dbo.ExtraClasses", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.ExtraClasses", "GradeId", "dbo.Grades");
            DropForeignKey("dbo.ExtraClassBookings", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClassBookings", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.RouteTrackings", "TransportRouteId", "dbo.TransportRoutes");
            DropForeignKey("dbo.EmergencyAlerts", "TransportRouteId", "dbo.TransportRoutes");
            DropForeignKey("dbo.TransportRoutes", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.TransportRoutes", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.EmergencyAlerts", "DriverId", "dbo.Drivers");
            DropIndex("dbo.RouteTrackings", new[] { "TransportRouteId" });
            DropIndex("dbo.TransportRoutes", new[] { "DriverId" });
            DropIndex("dbo.TransportRoutes", new[] { "ExtraClassId" });
            DropIndex("dbo.EmergencyAlerts", new[] { "DriverId" });
            DropIndex("dbo.EmergencyAlerts", new[] { "TransportRouteId" });
            DropTable("dbo.RouteTrackings");
            DropTable("dbo.TransportRoutes");
            DropTable("dbo.EmergencyAlerts");
            DropTable("dbo.Drivers");
            AddForeignKey("dbo.Payments", "BookingId", "dbo.ExtraClassBookings", "Id", cascadeDelete: true);
            AddForeignKey("dbo.ExtraClasses", "SubjectId", "dbo.Subjects", "Id", cascadeDelete: true);
            AddForeignKey("dbo.ExtraClasses", "GradeId", "dbo.Grades", "Id", cascadeDelete: true);
            AddForeignKey("dbo.ExtraClassBookings", "StudentId", "dbo.Students", "Id", cascadeDelete: true);
            AddForeignKey("dbo.ExtraClassBookings", "ExtraClassId", "dbo.ExtraClasses", "Id", cascadeDelete: true);
        }
    }
}
