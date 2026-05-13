using System.Data.Entity.ModelConfiguration;

namespace ElevateED.Models
{
    // ============================================
    // REMOVE THESE - No longer needed (Driver/Transport)
    // ============================================
    // public class TransportRouteConfiguration : EntityTypeConfiguration<TransportRoute> { ... }
    // public class RouteTrackingConfiguration : EntityTypeConfiguration<RouteTracking> { ... }
    // public class EmergencyAlertConfiguration : EntityTypeConfiguration<EmergencyAlert> { ... }

    // ============================================
    // KEEP THESE - Updated for new models
    // ============================================

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

    // ============================================
    // ADD THESE - New configurations for updated models
    // ============================================

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

            HasOptional(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassEnrollmentConfiguration : EntityTypeConfiguration<ExtraClassEnrollment>
    {
        public ExtraClassEnrollmentConfiguration()
        {
            HasRequired(e => e.ExtraClass)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.ExtraClassId)
                .WillCascadeOnDelete(true);

            HasRequired(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassAttendanceSessionConfiguration : EntityTypeConfiguration<ExtraClassAttendanceSession>
    {
        public ExtraClassAttendanceSessionConfiguration()
        {
            HasRequired(s => s.ExtraClass)
                .WithMany(c => c.AttendanceSessions)
                .HasForeignKey(s => s.ExtraClassId)
                .WillCascadeOnDelete(true);

            HasRequired(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassAttendanceRecordConfiguration : EntityTypeConfiguration<ExtraClassAttendanceRecord>
    {
        public ExtraClassAttendanceRecordConfiguration()
        {
            HasRequired(r => r.AttendanceSession)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(r => r.AttendanceSessionId)
                .WillCascadeOnDelete(true);

            HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassFeedbackConfiguration : EntityTypeConfiguration<ExtraClassFeedback>
    {
        public ExtraClassFeedbackConfiguration()
        {
            HasRequired(f => f.ExtraClass)
                .WithMany(c => c.Feedbacks)
                .HasForeignKey(f => f.ExtraClassId)
                .WillCascadeOnDelete(true);

            HasRequired(f => f.Student)
                .WithMany()
                .HasForeignKey(f => f.StudentId)
                .WillCascadeOnDelete(false);
        }
    }

    public class ExtraClassAIRecommendationConfiguration : EntityTypeConfiguration<ExtraClassAIRecommendation>
    {
        public ExtraClassAIRecommendationConfiguration()
        {
            HasRequired(r => r.ExtraClass)
                .WithMany(c => c.AIRecommendations)
                .HasForeignKey(r => r.ExtraClassId)
                .WillCascadeOnDelete(true);
        }
    }
}