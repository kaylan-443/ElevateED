namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDonationSystem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BulkPledges",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        CampaignId = c.Int(nullable: false),
                        DonorName = c.String(nullable: false, maxLength: 200),
                        DonorEmail = c.String(nullable: false, maxLength: 100),
                        DonorOrganization = c.String(maxLength: 200),
                        DonorPhone = c.String(maxLength: 100),
                        Category = c.Int(nullable: false),
                        ItemType = c.Int(nullable: false),
                        ItemName = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        QuantityPledged = c.Int(nullable: false),
                        QuantityDelivered = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        ExpectedDeliveryDate = c.DateTime(nullable: false),
                        ActualDeliveryDate = c.DateTime(),
                        DeliveryNotes = c.String(),
                        TrackingReference = c.String(),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ApprovedAt = c.DateTime(),
                        ApprovedBy = c.Int(),
                        UpdatedAt = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationCampaigns", t => t.CampaignId)
                .Index(t => t.CampaignId);
            
            CreateTable(
                "dbo.DonationCampaigns",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        Category = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        TargetQuantity = c.Int(nullable: false),
                        CurrentQuantity = c.Int(nullable: false),
                        TargetGroup = c.String(maxLength: 200),
                        IsExternal = c.Boolean(nullable: false),
                        ExternalPortalUrl = c.String(),
                        CampaignImage = c.String(),
                        CreatedBy = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.DonationItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonorId = c.Int(nullable: false),
                        DonorType = c.String(nullable: false, maxLength: 50),
                        DonorName = c.String(maxLength: 200),
                        DonorEmail = c.String(maxLength: 100),
                        Category = c.Int(nullable: false),
                        ItemType = c.Int(nullable: false),
                        ItemName = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        BookTitle = c.String(maxLength: 100),
                        Subject = c.String(maxLength: 50),
                        GradeLevel = c.String(maxLength: 20),
                        ISBN = c.String(maxLength: 50),
                        ClothingSize = c.String(maxLength: 20),
                        ClothingType = c.String(maxLength: 50),
                        Gender = c.String(maxLength: 20),
                        Quantity = c.Int(nullable: false),
                        QuantityRemaining = c.Int(nullable: false),
                        AllocationType = c.Int(nullable: false),
                        TargetStudentId = c.Int(),
                        Condition = c.String(nullable: false),
                        ConditionNotes = c.String(maxLength: 500),
                        PhotoEvidence = c.String(),
                        DeliveryLocation = c.String(maxLength: 200),
                        DeliveryConfirmedBy = c.String(maxLength: 100),
                        DeliveryConfirmedDate = c.DateTime(),
                        DeliveryNotes = c.String(maxLength: 500),
                        DeliveryLatitude = c.Double(),
                        DeliveryLongitude = c.Double(),
                        Status = c.Int(nullable: false),
                        TrackingCode = c.String(),
                        DonationDate = c.DateTime(nullable: false),
                        VerificationDate = c.DateTime(),
                        VerifiedBy = c.Int(),
                        AllocationDate = c.DateTime(),
                        CollectionDate = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        CampaignId = c.Int(),
                        IsFoodItem = c.Boolean(nullable: false),
                        AiExtractedKeywords = c.String(),
                        AiSuggestedMatches = c.String(),
                        AiLastProcessed = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationCampaigns", t => t.CampaignId)
                .ForeignKey("dbo.Students", t => t.TargetStudentId)
                .Index(t => t.TargetStudentId)
                .Index(t => t.CampaignId);
            
            CreateTable(
                "dbo.DonationAllocations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonationItemId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        RequestId = c.Int(),
                        QuantityAllocated = c.Int(nullable: false),
                        QuantityCollected = c.Int(nullable: false),
                        Status = c.String(nullable: false, maxLength: 50),
                        CollectionToken = c.String(),
                        QRCode = c.String(),
                        PinCode = c.String(),
                        AllocationDate = c.DateTime(nullable: false),
                        ScheduledCollectionDate = c.DateTime(),
                        CollectionDate = c.DateTime(),
                        CollectedBy = c.Int(),
                        CollectionConfirmation = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationItems", t => t.DonationItemId)
                .ForeignKey("dbo.DonationRequests", t => t.RequestId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.DonationItemId)
                .Index(t => t.StudentId)
                .Index(t => t.RequestId);
            
            CreateTable(
                "dbo.DonationRequests",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        RequestDate = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        IsFulfilled = c.Boolean(nullable: false),
                        FulfilledDate = c.DateTime(),
                        PriorityScore = c.Int(nullable: false),
                        WaitListPosition = c.Int(nullable: false),
                        WaitlistedDate = c.DateTime(),
                        AiExtractedKeywords = c.String(),
                        AiConfidenceScore = c.Decimal(precision: 18, scale: 2),
                        AiRecommendedMatches = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.DonationRequestItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonationRequestId = c.Int(nullable: false),
                        Category = c.Int(nullable: false),
                        ItemType = c.Int(nullable: false),
                        ItemName = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        BookTitle = c.String(maxLength: 100),
                        Subject = c.String(maxLength: 50),
                        GradeLevel = c.String(maxLength: 20),
                        ISBN = c.String(maxLength: 50),
                        ClothingSize = c.String(maxLength: 20),
                        ClothingType = c.String(maxLength: 50),
                        Gender = c.String(maxLength: 20),
                        StationeryType = c.String(maxLength: 50),
                        BrandPreference = c.String(maxLength: 100),
                        FoodType = c.String(maxLength: 50),
                        DietaryRequirements = c.String(maxLength: 50),
                        ItemSubCategory = c.String(maxLength: 50),
                        SizeSpecifications = c.String(maxLength: 100),
                        QuantityRequested = c.Int(nullable: false),
                        QuantityReceived = c.Int(nullable: false),
                        Priority = c.Int(nullable: false),
                        UrgencyReason = c.String(maxLength: 500),
                        IsFulfilled = c.Boolean(nullable: false),
                        FulfilledDate = c.DateTime(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationRequests", t => t.DonationRequestId, cascadeDelete: true)
                .Index(t => t.DonationRequestId);
            
            CreateTable(
                "dbo.FoodDonationChecks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonationItemId = c.Int(nullable: false),
                        FoodType = c.String(nullable: false),
                        ExpiryDate = c.DateTime(nullable: false),
                        BatchNumber = c.String(),
                        ProductionDate = c.DateTime(),
                        StorageType = c.Int(nullable: false),
                        StorageNotes = c.String(),
                        Allergens = c.Int(nullable: false),
                        AllergenDetails = c.String(),
                        PackagingSealed = c.Boolean(nullable: false),
                        NoDamage = c.Boolean(nullable: false),
                        NoBulging = c.Boolean(nullable: false),
                        NoPestDamage = c.Boolean(nullable: false),
                        QualityGrade = c.String(),
                        ConditionNotes = c.String(),
                        AppearancePassed = c.Boolean(nullable: false),
                        SmellPassed = c.Boolean(nullable: false),
                        IsCommercialSource = c.Boolean(nullable: false),
                        ProperlyLabeled = c.Boolean(nullable: false),
                        NutritionInfoPresent = c.Boolean(nullable: false),
                        Status = c.Int(nullable: false),
                        RejectionReason = c.String(),
                        CheckedAt = c.DateTime(nullable: false),
                        CheckedBy = c.Int(),
                        CheckerName = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationItems", t => t.DonationItemId)
                .Index(t => t.DonationItemId);
            
            CreateTable(
                "dbo.DonationHistories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonationItemId = c.Int(nullable: false),
                        Action = c.String(nullable: false, maxLength: 50),
                        Description = c.String(maxLength: 500),
                        ActorId = c.Int(),
                        ActorType = c.String(maxLength: 50),
                        ActionDate = c.DateTime(nullable: false),
                        AdditionalData = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationItems", t => t.DonationItemId)
                .Index(t => t.DonationItemId);
            
            CreateTable(
                "dbo.FoodAllocationDecisionLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FoodAllocationDecisionId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        RequestItemId = c.Int(nullable: false),
                        AllocatedQuantity = c.Int(nullable: false),
                        Priority = c.String(maxLength: 50),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.FoodAllocationDecisions", t => t.FoodAllocationDecisionId, cascadeDelete: true)
                .Index(t => t.FoodAllocationDecisionId);
            
            CreateTable(
                "dbo.FoodAllocationDecisions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DonationItemId = c.Int(nullable: false),
                        DecidedAt = c.DateTime(nullable: false),
                        TotalUnitsAvailable = c.Int(nullable: false),
                        TotalUnitsAllocated = c.Int(nullable: false),
                        EligibleStudentCount = c.Int(nullable: false),
                        ExcludedStudentCount = c.Int(nullable: false),
                        ExcludedReasonSummary = c.String(maxLength: 2000),
                        DecidedBy = c.Int(nullable: false),
                        DecidedByName = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DonationItems", t => t.DonationItemId)
                .Index(t => t.DonationItemId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.FoodAllocationDecisionLines", "FoodAllocationDecisionId", "dbo.FoodAllocationDecisions");
            DropForeignKey("dbo.FoodAllocationDecisions", "DonationItemId", "dbo.DonationItems");
            DropForeignKey("dbo.DonationHistories", "DonationItemId", "dbo.DonationItems");
            DropForeignKey("dbo.BulkPledges", "CampaignId", "dbo.DonationCampaigns");
            DropForeignKey("dbo.DonationItems", "TargetStudentId", "dbo.Students");
            DropForeignKey("dbo.FoodDonationChecks", "DonationItemId", "dbo.DonationItems");
            DropForeignKey("dbo.DonationItems", "CampaignId", "dbo.DonationCampaigns");
            DropForeignKey("dbo.DonationAllocations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.DonationAllocations", "RequestId", "dbo.DonationRequests");
            DropForeignKey("dbo.DonationRequests", "StudentId", "dbo.Students");
            DropForeignKey("dbo.DonationRequestItems", "DonationRequestId", "dbo.DonationRequests");
            DropForeignKey("dbo.DonationAllocations", "DonationItemId", "dbo.DonationItems");
            DropIndex("dbo.FoodAllocationDecisions", new[] { "DonationItemId" });
            DropIndex("dbo.FoodAllocationDecisionLines", new[] { "FoodAllocationDecisionId" });
            DropIndex("dbo.DonationHistories", new[] { "DonationItemId" });
            DropIndex("dbo.FoodDonationChecks", new[] { "DonationItemId" });
            DropIndex("dbo.DonationRequestItems", new[] { "DonationRequestId" });
            DropIndex("dbo.DonationRequests", new[] { "StudentId" });
            DropIndex("dbo.DonationAllocations", new[] { "RequestId" });
            DropIndex("dbo.DonationAllocations", new[] { "StudentId" });
            DropIndex("dbo.DonationAllocations", new[] { "DonationItemId" });
            DropIndex("dbo.DonationItems", new[] { "CampaignId" });
            DropIndex("dbo.DonationItems", new[] { "TargetStudentId" });
            DropIndex("dbo.BulkPledges", new[] { "CampaignId" });
            DropTable("dbo.FoodAllocationDecisions");
            DropTable("dbo.FoodAllocationDecisionLines");
            DropTable("dbo.DonationHistories");
            DropTable("dbo.FoodDonationChecks");
            DropTable("dbo.DonationRequestItems");
            DropTable("dbo.DonationRequests");
            DropTable("dbo.DonationAllocations");
            DropTable("dbo.DonationItems");
            DropTable("dbo.DonationCampaigns");
            DropTable("dbo.BulkPledges");
        }
    }
}
