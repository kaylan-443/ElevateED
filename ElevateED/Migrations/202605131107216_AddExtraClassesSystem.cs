namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddExtraClassesSystem : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.EmergencyAlerts", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TransportRoutes", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TransportRoutes", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.EmergencyAlerts", "TransportRouteId", "dbo.TransportRoutes");
            DropForeignKey("dbo.RouteTrackings", "TransportRouteId", "dbo.TransportRoutes");
            DropForeignKey("dbo.Trips", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.Trips", "TransportRouteId", "dbo.TransportRoutes");
            DropIndex("dbo.EmergencyAlerts", new[] { "TransportRouteId" });
            DropIndex("dbo.EmergencyAlerts", new[] { "DriverId" });
            DropIndex("dbo.TransportRoutes", new[] { "ExtraClassId" });
            DropIndex("dbo.TransportRoutes", new[] { "DriverId" });
            DropIndex("dbo.RouteTrackings", new[] { "TransportRouteId" });
            DropIndex("dbo.Trips", new[] { "TransportRouteId" });
            DropIndex("dbo.Trips", new[] { "DriverId" });
            CreateTable(
                "dbo.ExtraClassAIRecommendations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        RecommendedTopics = c.String(nullable: false),
                        DifficultTopics = c.String(nullable: false),
                        SuggestedTeachingOrder = c.String(),
                        EasyWinTopics = c.String(),
                        CommonMistakes = c.String(),
                        PredictedImprovement = c.Decimal(nullable: false, precision: 18, scale: 2),
                        GeneratedDate = c.DateTime(nullable: false),
                        IsApplied = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId, cascadeDelete: true)
                .Index(t => t.ExtraClassId);
            
            CreateTable(
                "dbo.ExtraClassAttendanceSessions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        SessionNumber = c.Int(nullable: false),
                        SessionDate = c.DateTime(nullable: false),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        QRCode = c.String(nullable: false, maxLength: 50),
                        QRCodeExpiry = c.DateTime(nullable: false),
                        TopicsCovered = c.String(maxLength: 500),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId, cascadeDelete: true)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.ExtraClassId)
                .Index(t => t.TeacherId);
            
            CreateTable(
                "dbo.ExtraClassAttendanceRecords",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        AttendanceSessionId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        ScanTime = c.DateTime(nullable: false),
                        Status = c.Int(nullable: false),
                        IsPresent = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClassAttendanceSessions", t => t.AttendanceSessionId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AttendanceSessionId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.ExtraClassEnrollments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        IsPaid = c.Boolean(nullable: false),
                        PaymentDate = c.DateTime(nullable: false),
                        PaymentReference = c.String(maxLength: 100),
                        EnrollmentDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.ExtraClassId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.ExtraClassFeedbacks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExtraClassId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Rating = c.Int(nullable: false),
                        Comment = c.String(maxLength: 500),
                        DateSubmitted = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExtraClasses", t => t.ExtraClassId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.ExtraClassId)
                .Index(t => t.StudentId);
            
            AddColumn("dbo.ExtraClasses", "Name", c => c.String(nullable: false, maxLength: 200));
            AddColumn("dbo.ExtraClasses", "TeacherId", c => c.Int());
            AddColumn("dbo.ExtraClasses", "StartDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.ExtraClasses", "EndDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.ExtraClasses", "Schedule", c => c.String(nullable: false));
            AddColumn("dbo.ExtraClasses", "Venue", c => c.String(maxLength: 200));
            AddColumn("dbo.ExtraClasses", "Price", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AddColumn("dbo.ExtraClasses", "Capacity", c => c.Int(nullable: false));
            AddColumn("dbo.ExtraClasses", "CurrentEnrollment", c => c.Int(nullable: false));
            AddColumn("dbo.ExtraClasses", "Status", c => c.Int(nullable: false));
            CreateIndex("dbo.ExtraClasses", "TeacherId");
            AddForeignKey("dbo.ExtraClasses", "TeacherId", "dbo.Teachers", "Id");
            DropColumn("dbo.ExtraClasses", "Title");
            DropColumn("dbo.ExtraClasses", "ClassDate");
            DropColumn("dbo.ExtraClasses", "StartTime");
            DropColumn("dbo.ExtraClasses", "EndTime");
            DropColumn("dbo.ExtraClasses", "Location");
            DropColumn("dbo.ExtraClasses", "Fee");
            DropColumn("dbo.ExtraClasses", "MaxStudents");
            DropColumn("dbo.ExtraClasses", "CreatedBy");
            DropTable("dbo.Drivers");
            DropTable("dbo.EmergencyAlerts");
            DropTable("dbo.TransportRoutes");
            DropTable("dbo.RouteTrackings");
            DropTable("dbo.Trips");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.Trips",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TransportRouteId = c.Int(nullable: false),
                        DriverId = c.Int(nullable: false),
                        TripType = c.String(),
                        Status = c.String(),
                        DestinationName = c.String(),
                        DestinationAddress = c.String(),
                        DestinationLatitude = c.Double(),
                        DestinationLongitude = c.Double(),
                        RollCallData = c.String(),
                        StartedAt = c.DateTime(),
                        EndedAt = c.DateTime(),
                        CreatedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
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
                .PrimaryKey(t => t.Id);
            
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
                .PrimaryKey(t => t.Id);
            
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
            
            AddColumn("dbo.ExtraClasses", "CreatedBy", c => c.Int(nullable: false));
            AddColumn("dbo.ExtraClasses", "MaxStudents", c => c.Int(nullable: false));
            AddColumn("dbo.ExtraClasses", "Fee", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AddColumn("dbo.ExtraClasses", "Location", c => c.String(maxLength: 100));
            AddColumn("dbo.ExtraClasses", "EndTime", c => c.Time(nullable: false, precision: 7));
            AddColumn("dbo.ExtraClasses", "StartTime", c => c.Time(nullable: false, precision: 7));
            AddColumn("dbo.ExtraClasses", "ClassDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.ExtraClasses", "Title", c => c.String(nullable: false, maxLength: 200));
            DropForeignKey("dbo.ExtraClassAIRecommendations", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.ExtraClasses", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.ExtraClassFeedbacks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClassFeedbacks", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.ExtraClassEnrollments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClassEnrollments", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.ExtraClassAttendanceSessions", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.ExtraClassAttendanceSessions", "ExtraClassId", "dbo.ExtraClasses");
            DropForeignKey("dbo.ExtraClassAttendanceRecords", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ExtraClassAttendanceRecords", "AttendanceSessionId", "dbo.ExtraClassAttendanceSessions");
            DropIndex("dbo.ExtraClassFeedbacks", new[] { "StudentId" });
            DropIndex("dbo.ExtraClassFeedbacks", new[] { "ExtraClassId" });
            DropIndex("dbo.ExtraClassEnrollments", new[] { "StudentId" });
            DropIndex("dbo.ExtraClassEnrollments", new[] { "ExtraClassId" });
            DropIndex("dbo.ExtraClassAttendanceRecords", new[] { "StudentId" });
            DropIndex("dbo.ExtraClassAttendanceRecords", new[] { "AttendanceSessionId" });
            DropIndex("dbo.ExtraClassAttendanceSessions", new[] { "TeacherId" });
            DropIndex("dbo.ExtraClassAttendanceSessions", new[] { "ExtraClassId" });
            DropIndex("dbo.ExtraClasses", new[] { "TeacherId" });
            DropIndex("dbo.ExtraClassAIRecommendations", new[] { "ExtraClassId" });
            DropColumn("dbo.ExtraClasses", "Status");
            DropColumn("dbo.ExtraClasses", "CurrentEnrollment");
            DropColumn("dbo.ExtraClasses", "Capacity");
            DropColumn("dbo.ExtraClasses", "Price");
            DropColumn("dbo.ExtraClasses", "Venue");
            DropColumn("dbo.ExtraClasses", "Schedule");
            DropColumn("dbo.ExtraClasses", "EndDate");
            DropColumn("dbo.ExtraClasses", "StartDate");
            DropColumn("dbo.ExtraClasses", "TeacherId");
            DropColumn("dbo.ExtraClasses", "Name");
            DropTable("dbo.ExtraClassFeedbacks");
            DropTable("dbo.ExtraClassEnrollments");
            DropTable("dbo.ExtraClassAttendanceRecords");
            DropTable("dbo.ExtraClassAttendanceSessions");
            DropTable("dbo.ExtraClassAIRecommendations");
            CreateIndex("dbo.Trips", "DriverId");
            CreateIndex("dbo.Trips", "TransportRouteId");
            CreateIndex("dbo.RouteTrackings", "TransportRouteId");
            CreateIndex("dbo.TransportRoutes", "DriverId");
            CreateIndex("dbo.TransportRoutes", "ExtraClassId");
            CreateIndex("dbo.EmergencyAlerts", "DriverId");
            CreateIndex("dbo.EmergencyAlerts", "TransportRouteId");
            AddForeignKey("dbo.Trips", "TransportRouteId", "dbo.TransportRoutes", "Id", cascadeDelete: true);
            AddForeignKey("dbo.Trips", "DriverId", "dbo.Drivers", "Id", cascadeDelete: true);
            AddForeignKey("dbo.RouteTrackings", "TransportRouteId", "dbo.TransportRoutes", "Id");
            AddForeignKey("dbo.EmergencyAlerts", "TransportRouteId", "dbo.TransportRoutes", "Id");
            AddForeignKey("dbo.TransportRoutes", "ExtraClassId", "dbo.ExtraClasses", "Id");
            AddForeignKey("dbo.TransportRoutes", "DriverId", "dbo.Drivers", "Id");
            AddForeignKey("dbo.EmergencyAlerts", "DriverId", "dbo.Drivers", "Id");
        }
    }
}
