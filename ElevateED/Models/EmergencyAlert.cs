using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved
    }

    public class EmergencyAlert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TransportRouteId { get; set; }

        [ForeignKey("TransportRouteId")]
        public virtual TransportRoute TransportRoute { get; set; }

        [Required]
        public int DriverId { get; set; }

        [ForeignKey("DriverId")]
        public virtual Driver Driver { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public AlertStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public int? AcknowledgedBy { get; set; }

        public EmergencyAlert()
        {
            CreatedAt = DateTime.Now;
            Status = AlertStatus.Active;
        }
    }
}