namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVirtualLabGrading : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.VirtualLabExperimentTasks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExperimentId = c.Int(nullable: false),
                        TaskKey = c.String(nullable: false, maxLength: 50),
                        TaskLabel = c.String(nullable: false, maxLength: 200),
                        MaxMarks = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SortOrder = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.VirtualLabExperiments", t => t.ExperimentId)
                .Index(t => t.ExperimentId);
            
            AddColumn("dbo.VirtualLabResults", "MaxScore", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.VirtualLabResults", "AutoGraded", c => c.Boolean(nullable: false));
            AddColumn("dbo.VirtualLabResults", "TaskScoresJson", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VirtualLabExperimentTasks", "ExperimentId", "dbo.VirtualLabExperiments");
            DropIndex("dbo.VirtualLabExperimentTasks", new[] { "ExperimentId" });
            DropColumn("dbo.VirtualLabResults", "TaskScoresJson");
            DropColumn("dbo.VirtualLabResults", "AutoGraded");
            DropColumn("dbo.VirtualLabResults", "MaxScore");
            DropTable("dbo.VirtualLabExperimentTasks");
        }
    }
}
