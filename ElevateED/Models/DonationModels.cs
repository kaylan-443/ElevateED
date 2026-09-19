using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace ElevateED.Models
{
    #region Enums

    public enum DonationItemType
    {
        Book,
        Clothing,
        Stationery,
        Food,
        Other
    }

    public enum DonationCategory
    {
        Textbook,
        Clothing,
        Stationery,
        Food,
        Other
    }

    public enum DonationStatus
    {
        PendingVerification,
        PendingEligibilityCheck,
        AwaitingDelivery,
        Verified,
        Allocated,
        Collected,
        Expired,
        Rejected
    }

    public enum AllocationType
    {
        OpenDonation,
        TargetedDonation,
        NamedAllocation
    }

    public enum RequestPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }

    public enum RequestItemStatus
    {
        Pending,
        Allocated,
        Declined
    }

    public enum CampaignStatus
    {
        Draft,
        Active,
        Completed,
        Cancelled
    }

    public enum PledgeStatus
    {
        PendingApproval,
        Approved,
        PartiallyDelivered,
        FullyDelivered,
        Cancelled
    }

    public enum FoodDonationStatus
    {
        PendingEligibilityCheck,
        Approved,
        Rejected,
        Expired
    }

    public enum FoodStorageType
    {
        ShelfStable,
        Refrigerated,
        Frozen,
        DryStorage
    }

    public enum FoodAllergen
    {
        None,
        Nuts,
        Dairy,
        Gluten,
        Soy,
        Eggs,
        Shellfish,
        Peanuts,
        Sesame,
        Mustard,
        Sulphites,
        Multiple
    }

    #endregion

    #region Donation Request (Parent)

    public class DonationRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public DateTime RequestDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool IsActive { get; set; }
        public bool IsFulfilled { get; set; }
        public DateTime? FulfilledDate { get; set; }

        public int PriorityScore { get; set; }
        public int WaitListPosition { get; set; }
        public DateTime? WaitlistedDate { get; set; }

        public string AiExtractedKeywords { get; set; }
        public decimal? AiConfidenceScore { get; set; }
        public string AiRecommendedMatches { get; set; }

        public virtual ICollection<DonationRequestItem> Items { get; set; }
        public virtual ICollection<DonationAllocation> Allocations { get; set; }

        public DonationRequest()
        {
            RequestDate = DateTime.Now;
            IsActive = true;
            IsFulfilled = false;
            Items = new HashSet<DonationRequestItem>();
            Allocations = new HashSet<DonationAllocation>();
        }

        public int CalculatePriorityScore()
        {
            if (Items == null || !Items.Any()) return 10;

            int highestPriority = Items.Max(i => (int)i.Priority);
            int score = 0;

            switch ((RequestPriority)highestPriority)
            {
                case RequestPriority.Urgent: score = 50; break;
                case RequestPriority.High: score = 35; break;
                case RequestPriority.Medium: score = 20; break;
                case RequestPriority.Low: score = 10; break;
            }

            foreach (var item in Items)
            {
                if (!string.IsNullOrEmpty(item.GradeLevel))
                {
                    if (item.GradeLevel.Contains("12")) { score += 15; break; }
                    else if (item.GradeLevel.Contains("11")) { score += 12; break; }
                    else if (item.GradeLevel.Contains("10")) { score += 10; break; }
                    else if (item.GradeLevel.Contains("9")) { score += 8; break; }
                }
            }

            var daysWaiting = (DateTime.Now - RequestDate).Days;
            score += Math.Min(daysWaiting, 30);

            return score;
        }
    }

    #endregion

    #region Donation Request Item (Child)

    public class DonationRequestItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonationRequestId { get; set; }

        [ForeignKey("DonationRequestId")]
        public virtual DonationRequest DonationRequest { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        // Textbook
        [StringLength(100)]
        public string BookTitle { get; set; }

        [StringLength(50)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string GradeLevel { get; set; }

        [StringLength(50)]
        public string ISBN { get; set; }

        // Clothing
        [StringLength(20)]
        public string ClothingSize { get; set; }

        [StringLength(50)]
        public string ClothingType { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        // Stationery
        [StringLength(50)]
        public string StationeryType { get; set; }

        [StringLength(100)]
        public string BrandPreference { get; set; }

        // Food
        [StringLength(50)]
        public string FoodType { get; set; }

        [StringLength(50)]
        public string DietaryRequirements { get; set; }

        // Other
        [StringLength(50)]
        public string ItemSubCategory { get; set; }

        [StringLength(100)]
        public string SizeSpecifications { get; set; }

        [Required]
        public int QuantityRequested { get; set; }

        public int QuantityReceived { get; set; }

        [Required]
        public RequestPriority Priority { get; set; }

        [StringLength(500)]
        public string UrgencyReason { get; set; }

        public bool IsFulfilled { get; set; }
        public DateTime? FulfilledDate { get; set; }
        public DateTime CreatedAt { get; set; }

        [Required]
        public RequestItemStatus Status { get; set; }

        [StringLength(500)]
        public string DeclineReason { get; set; }

        public DateTime? DeclinedDate { get; set; }
        public int? DeclinedBy { get; set; }

        public DonationRequestItem()
        {
            CreatedAt = DateTime.Now;
            IsFulfilled = false;
            Priority = RequestPriority.Medium;
            QuantityRequested = 1;
            Status = RequestItemStatus.Pending;
        }

        public int CalculateItemScore()
        {
            int score = 0;
            switch (Priority)
            {
                case RequestPriority.Urgent: score = 50; break;
                case RequestPriority.High: score = 35; break;
                case RequestPriority.Medium: score = 20; break;
                case RequestPriority.Low: score = 10; break;
            }

            if (!string.IsNullOrEmpty(GradeLevel))
            {
                if (GradeLevel.Contains("12")) score += 15;
                else if (GradeLevel.Contains("11")) score += 12;
                else if (GradeLevel.Contains("10")) score += 10;
                else if (GradeLevel.Contains("9")) score += 8;
                else score += 5;
            }

            if (!string.IsNullOrEmpty(Subject)) score += 10;
            if (!string.IsNullOrEmpty(ClothingSize)) score += 10;
            if (!string.IsNullOrEmpty(UrgencyReason)) score += 5;

            return score;
        }
    }

    #endregion

    #region Food Donation Check

    public class FoodDonationCheck
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonationItemId { get; set; }

        [ForeignKey("DonationItemId")]
        public virtual DonationItem DonationItem { get; set; }

        [Required]
        public string FoodType { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public string BatchNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public FoodStorageType StorageType { get; set; }
        public string StorageNotes { get; set; }
        public FoodAllergen Allergens { get; set; }
        public string AllergenDetails { get; set; }
        public bool PackagingSealed { get; set; }
        public bool NoDamage { get; set; }
        public bool NoBulging { get; set; }
        public bool NoPestDamage { get; set; }
        public string QualityGrade { get; set; }
        public string ConditionNotes { get; set; }
        public bool AppearancePassed { get; set; }
        public bool SmellPassed { get; set; }
        public bool IsCommercialSource { get; set; }
        public bool ProperlyLabeled { get; set; }
        public bool NutritionInfoPresent { get; set; }
        public FoodDonationStatus Status { get; set; }
        public string RejectionReason { get; set; }
        public DateTime CheckedAt { get; set; }
        public int? CheckedBy { get; set; }
        public string CheckerName { get; set; }

        [NotMapped]
        public bool IsExpired => ExpiryDate < DateTime.Now.Date;

        [NotMapped]
        public bool IsExpiringSoon => ExpiryDate < DateTime.Now.AddMonths(3) && !IsExpired;

        public FoodDonationCheck()
        {
            Status = FoodDonationStatus.PendingEligibilityCheck;
            CheckedAt = DateTime.Now;
            Allergens = FoodAllergen.None;
            StorageType = FoodStorageType.ShelfStable;
            IsCommercialSource = true;
            ProperlyLabeled = true;
            NutritionInfoPresent = true;
            PackagingSealed = true;
            NoDamage = true;
            NoBulging = true;
            NoPestDamage = true;
            AppearancePassed = true;
            SmellPassed = true;
            QualityGrade = "Good";
            FoodType = "Canned";
        }
    }

    #endregion

    #region Donation Item

    public class DonationItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonorId { get; set; }

        [Required]
        [StringLength(50)]
        public string DonorType { get; set; }

        [StringLength(200)]
        public string DonorName { get; set; }

        [StringLength(100)]
        public string DonorEmail { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(100)]
        public string BookTitle { get; set; }

        [StringLength(50)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string GradeLevel { get; set; }

        [StringLength(50)]
        public string ISBN { get; set; }

        [StringLength(20)]
        public string ClothingSize { get; set; }

        [StringLength(50)]
        public string ClothingType { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        public int Quantity { get; set; }
        public int QuantityRemaining { get; set; }

        [Required]
        public AllocationType AllocationType { get; set; }

        public int? TargetStudentId { get; set; }

        [ForeignKey("TargetStudentId")]
        public virtual Student TargetStudent { get; set; }

        [Required]
        public string Condition { get; set; }

        [StringLength(500)]
        public string ConditionNotes { get; set; }

        public string PhotoEvidence { get; set; }

        // Delivery confirmation
        [StringLength(200)]
        public string DeliveryLocation { get; set; }

        [StringLength(100)]
        public string DeliveryConfirmedBy { get; set; }

        public DateTime? DeliveryConfirmedDate { get; set; }

        [StringLength(500)]
        public string DeliveryNotes { get; set; }
        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }


        [Required]
        public DonationStatus Status { get; set; }

        public string TrackingCode { get; set; }

        public DateTime DonationDate { get; set; }
        public DateTime? VerificationDate { get; set; }
        public int? VerifiedBy { get; set; }
        public DateTime? AllocationDate { get; set; }
        public DateTime? CollectionDate { get; set; }
        public bool IsActive { get; set; }
        public int? CampaignId { get; set; }

        public bool IsFoodItem { get; set; }

        public virtual ICollection<FoodDonationCheck> FoodChecks { get; set; }

        [NotMapped]
        public FoodDonationCheck FoodCheck => FoodChecks?.OrderByDescending(f => f.CheckedAt).FirstOrDefault();

        public string AiExtractedKeywords { get; set; }
        public string AiSuggestedMatches { get; set; }
        public DateTime? AiLastProcessed { get; set; }

        [ForeignKey("CampaignId")]
        public virtual DonationCampaign Campaign { get; set; }

        public virtual ICollection<DonationAllocation> Allocations { get; set; }



        public DonationItem()
        {
            DonationDate = DateTime.Now;
            Status = DonationStatus.PendingVerification;
            IsActive = true;
            Allocations = new HashSet<DonationAllocation>();
            FoodChecks = new HashSet<FoodDonationCheck>();
            TrackingCode = GenerateTrackingCode();
            IsFoodItem = false;
        }

        private string GenerateTrackingCode()
        {
            return "DON-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        }
    }

    #endregion

    #region Donation Allocation

    public class DonationAllocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonationItemId { get; set; }

        [ForeignKey("DonationItemId")]
        public virtual DonationItem DonationItem { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public int? RequestId { get; set; }

        [ForeignKey("RequestId")]
        public virtual DonationRequest Request { get; set; }

        public int QuantityAllocated { get; set; }
        public int QuantityCollected { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; }

        public string CollectionToken { get; set; }
        public string QRCode { get; set; }
        public string PinCode { get; set; }

        public DateTime AllocationDate { get; set; }
        public DateTime? ScheduledCollectionDate { get; set; }
        public DateTime? CollectionDate { get; set; }
        public int? CollectedBy { get; set; }
        public string CollectionConfirmation { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }

        public DonationAllocation()
        {
            AllocationDate = DateTime.Now;
            Status = "Pending";
            IsActive = true;
            CollectionToken = GenerateCollectionToken();
            PinCode = GeneratePinCode();
        }

        private string GenerateCollectionToken()
        {
            return "COL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        }

        private string GeneratePinCode()
        {
            var random = new Random();
            return random.Next(1000, 9999).ToString();
        }
    }

    #endregion

    #region Donation Campaign

    public class DonationCampaign
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public CampaignStatus Status { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TargetQuantity { get; set; }
        public int CurrentQuantity { get; set; }

        [StringLength(200)]
        public string TargetGroup { get; set; }

        public bool IsExternal { get; set; }
        public string ExternalPortalUrl { get; set; }
        public string CampaignImage { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<DonationItem> Donations { get; set; }
        public virtual ICollection<BulkPledge> Pledges { get; set; }

        public DonationCampaign()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            Status = CampaignStatus.Draft;
            Donations = new HashSet<DonationItem>();
            Pledges = new HashSet<BulkPledge>();
        }
    }

    #endregion

    #region Bulk Pledge

    public class BulkPledge
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CampaignId { get; set; }

        [ForeignKey("CampaignId")]
        public virtual DonationCampaign Campaign { get; set; }

        [Required]
        [StringLength(200)]
        public string DonorName { get; set; }

        [Required]
        [StringLength(100)]
        public string DonorEmail { get; set; }

        [StringLength(200)]
        public string DonorOrganization { get; set; }

        [StringLength(100)]
        public string DonorPhone { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public int QuantityPledged { get; set; }

        public int QuantityDelivered { get; set; }

        [Required]
        public PledgeStatus Status { get; set; }

        public DateTime ExpectedDeliveryDate { get; set; }
        public DateTime? ActualDeliveryDate { get; set; }
        public string DeliveryNotes { get; set; }
        public string TrackingReference { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }

        public BulkPledge()
        {
            CreatedAt = DateTime.Now;
            Status = PledgeStatus.PendingApproval;
            IsActive = true;
        }
    }

    #endregion

    #region Donation History

    public class DonationHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonationItemId { get; set; }

        [ForeignKey("DonationItemId")]
        public virtual DonationItem DonationItem { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public int? ActorId { get; set; }
        [StringLength(50)]
        public string ActorType { get; set; }

        public DateTime ActionDate { get; set; }
        public string AdditionalData { get; set; }

        public DonationHistory()
        {
            ActionDate = DateTime.Now;
        }
    }

    #endregion

    #region ViewModels

    // ────────────────────────────────────────────────────────────────
    //  Request Donation
    // ────────────────────────────────────────────────────────────────

    public class DonationRequestViewModel
    {
        public List<DonationRequestItemViewModel> Items { get; set; }

        public DonationRequestViewModel()
        {
            Items = new List<DonationRequestItemViewModel>();
        }
    }

    public class DonationRequestItemViewModel
    {
        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(100)]
        public string BookTitle { get; set; }

        [StringLength(50)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string GradeLevel { get; set; }

        [StringLength(50)]
        public string ISBN { get; set; }

        [StringLength(20)]
        public string ClothingSize { get; set; }

        [StringLength(50)]
        public string ClothingType { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [StringLength(50)]
        public string StationeryType { get; set; }

        [StringLength(100)]
        public string BrandPreference { get; set; }

        [StringLength(50)]
        public string FoodType { get; set; }

        [StringLength(50)]
        public string DietaryRequirements { get; set; }

        [StringLength(50)]
        public string ItemSubCategory { get; set; }

        [StringLength(100)]
        public string SizeSpecifications { get; set; }

        [Required]
        [Range(1, 10)]
        public int QuantityRequested { get; set; }

        [Required]
        public RequestPriority Priority { get; set; }

        [StringLength(500)]
        public string UrgencyReason { get; set; }
    }

    // ────────────────────────────────────────────────────────────────
    //  Add Donation (multi-item wizard)
    //
    //  Deliberately mirrors DonationRequestViewModel: a single Items
    //  collection, nothing else. Donor name, subjects, and current-needs
    //  data are passed via ViewBag so the model binder has only one
    //  property to bind — the same shape that RequestDonation uses,
    //  and which MVC's JSON binder handles reliably.
    // ────────────────────────────────────────────────────────────────

    public class DonationItemFormViewModel
    {
        public List<DonationItemEntry> Items { get; set; }

        public DonationItemFormViewModel()
        {
            Items = new List<DonationItemEntry>();
        }
    }

    public class DonationItemEntry
    {
        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(200)]
        public string BookTitle { get; set; }

        [StringLength(100)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string GradeLevel { get; set; }

        [StringLength(50)]
        public string ISBN { get; set; }

        [StringLength(20)]
        public string ClothingSize { get; set; }

        [StringLength(50)]
        public string ClothingType { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [StringLength(50)]
        public string StationeryType { get; set; }

        [StringLength(100)]
        public string BrandPreference { get; set; }

        [StringLength(50)]
        public string FoodType { get; set; }

        [StringLength(50)]
        public string DietaryRequirements { get; set; }

        [StringLength(50)]
        public string ItemSubCategory { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        [Required]
        [StringLength(50)]
        public string Condition { get; set; }

        [StringLength(500)]
        public string ConditionNotes { get; set; }
    }

    // Legacy single-item form view model — kept because it still appears
    // in some older partials/routes. Not used by AddDonation anymore.
    public class DonationItemViewModel
    {
        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(100)]
        public string BookTitle { get; set; }

        [StringLength(50)]
        public string Subject { get; set; }

        [StringLength(20)]
        public string GradeLevel { get; set; }

        [StringLength(50)]
        public string ISBN { get; set; }

        [StringLength(20)]
        public string ClothingSize { get; set; }

        [StringLength(50)]
        public string ClothingType { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        [Required]
        public AllocationType AllocationType { get; set; }

        public int? TargetStudentId { get; set; }

        [Required]
        public string Condition { get; set; }

        [StringLength(500)]
        public string ConditionNotes { get; set; }

        public string PhotoEvidence { get; set; }
    }

    // ────────────────────────────────────────────────────────────────
    //  Food Eligibility
    // ────────────────────────────────────────────────────────────────

    public class FoodEligibilityViewModel
    {
        public int Id { get; set; }
        public int DonationItemId { get; set; }
        public string DonationItemName { get; set; }
        public string DonorName { get; set; }
        public int Quantity { get; set; }
        public DateTime DonationDate { get; set; }

        [Required]
        public string FoodType { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        public string BatchNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public FoodStorageType StorageType { get; set; }
        public string StorageNotes { get; set; }
        public FoodAllergen Allergens { get; set; }
        public string AllergenDetails { get; set; }
        public bool PackagingSealed { get; set; }
        public bool NoDamage { get; set; }
        public bool NoBulging { get; set; }
        public bool NoPestDamage { get; set; }
        public string QualityGrade { get; set; }
        public string ConditionNotes { get; set; }
        public bool AppearancePassed { get; set; }
        public bool SmellPassed { get; set; }
        public bool IsCommercialSource { get; set; }
        public bool ProperlyLabeled { get; set; }
        public bool NutritionInfoPresent { get; set; }
        public FoodDonationStatus Status { get; set; }
        public string RejectionReason { get; set; }
        public string PhotoPath { get; set; }

        [NotMapped]
        public bool IsExpired => ExpiryDate < DateTime.Now.Date;

        [NotMapped]
        public bool IsExpiringSoon => ExpiryDate < DateTime.Now.AddMonths(3) && !IsExpired;

        [NotMapped]
        public string DaysUntilExpiry => IsExpired ? "Expired" : $"{(ExpiryDate - DateTime.Now).Days} days";
    }

    public class FoodEligibilityListViewModel
    {
        public int Id { get; set; }
        public int DonationItemId { get; set; }
        public string ItemName { get; set; }
        public string DonorName { get; set; }
        public int Quantity { get; set; }
        public string FoodType { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string ExpiryStatus { get; set; }
        public FoodDonationStatus Status { get; set; }
        public string StatusDisplay { get; set; }
        public string StatusBadgeClass { get; set; }
        public DateTime CheckedAt { get; set; }
        public string CheckerName { get; set; }
        public bool IsFoodItem { get; set; }
    }

    // ────────────────────────────────────────────────────────────────
    //  Dashboard / Distribution / Campaign / Pledge / Match
    // ────────────────────────────────────────────────────────────────

    public class DonationDashboardViewModel
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int FulfilledRequests { get; set; }
        public int TotalDonations { get; set; }
        public int PendingVerifications { get; set; }
        public int AvailableDonations { get; set; }
        public int TotalAllocations { get; set; }
        public int PendingCollection { get; set; }
        public int PendingFoodEligibility { get; set; }
        public int ApprovedFoodItems { get; set; }
        public int RejectedFoodItems { get; set; }
        public int ExpiredFoodItems { get; set; }
        public List<DonationRequest> RecentRequests { get; set; }
        public List<DonationItem> RecentDonations { get; set; }
        public List<DonationAllocation> RecentAllocations { get; set; }
        public List<DonationCampaign> ActiveCampaigns { get; set; }
        public List<FoodEligibilityListViewModel> PendingFoodChecks { get; set; }

        public DonationDashboardViewModel()
        {
            RecentRequests = new List<DonationRequest>();
            RecentDonations = new List<DonationItem>();
            RecentAllocations = new List<DonationAllocation>();
            ActiveCampaigns = new List<DonationCampaign>();
            PendingFoodChecks = new List<FoodEligibilityListViewModel>();
        }
    }

    public class DonationDistributionViewModel
    {
        public int AllocationId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; }
        public int Quantity { get; set; }
        public string CollectionToken { get; set; }
        public string QRCode { get; set; }
        public string PinCode { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string Status { get; set; }
        public bool IsFoodItem { get; set; }
        public string FoodStatus { get; set; }

        // Extra fields used by the Distribution view for AI match badges.
        public decimal? AiMatchScore { get; set; }
        public string AiMatchReason { get; set; }
    }

    public class CampaignViewModel
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, 10000)]
        public int TargetQuantity { get; set; }

        [StringLength(200)]
        public string TargetGroup { get; set; }

        public bool IsExternal { get; set; }
        public string ExternalPortalUrl { get; set; }
    }

    public class BulkPledgeViewModel
    {
        [Required]
        public int CampaignId { get; set; }

        [Required]
        [StringLength(200)]
        public string DonorName { get; set; }

        [Required]
        [StringLength(100)]
        public string DonorEmail { get; set; }

        [StringLength(200)]
        public string DonorOrganization { get; set; }

        [StringLength(100)]
        public string DonorPhone { get; set; }

        [Required]
        public DonationCategory Category { get; set; }

        [Required]
        public DonationItemType ItemType { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(1, 10000)]
        public int QuantityPledged { get; set; }

        [Required]
        public DateTime ExpectedDeliveryDate { get; set; }

        public string DeliveryNotes { get; set; }
    }

    public class PriorityMatchViewModel
    {
        public int RequestId { get; set; }
        public int RequestItemId { get; set; }
        public DonationCategory Category { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string ItemNeeded { get; set; }
        public int PriorityScore { get; set; }
        public int WaitListPosition { get; set; }
        public DateTime RequestDate { get; set; }
        public RequestPriority Priority { get; set; }
        public int? DonationItemId { get; set; }
        public string DonationItemName { get; set; }
        public int MatchScore { get; set; }
    }

    public class AIMatchResult
    {
        public int DonationItemId { get; set; }
        public int RequestId { get; set; }
        public decimal MatchScore { get; set; }
        public string MatchReason { get; set; }
        public string ExtractedKeywords { get; set; }
        public List<string> MatchedFields { get; set; }
        public List<string> UnmatchedFields { get; set; }
        public bool IsExactMatch { get; set; }

        public AIMatchResult()
        {
            MatchedFields = new List<string>();
            UnmatchedFields = new List<string>();
        }
    }

    #endregion

    #region Food Allocation Decision

    public class FoodAllocationDecision
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonationItemId { get; set; }

        [ForeignKey("DonationItemId")]
        public virtual DonationItem DonationItem { get; set; }

        public DateTime DecidedAt { get; set; }

        public int TotalUnitsAvailable { get; set; }
        public int TotalUnitsAllocated { get; set; }
        public int EligibleStudentCount { get; set; }
        public int ExcludedStudentCount { get; set; }

        [StringLength(2000)]
        public string ExcludedReasonSummary { get; set; }

        public int DecidedBy { get; set; }

        [StringLength(200)]
        public string DecidedByName { get; set; }

        public virtual ICollection<FoodAllocationDecisionLine> Lines { get; set; }

        public FoodAllocationDecision()
        {
            DecidedAt = DateTime.Now;
            Lines = new HashSet<FoodAllocationDecisionLine>();
        }
    }

    public class FoodAllocationDecisionLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FoodAllocationDecisionId { get; set; }

        [ForeignKey("FoodAllocationDecisionId")]
        public virtual FoodAllocationDecision FoodAllocationDecision { get; set; }

        [Required]
        public int StudentId { get; set; }

        public int RequestItemId { get; set; }
        public int AllocatedQuantity { get; set; }

        [StringLength(50)]
        public string Priority { get; set; }
    }

    #endregion
}