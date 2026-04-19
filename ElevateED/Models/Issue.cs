using System;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models
{
    public enum IssueStatus
    {
        Pending,
        UnderReview,
        Resolved,
        Closed
    }

    public enum IssuePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum IssueCategory
    {
        Academic,
        Technical,
        Bullying,
        Facility,
        Discipline,
        Other
    }

    public class Issue
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }  // Who reported it

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public IssueCategory Category { get; set; }

        public IssuePriority Priority { get; set; }

        public IssueStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public int? ResolvedBy { get; set; }  // Admin/User who resolved it

        public string ResolutionNotes { get; set; }

        public string AdminResponse { get; set; }

        public bool IsAnonymous { get; set; }

        public string AttachmentsPath { get; set; }  // Optional file upload

        // Navigation property
        public virtual Student Student { get; set; }
    }
}