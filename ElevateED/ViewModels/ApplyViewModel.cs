using System;
using System.ComponentModel.DataAnnotations;
using System.Web;
using ElevateED.Models;

namespace ElevateED.ViewModels
{
    public class ApplyViewModel
    {
        // Personal Information
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public Gender Gender { get; set; }

        [Required(ErrorMessage = "ID/Passport number is required")]
        [Display(Name = "ID Number / Passport Number")]
        public string IdentityNumber { get; set; }

        [Display(Name = "Nationality")]
        public string Nationality { get; set; }

        [Display(Name = "Home Language")]
        public string HomeLanguage { get; set; }

        // Contact Information
        [Required(ErrorMessage = "Cell phone number is required")]
        [Display(Name = "Cell Phone Number")]
        public string CellPhone { get; set; }

        [Display(Name = "Alternative Phone Number")]
        public string AlternativePhone { get; set; }

        [Required(ErrorMessage = "Physical address is required")]
        [Display(Name = "Physical Address")]
        public string PhysicalAddress { get; set; }

        [Display(Name = "Postal Address")]
        public string PostalAddress { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [Display(Name = "Email Address")]
        [EmailAddress]
        public string Email { get; set; }

        // Academic Information
        [Required(ErrorMessage = "Previous school is required")]
        [Display(Name = "Previous School Name")]
        public string PreviousSchool { get; set; }

        [Required(ErrorMessage = "Highest grade passed is required")]
        [Display(Name = "Highest Grade Passed")]
        public string HighestGradePassed { get; set; }

        [Required(ErrorMessage = "Year completed is required")]
        [Display(Name = "Year Completed")]
        public int YearCompleted { get; set; }

        [Display(Name = "Previous School Address")]
        public string PreviousSchoolAddress { get; set; }

        [Display(Name = "Academic Average (%)")]
        [Range(0, 100)]
        public decimal? AcademicAverage { get; set; }

        // Application Details
        [Required(ErrorMessage = "Grade applying for is required")]
        [Display(Name = "Grade Applying For")]
        public string GradeApplyingFor { get; set; }

        [Display(Name = "Stream / Subject Choice")]
        public string StreamChoice { get; set; }

        // Parent/Guardian Information
        [Required(ErrorMessage = "Parent/Guardian name is required")]
        [Display(Name = "Parent/Guardian Full Name")]
        public string ParentName { get; set; }

        [Required(ErrorMessage = "Parent ID number is required")]
        [Display(Name = "Parent/Guardian ID Number")]
        public string ParentIdNumber { get; set; }

        [Required(ErrorMessage = "Relationship is required")]
        [Display(Name = "Relationship to Applicant")]
        public string ParentRelationship { get; set; }

        [Required(ErrorMessage = "Parent cell phone is required")]
        [Display(Name = "Parent/Guardian Cell Phone")]
        public string ParentCellPhone { get; set; }

        [Required(ErrorMessage = "Parent email is required")]
        [Display(Name = "Parent/Guardian Email")]
        [EmailAddress]
        public string ParentEmail { get; set; }

        [Display(Name = "Parent/Guardian Work Phone")]
        public string ParentWorkPhone { get; set; }

        [Display(Name = "Parent/Guardian Occupation")]
        public string ParentOccupation { get; set; }

        [Display(Name = "Parent/Guardian Employer")]
        public string ParentEmployer { get; set; }

        [Display(Name = "Parent/Guardian Work Address")]
        public string ParentWorkAddress { get; set; }

        // Emergency Contact
        [Required(ErrorMessage = "Emergency contact name is required")]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [Required(ErrorMessage = "Emergency contact phone is required")]
        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; }

        [Display(Name = "Relationship to Applicant")]
        public string EmergencyContactRelationship { get; set; }

        // Medical Information
        [Display(Name = "Medical Conditions (if any)")]
        public string MedicalConditions { get; set; }

        [Display(Name = "Allergies (if any)")]
        public string Allergies { get; set; }

        [Display(Name = "Current Medication (if any)")]
        public string CurrentMedication { get; set; }

        [Display(Name = "Doctor's Name")]
        public string DoctorName { get; set; }

        [Display(Name = "Doctor's Phone Number")]
        public string DoctorPhone { get; set; }

        [Display(Name = "Medical Aid Scheme Name")]
        public string MedicalAidName { get; set; }

        [Display(Name = "Medical Aid Number")]
        public string MedicalAidNumber { get; set; }

        // File Uploads
        [Display(Name = "ID Document / Birth Certificate")]
        public HttpPostedFileBase IdDocument { get; set; }

        [Display(Name = "Previous School Report Card")]
        public HttpPostedFileBase ReportCard { get; set; }

        [Display(Name = "Transfer Certificate (if applicable)")]
        public HttpPostedFileBase TransferCertificate { get; set; }

        [Display(Name = "Proof of Residence")]
        public HttpPostedFileBase ProofOfResidence { get; set; }

        [Display(Name = "Parent/Guardian ID Document")]
        public HttpPostedFileBase ParentIdDocument { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class ApplicationStatusViewModel
    {
        public string StudentNumber { get; set; }
        public string ApplicantName { get; set; }
        public ApplicationStatus Status { get; set; }
        public DateTime ApplicationDate { get; set; }
        public string GradeApplyingFor { get; set; }
        public string RejectionReason { get; set; }
        public string AdminNotes { get; set; }
    }
}