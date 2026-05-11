using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Applicant
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        // Personal Information
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        public string IdentityNumber { get; set; }

        public string Nationality { get; set; }
        public string HomeLanguage { get; set; }

        // Contact Information
        [Required]
        public string CellPhone { get; set; }
        public string AlternativePhone { get; set; }

        [Required]
        public string PhysicalAddress { get; set; }
        public string PostalAddress { get; set; }

        // Academic Information
        [Required]
        public string PreviousSchool { get; set; }

        [Required]
        public string HighestGradePassed { get; set; }

        [Required]
        public int YearCompleted { get; set; }

        public string PreviousSchoolAddress { get; set; }
        public decimal? AcademicAverage { get; set; }

        // Application Details - RELATIONAL
        public int? ApplicationCycleId { get; set; }
        [ForeignKey("ApplicationCycleId")]
        public virtual ApplicationCycle ApplicationCycle { get; set; }

        public int? GradeApplyingForId { get; set; }
        [ForeignKey("GradeApplyingForId")]
        public virtual Grade GradeApplyingFor { get; set; }

        public int? StreamId { get; set; }
        [ForeignKey("StreamId")]
        public virtual Stream Stream { get; set; }

        // String fields for backward compatibility (keep these)
        public string GradeApplyingForName { get; set; }
        public string StreamChoiceName { get; set; }

        // Parent/Guardian Information
        [Required]
        public string ParentName { get; set; }

        [Required]
        public string ParentIdNumber { get; set; }

        [Required]
        public string ParentRelationship { get; set; }

        [Required]
        public string ParentCellPhone { get; set; }

        [Required]
        public string ParentEmail { get; set; }

        public string ParentWorkPhone { get; set; }
        public string ParentOccupation { get; set; }
        public string ParentEmployer { get; set; }
        public string ParentWorkAddress { get; set; }

        // Emergency Contact
        [Required]
        public string EmergencyContactName { get; set; }

        [Required]
        public string EmergencyContactPhone { get; set; }
        public string EmergencyContactRelationship { get; set; }

        // Medical Information
        public string MedicalConditions { get; set; }
        public string Allergies { get; set; }
        public string CurrentMedication { get; set; }
        public string DoctorName { get; set; }
        public string DoctorPhone { get; set; }
        public string MedicalAidName { get; set; }
        public string MedicalAidNumber { get; set; }

        // Documents
        public string IdDocumentPath { get; set; }
        public string ReportCardPath { get; set; }
        public string TransferCertificatePath { get; set; }
        public string ProofOfResidencePath { get; set; }
        public string ParentIdDocumentPath { get; set; }

        // Application Status
        public ApplicationStatus Status { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? ReviewDate { get; set; }
        public string ReviewedBy { get; set; }
        public string RejectionReason { get; set; }
        public string AdminNotes { get; set; }

        // Computed Properties
        public string FullName => $"{FirstName} {LastName}";

        [NotMapped]
        public string GradeApplyingForDisplay => GradeApplyingFor?.Name ?? GradeApplyingForName;

        [NotMapped]
        public string StreamChoiceDisplay => Stream?.Name ?? StreamChoiceName;

        public Applicant()
        {
            Status = ApplicationStatus.Pending;
            ApplicationDate = DateTime.Now;
            Nationality = "South African";
        }
    }
}