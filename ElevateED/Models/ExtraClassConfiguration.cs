using System.Data.Entity.ModelConfiguration;

namespace ElevateED.Models
{
    public class TransportRouteConfiguration : EntityTypeConfiguration<TransportRoute>
    {
        public TransportRouteConfiguration()
        {
            HasRequired(r => r.ExtraClass)
                .WithMany()
                .HasForeignKey(r => r.ExtraClassId)
                .WillCascadeOnDelete(false);

            HasRequired(r => r.Driver)
                .WithMany()
                .HasForeignKey(r => r.DriverId)
                .WillCascadeOnDelete(false);
        }
    }

    public class RouteTrackingConfiguration : EntityTypeConfiguration<RouteTracking>
    {
        public RouteTrackingConfiguration()
        {
            HasRequired(t => t.TransportRoute)
                .WithMany()
                .HasForeignKey(t => t.TransportRouteId)
                .WillCascadeOnDelete(false);
        }
    }

    public class EmergencyAlertConfiguration : EntityTypeConfiguration<EmergencyAlert>
    {
        public EmergencyAlertConfiguration()
        {
            HasRequired(a => a.TransportRoute)
                .WithMany()
                .HasForeignKey(a => a.TransportRouteId)
                .WillCascadeOnDelete(false);

            HasRequired(a => a.Driver)
                .WithMany()
                .HasForeignKey(a => a.DriverId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassBookingConfiguration : EntityTypeConfiguration<ExtraClassBooking>
    {
        public ExtraClassBookingConfiguration()
        {
            HasRequired(b => b.ExtraClass)
                .WithMany(e => e.Bookings)
                .HasForeignKey(b => b.ExtraClassId)
                .WillCascadeOnDelete(false);

            HasRequired(b => b.Student)
                .WithMany()
                .HasForeignKey(b => b.StudentId)
                .WillCascadeOnDelete(false);
        }
    }

    public class PaymentConfiguration : EntityTypeConfiguration<Payment>
    {
        public PaymentConfiguration()
        {
            HasRequired(p => p.Booking)
                .WithMany()
                .HasForeignKey(p => p.BookingId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassConfiguration : EntityTypeConfiguration<ExtraClass>
    {
        public ExtraClassConfiguration()
        {
            HasRequired(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .WillCascadeOnDelete(false);

            HasRequired(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .WillCascadeOnDelete(false);
        }
    }
}