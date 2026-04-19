using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class Teacher
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string MiddleName { get; set; }

        [Required]
        public string IdentityNumber { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        public string AlternativePhone { get; set; }

        public string Address { get; set; }

        [Required]
        public string Qualification { get; set; }

        public int YearsOfExperience { get; set; }

        public string EmergencyContactName { get; set; }

        public string EmergencyContactPhone { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        // RELATIONAL PROPERTIES (replaces old Subjects and Grades strings)
        public virtual ICollection<TeacherSubjectQualification> SubjectQualifications { get; set; }
        public virtual ICollection<TeacherGradeAssignment> GradeAssignments { get; set; }

        // Navigation for assignments
        public virtual ICollection<TeacherSubjectAssignment> SubjectAssignments { get; set; }

        public Teacher()
        {
            IsActive = true;
            CreatedAt = DateTime.Now;
            SubjectQualifications = new HashSet<TeacherSubjectQualification>();
            GradeAssignments = new HashSet<TeacherGradeAssignment>();
            SubjectAssignments = new HashSet<TeacherSubjectAssignment>();
        }
    }
}