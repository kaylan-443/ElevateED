namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DonationRedesignFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DonationItems", "ScheduledIntakeDate", c => c.DateTime());
            AddColumn("dbo.DonationItems", "RejectionReason", c => c.String(maxLength: 500));
            AddColumn("dbo.DonationAllocations", "MatchScore", c => c.Int());
            AddColumn("dbo.DonationAllocations", "MatchReason", c => c.String(maxLength: 500));
            AddColumn("dbo.DonationAllocations", "UnclaimedDate", c => c.DateTime());
            AddColumn("dbo.DonationAllocations", "CollectionTimeWindow", c => c.String(maxLength: 50));
            AddColumn("dbo.DonationRequestItems", "PriorityBoostGranted", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.DonationRequestItems", "PriorityBoostGranted");
            DropColumn("dbo.DonationAllocations", "CollectionTimeWindow");
            DropColumn("dbo.DonationAllocations", "UnclaimedDate");
            DropColumn("dbo.DonationAllocations", "MatchReason");
            DropColumn("dbo.DonationAllocations", "MatchScore");
            DropColumn("dbo.DonationItems", "RejectionReason");
            DropColumn("dbo.DonationItems", "ScheduledIntakeDate");
        }
    }
}
