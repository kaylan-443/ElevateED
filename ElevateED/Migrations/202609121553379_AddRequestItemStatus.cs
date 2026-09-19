namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRequestItemStatus : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DonationRequestItems", "Status", c => c.Int(nullable: false));
            AddColumn("dbo.DonationRequestItems", "DeclineReason", c => c.String(maxLength: 500));
            AddColumn("dbo.DonationRequestItems", "DeclinedDate", c => c.DateTime());
            AddColumn("dbo.DonationRequestItems", "DeclinedBy", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.DonationRequestItems", "DeclinedBy");
            DropColumn("dbo.DonationRequestItems", "DeclinedDate");
            DropColumn("dbo.DonationRequestItems", "DeclineReason");
            DropColumn("dbo.DonationRequestItems", "Status");
        }
    }
}
