using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ElevateED.Models
{
    public class Trip
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

        public string TripType { get; set; } // "Outbound" or "Return"

        public string Status { get; set; } // "RollCall", "DestinationSet", "Active", "Completed"

        public string DestinationName { get; set; }

        public string DestinationAddress { get; set; }

        public double? DestinationLatitude { get; set; }

        public double? DestinationLongitude { get; set; }

        public string RollCallData { get; set; } // JSON string of roll call data

        public DateTime? StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public Trip()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
        }
    }
}