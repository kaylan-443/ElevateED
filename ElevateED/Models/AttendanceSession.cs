using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    /// <summary>
    /// Represents an attendance session initiated by a teacher with OTP-based marking.
    /// </summary>
    public class AttendanceSession
    {
        [Key]
        public int AttendanceSessionId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        [Required]
        [StringLength(450)]
        public string TeacherId { get; set; }

        [Required]
        public DateTime SessionDate { get; set; }

        [Required]
        [StringLength(6)]
        public string OTPCode { get; set; }

        public DateTime OTPExpiry { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}
