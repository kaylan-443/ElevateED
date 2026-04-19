using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    [Table("ApplicationUsers")]
    public class ApplicationUser
    {

        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string StudentNumber { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLogin { get; set; }

        // NEW: Track if user has changed password from default
        public bool HasChangedPassword { get; set; }

        public ApplicationUser()
        {
            IsActive = true;
            CreatedAt = DateTime.Now;
            Role = UserRole.Applicant;
            HasChangedPassword = false; // Default to false
        }
    }
}