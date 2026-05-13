using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class AttendanceSession
    {
        [Key]
        public int AttendanceSessionId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [Required]
        public DateTime SessionDate { get; set; }

        [Required]
        [StringLength(6)]
        public string OTPCode { get; set; }
        public string QRCode { get; set; }  // ADD THIS
        public DateTime OTPExpiry { get; set; }
        public DateTime? QRCodeExpiry { get; set; }  // ADD THIS
        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}