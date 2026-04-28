using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class TransportRoute
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public int DriverId { get; set; }

        [ForeignKey("DriverId")]
        public virtual Driver Driver { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        [StringLength(20)]
        public string Status { get; set; } // "Pending", "Active", "Completed", "Emergency"

        public DateTime CreatedAt { get; set; }

        public TransportRoute()
        {
            CreatedAt = DateTime.Now;
            Status = "Pending";
        }
    }
}