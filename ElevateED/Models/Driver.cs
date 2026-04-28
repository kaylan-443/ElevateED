using System;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public class Driver
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(20)]
        public string VehicleRegistration { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsActive { get; set; }

        public int? CreatedBy { get; set; }

        public Driver()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            ExpiresAt = DateTime.Now.AddHours(8); // Default 8-hour session
        }
    }
}