using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ElevateED.ViewModels
{
    // Student: Create Issue
    public class CreateIssueViewModel
    {
        [Required]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        [Display(Name = "Issue Title")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Category")]
        public IssueCategory Category { get; set; }

        [Display(Name = "Report Anonymously")]
        public bool IsAnonymous { get; set; }

        [Display(Name = "Attachment (Optional)")]
        public HttpPostedFileBase Attachment { get; set; }
    }

    // Student: View My Issues
    public class MyIssueViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueCategory Category { get; set; }
        public IssuePriority Priority { get; set; }
        public IssueStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("dd MMM yyyy, HH:mm");
        public bool IsAnonymous { get; set; }
        public string StatusBadgeClass => GetStatusBadgeClass(Status);
        public string PriorityBadgeClass => GetPriorityBadgeClass(Priority);
        public string CategoryIcon => GetCategoryIcon(Category);

        private string GetStatusBadgeClass(IssueStatus status)
        {
            switch (status)
            {
                case IssueStatus.Pending: return "badge-warning";
                case IssueStatus.UnderReview: return "badge-info";
                case IssueStatus.Resolved: return "badge-success";
                case IssueStatus.Closed: return "badge-secondary";
                default: return "badge-secondary";
            }
        }

        private string GetPriorityBadgeClass(IssuePriority priority)
        {
            switch (priority)
            {
                case IssuePriority.Critical: return "badge-danger";
                case IssuePriority.High: return "badge-warning";
                case IssuePriority.Medium: return "badge-info";
                case IssuePriority.Low: return "badge-secondary";
                default: return "badge-secondary";
            }
        }

        private string GetCategoryIcon(IssueCategory category)
        {
            switch (category)
            {
                case IssueCategory.Academic: return "fas fa-book";
                case IssueCategory.Technical: return "fas fa-laptop";
                case IssueCategory.Bullying: return "fas fa-user-shield";
                case IssueCategory.Facility: return "fas fa-building";
                case IssueCategory.Discipline: return "fas fa-gavel";
                default: return "fas fa-question-circle";
            }
        }
    }

    // Admin: View All Issues
    public class AdminIssueViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueCategory Category { get; set; }
        public IssuePriority Priority { get; set; }
        public IssueStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("dd MMM yyyy, HH:mm");
        public bool IsAnonymous { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string StatusBadgeClass => GetStatusBadgeClass(Status);
        public string PriorityBadgeClass => GetPriorityBadgeClass(Priority);

        private string GetStatusBadgeClass(IssueStatus status)
        {
            switch (status)
            {
                case IssueStatus.Pending: return "badge-warning";
                case IssueStatus.UnderReview: return "badge-info";
                case IssueStatus.Resolved: return "badge-success";
                case IssueStatus.Closed: return "badge-secondary";
                default: return "badge-secondary";
            }
        }

        private string GetPriorityBadgeClass(IssuePriority priority)
        {
            switch (priority)
            {
                case IssuePriority.Critical: return "badge-danger";
                case IssuePriority.High: return "badge-warning";
                case IssuePriority.Medium: return "badge-info";
                case IssuePriority.Low: return "badge-secondary";
                default: return "badge-secondary";
            }
        }
    }

    // Admin: Issue Detail & Resolution
    public class IssueDetailViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueCategory Category { get; set; }
        public IssuePriority Priority { get; set; }
        public IssueStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public bool IsAnonymous { get; set; }

        // Student Info (if not anonymous)
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string StudentEmail { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }

        // Resolution
        public int? ResolvedBy { get; set; }
        public string ResolvedByName { get; set; }
        public string ResolutionNotes { get; set; }
        public string AdminResponse { get; set; }

        // Attachment
        public string AttachmentsPath { get; set; }
        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentsPath);
    }

    // Admin: Resolve Issue Form
    public class ResolveIssueViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Resolution Notes")]
        public string ResolutionNotes { get; set; }

        [Display(Name = "Response to Student (Optional)")]
        public string AdminResponse { get; set; }

        [Display(Name = "Mark as")]
        public IssueStatus NewStatus { get; set; } = IssueStatus.Resolved;
    }

    // Dashboard Stats
    public class IssueStatsViewModel
    {
        public int TotalIssues { get; set; }
        public int PendingIssues { get; set; }
        public int UnderReviewIssues { get; set; }
        public int ResolvedIssues { get; set; }
        public int CriticalIssues { get; set; }
    }

}