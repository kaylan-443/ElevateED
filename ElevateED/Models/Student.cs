using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ApplicantId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [StringLength(50)]
        public string MiddleName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        [StringLength(20)]
        public string IdentityNumber { get; set; }

        [StringLength(50)]
        public string Nationality { get; set; }

        [StringLength(50)]
        public string HomeLanguage { get; set; }

        // RELATIONAL PROPERTIES
        public int? ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        // ADD THIS - Grade Applying For (used before class allocation)
        public int? GradeApplyingForId { get; set; }

        [ForeignKey("GradeApplyingForId")]
        public virtual Grade GradeApplyingFor { get; set; }

        public int? StreamId { get; set; }

        [ForeignKey("StreamId")]
        public virtual Stream Stream { get; set; }

        // Contact Information
        [Required]
        [StringLength(15)]
        public string CellPhone { get; set; }

        [StringLength(15)]
        public string AlternativePhone { get; set; }

        [Required]
        public string PhysicalAddress { get; set; }

        public string PostalAddress { get; set; }

        // Parent Information
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

        // Document Paths
        public string IdDocumentPath { get; set; }
        public string ReportCardPath { get; set; }
        public string TransferCertificatePath { get; set; }
        public string ProofOfResidencePath { get; set; }
        public string ParentIdDocumentPath { get; set; }

        // Status
        public bool IsActive { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("ApplicantId")]
        public virtual Applicant Applicant { get; set; }

        // Computed Properties
        public string FullName => $"{FirstName} {LastName}";

        public string Grade => Class?.Grade?.Name ?? GradeApplyingFor?.Name;
        public string ClassName => Class?.FullName;
        public string StreamChoice => Stream?.Name;

        public Student()
        {
            IsActive = true;
            EnrollmentDate = DateTime.Now;
        }
    }
}