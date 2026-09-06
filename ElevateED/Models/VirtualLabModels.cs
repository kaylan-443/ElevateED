using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ElevateED.Models
{
    public enum LabType
    {
        Biology,
        Chemistry,
        Physics
    }

    public enum LabDifficulty
    {
        Beginner,
        Intermediate,
        Advanced
    }

    public class VirtualLabExperiment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string Instructions { get; set; }

        [Required]
        public LabType LabType { get; set; }

        [Required]
        public LabDifficulty Difficulty { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        [Required]
        [StringLength(20)]
        public string GradeLevel { get; set; }

        // These properties exist in the model
        public string LearningObjectives { get; set; }
        public string PreLabQuestions { get; set; }
        public string PostLabQuestions { get; set; }
        public string EquipmentList { get; set; }

        // Remove SceneConfig - it's not needed for the lab views
        // The lab views (BiologyLab, ChemistryLab, PhysicsLab) have their own built-in 3D environments

        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual Teacher Creator { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<VirtualLabAssignment> Assignments { get; set; }

        public VirtualLabExperiment()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            Assignments = new HashSet<VirtualLabAssignment>();
        }
    }

    public class VirtualLabAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExperimentId { get; set; }

        [ForeignKey("ExperimentId")]
        public virtual VirtualLabExperiment Experiment { get; set; }

        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; }

        public int? SubjectId { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public DateTime DueDate { get; set; }
        public string Instructions { get; set; }
        public string TaskRequirements { get; set; } // JSON for interactive tasks

        public int AssignedBy { get; set; }

        [ForeignKey("AssignedBy")]
        public virtual Teacher Teacher { get; set; }

        public DateTime AssignedAt { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<VirtualLabResult> Results { get; set; }

        public VirtualLabAssignment()
        {
            AssignedAt = DateTime.Now;
            IsActive = true;
            Results = new HashSet<VirtualLabResult>();
        }
    }

    public class VirtualLabResult
    {
        [Key]
        public int Id { get; set; }

        public int? AssignmentId { get; set; }

        [ForeignKey("AssignmentId")]
        public virtual VirtualLabAssignment Assignment { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        public string Observations { get; set; }
        public string Conclusions { get; set; }
        public string Answers { get; set; }
        public string ExperimentData { get; set; }
        public string ScreenshotPath { get; set; }

        public decimal? Score { get; set; }
        public string TeacherFeedback { get; set; }

        public int? GradedBy { get; set; }

        [ForeignKey("GradedBy")]
        public virtual Teacher Grader { get; set; }

        public DateTime? GradedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }

        public int? SessionId { get; set; }

        [ForeignKey("SessionId")]
        public virtual VirtualLabSession Session { get; set; }

        public VirtualLabResult()
        {
            StartedAt = DateTime.Now;
            Status = "InProgress";
        }
    }

    public class VirtualLabSession
    {
        [Key]
        public int Id { get; set; }

        public string SessionToken { get; set; }
        public string CurrentState { get; set; }
        public string InteractionsLog { get; set; }
        public DateTime LastActivityAt { get; set; }
        public bool IsActive { get; set; }

        public VirtualLabSession()
        {
            SessionToken = Guid.NewGuid().ToString("N");
            LastActivityAt = DateTime.Now;
            IsActive = true;
        }
    }
}