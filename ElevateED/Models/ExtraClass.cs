using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    public class ExtraClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public int GradeId { get; set; }

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; }

        [Required]
        public int SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        [Required]
        public DateTime ClassDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [StringLength(100)]
        public string Location { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal Fee { get; set; }

        public int MaxStudents { get; set; } = 30;

        public int CreatedBy { get; set; } // AdminId

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<ExtraClassBooking> Bookings { get; set; }

        public ExtraClass()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            Bookings = new HashSet<ExtraClassBooking>();
        }
    }

    public enum BookingStatus
    {
        Pending,
        Paid,
        Cancelled,
        Confirmed
    }

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