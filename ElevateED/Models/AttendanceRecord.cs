using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    /// <summary>
    /// Represents a student's attendance status for a specific attendance session.
    /// </summary>
    public class AttendanceRecord
    {
        [Key]
        public int AttendanceRecordId { get; set; }

        [Required]
        public int AttendanceSessionId { get; set; }

        [ForeignKey("AttendanceSessionId")]
        public virtual AttendanceSession AttendanceSession { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required]
        public bool IsPresent { get; set; } = false;

        public DateTime? MarkedAt { get; set; }

        [Required]
        public bool IsManualOverride { get; set; } = false;
    }
}
