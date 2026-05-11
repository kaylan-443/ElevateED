using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ElevateED.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<Applicant> PendingApplications { get; set; }
        public DashboardStats Stats { get; set; }
        // Add to AdminDashboardViewModel class
        public ApplicationCycle ActiveCycle { get; set; }
    }

    public class DashboardStats
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
    }

    public class RegisterTeacherViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "ID number is required")]
        [Display(Name = "ID Number")]
        public string IdentityNumber { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Alternative Phone Number")]
        public string AlternativePhone { get; set; }

        [Required(ErrorMessage = "Please select at least one subject")]
        [Display(Name = "Subjects to Teach")]
        public List<string> Subjects { get; set; }

        [Required(ErrorMessage = "Please select at least one grade")]
        [Display(Name = "Grades to Teach")]
        public List<string> Grades { get; set; }

        [Required(ErrorMessage = "Qualification is required")]
        [Display(Name = "Qualification")]
        public string Qualification { get; set; }

        [Display(Name = "Years of Experience")]
        [Range(0, 50, ErrorMessage = "Years of experience must be between 0 and 50")]
        public int YearsOfExperience { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; }
    }

    public class TeacherViewModel
    {
        public int Id { get; set; }
        public string StaffNumber { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }  // Changed to a regular property
        public string Email { get; set; }
        public string Subjects { get; set; }
        public string Grades { get; set; }
        public string PhoneNumber { get; set; }
        public string IdentityNumber { get; set; }
        public string Qualification { get; set; }
        public int YearsOfExperience { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Address { get; set; }
        public string EmergencyContactName { get; set; }
        public string EmergencyContactPhone { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StudentNumber
        {
            get { return StaffNumber; }
            set { StaffNumber = value; }
        }
    }

    public class SubjectAssignmentViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? AssignedTeacherId { get; set; }
        public string AssignedTeacherName { get; set; }
        public List<TeacherViewModel> QualifiedTeachers { get; set; } = new List<TeacherViewModel>();
    }

    public class ClassTeacherAssignmentViewModel
    {
        public int TeacherId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string StaffNumber { get; set; }
    }

    public class TimeTableViewModel
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class UploadTimeTableViewModel
    {
        [Required]
        public string Type { get; set; }

        public string Grade { get; set; }

        public string ClassName { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public HttpPostedFileBase TimetableFile { get; set; }
    }
}