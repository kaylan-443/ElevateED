using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    // ============================================
    // ENUMS
    // ============================================

    public enum ExtraClassStatus
    {
        Upcoming,
        InProgress,
        Completed,
        Cancelled
    }

    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late
    }

    public enum BookingStatus
    {
        Pending,
        Paid,
        Cancelled,
        Confirmed
    }

    // ============================================
    // EXTRA CLASS MAIN MODEL
    // ============================================

    public class ExtraClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        [Required]
        public int GradeId { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }

        public int? TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public string Schedule { get; set; } // e.g., "Saturdays 09:00-11:00"

        [StringLength(200)]
        public string Venue { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Capacity { get; set; }

        public int CurrentEnrollment { get; set; }

        public ExtraClassStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        // Navigation Properties
        public virtual ICollection<ExtraClassEnrollment> Enrollments { get; set; }
        public virtual ICollection<ExtraClassAttendanceSession> AttendanceSessions { get; set; }
        public virtual ICollection<ExtraClassFeedback> Feedbacks { get; set; }
        public virtual ICollection<ExtraClassAIRecommendation> AIRecommendations { get; set; }
        // Keep old Booking for backward compatibility during transition
        public virtual ICollection<ExtraClassBooking> Bookings { get; set; }

        public ExtraClass()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            Status = ExtraClassStatus.Upcoming;
            Enrollments = new HashSet<ExtraClassEnrollment>();
            AttendanceSessions = new HashSet<ExtraClassAttendanceSession>();
            Feedbacks = new HashSet<ExtraClassFeedback>();
            AIRecommendations = new HashSet<ExtraClassAIRecommendation>();
            Bookings = new HashSet<ExtraClassBooking>();
        }
    }

    // ============================================
    // ENROLLMENT MODEL
    // ============================================

    public class ExtraClassEnrollment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public bool IsPaid { get; set; }

        public DateTime PaymentDate { get; set; }

        [StringLength(100)]
        public string PaymentReference { get; set; }

        public DateTime EnrollmentDate { get; set; }

        public bool IsActive { get; set; }

        public ExtraClassEnrollment()
        {
            EnrollmentDate = DateTime.Now;
            IsActive = true;
            IsPaid = false;
        }
    }

    // ============================================
    // BOOKING MODEL (Keep for backward compatibility)
    // ============================================

    public class ExtraClassBooking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public BookingStatus Status { get; set; }

        public DateTime BookedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        [StringLength(100)]
        public string PaymentReference { get; set; }

        public decimal AmountPaid { get; set; }

        public ExtraClassBooking()
        {
            BookedAt = DateTime.Now;
            Status = BookingStatus.Pending;
        }
    }

    // ============================================
    // ATTENDANCE SESSION MODEL
    // ============================================

    public class ExtraClassAttendanceSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        [Required]
        public int SessionNumber { get; set; } // 1, 2, 3, etc.

        public DateTime SessionDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        [Required]
        [StringLength(50)]
        public string QRCode { get; set; }

        public DateTime QRCodeExpiry { get; set; }

        [StringLength(500)]
        public string TopicsCovered { get; set; }

        public AttendanceStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<ExtraClassAttendanceRecord> AttendanceRecords { get; set; }

        public ExtraClassAttendanceSession()
        {
            CreatedAt = DateTime.Now;
            QRCode = GenerateQRCode();
            QRCodeExpiry = DateTime.Now.AddHours(2);
            AttendanceRecords = new HashSet<ExtraClassAttendanceRecord>();
        }

        private string GenerateQRCode()
        {
            var random = new Random();
            return "EC" + DateTime.Now.ToString("yyyyMMddHHmmss") + random.Next(1000, 9999);
        }
    }

    // ============================================
    // ATTENDANCE RECORD MODEL
    // ============================================

    public class ExtraClassAttendanceRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AttendanceSessionId { get; set; }

        [ForeignKey("AttendanceSessionId")]
        public virtual ExtraClassAttendanceSession AttendanceSession { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public DateTime ScanTime { get; set; }

        public AttendanceStatus Status { get; set; }

        public bool IsPresent { get; set; }

        public ExtraClassAttendanceRecord()
        {
            ScanTime = DateTime.Now;
            IsPresent = true;
            Status = AttendanceStatus.Present;
        }
    }

    // ============================================
    // FEEDBACK MODEL
    // ============================================

    public class ExtraClassFeedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(500)]
        public string Comment { get; set; }

        public DateTime DateSubmitted { get; set; }

        public ExtraClassFeedback()
        {
            DateSubmitted = DateTime.Now;
        }
    }

    // ============================================
    // AI RECOMMENDATION MODEL
    // ============================================

    public class ExtraClassAIRecommendation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExtraClassId { get; set; }

        [ForeignKey("ExtraClassId")]
        public virtual ExtraClass ExtraClass { get; set; }

        [Required]
        public string RecommendedTopics { get; set; } // JSON array

        [Required]
        public string DifficultTopics { get; set; } // JSON array

        public string SuggestedTeachingOrder { get; set; } // JSON array

        public string EasyWinTopics { get; set; } // JSON array

        public string CommonMistakes { get; set; } // JSON array

        public decimal PredictedImprovement { get; set; }

        public DateTime GeneratedDate { get; set; }

        public bool IsApplied { get; set; }

        public ExtraClassAIRecommendation()
        {
            GeneratedDate = DateTime.Now;
            IsApplied = false;
        }
    }

    // ============================================
    // PAYMENT MODEL (Keep for backward compatibility)
    // ============================================

    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey("BookingId")]
        public virtual ExtraClassBooking Booking { get; set; }

        [Required]
        [StringLength(100)]
        public string PaymentReference { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(20)]
        public string Status { get; set; } // "Success", "Failed", "Pending"

        public Payment()
        {
            PaymentDate = DateTime.Now;
            Status = "Pending";
        }
    }
}