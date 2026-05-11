using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.ViewModels
{
    public class TeacherRegistrationViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "ID Number")]
        public string IdentityNumber { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Alternative Phone")]
        public string AlternativePhone { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Required]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Display(Name = "Qualification")]
        public string Qualification { get; set; }

        [Display(Name = "Years of Experience")]
        [Range(0, 50)]
        public int YearsOfExperience { get; set; }

        [Display(Name = "Subjects to Teach")]
        public List<string> Subjects { get; set; }

        [Display(Name = "Grades to Teach")]
        public List<string> Grades { get; set; }

        // NEW - Relational properties
        [Display(Name = "Subjects to Teach")]
        public List<int> SubjectIds { get; set; }

        [Display(Name = "Grades to Teach")]
        public List<int> GradeIds { get; set; }

        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; }

        public TeacherRegistrationViewModel()
        {
            Subjects = new List<string>();
            Grades = new List<string>();
            SubjectIds = new List<int>();
            GradeIds = new List<int>();
            YearsOfExperience = 0;
        }

        public class EditTeacherViewModel
        {
            public int Id { get; set; }

            [Required]
            public string FirstName { get; set; }

            public string MiddleName { get; set; }

            [Required]
            public string LastName { get; set; }

            [Required]
            public string IdentityNumber { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            public string PhoneNumber { get; set; }

            public string AlternativePhone { get; set; }

            public string Address { get; set; }

            [Required]
            public DateTime DateOfBirth { get; set; }

            [Required]
            public string Qualification { get; set; }

            [Range(0, 50)]
            public int YearsOfExperience { get; set; }

            public List<string> Subjects { get; set; }
            public List<string> Grades { get; set; }

            // ADD THESE
            public List<int> SubjectIds { get; set; }
            public List<int> GradeIds { get; set; }

            public string EmergencyContactName { get; set; }
            public string EmergencyContactPhone { get; set; }

            public EditTeacherViewModel()
            {
                Subjects = new List<string>();
                Grades = new List<string>();
                SubjectIds = new List<int>();
                GradeIds = new List<int>();
            }
        }
    }
}