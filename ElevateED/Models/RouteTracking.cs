using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class RouteTracking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TransportRouteId { get; set; }

        [ForeignKey("TransportRouteId")]
        public virtual TransportRoute TransportRoute { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public DateTime TrackedAt { get; set; }

        public RouteTracking()
        {
            TrackedAt = DateTime.Now;
        }
    }
}