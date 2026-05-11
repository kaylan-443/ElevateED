namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddTripModel : DbMigration
    {
        public override void Up()
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
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId, cascadeDelete: true)
                .ForeignKey("dbo.TransportRoutes", t => t.TransportRouteId, cascadeDelete: true)
                .Index(t => t.TransportRouteId)
                .Index(t => t.DriverId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Trips", "TransportRouteId", "dbo.TransportRoutes");
            DropForeignKey("dbo.Trips", "DriverId", "dbo.Drivers");
            DropIndex("dbo.Trips", new[] { "DriverId" });
            DropIndex("dbo.Trips", new[] { "TransportRouteId" });
            DropTable("dbo.Trips");
        }
    }
}
