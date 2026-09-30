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

        // Which of this lab type's fixed checkpoints the teacher requires
        // for this experiment, and how many marks each is worth.
        public virtual ICollection<VirtualLabExperimentTask> Tasks { get; set; }

        public VirtualLabExperiment()
        {
            CreatedAt = DateTime.Now;
            IsActive = true;
            Assignments = new HashSet<VirtualLabAssignment>();
            Tasks = new HashSet<VirtualLabExperimentTask>();
        }
    }

    // A single gradable checkpoint within an experiment (e.g. "Mix at least
    // two chemicals in the beaker"). The set of possible keys is fixed per
    // LabType by the simulation itself (see VirtualLabTaskCatalog below) —
    // this row just records which of those the teacher chose to require for
    // THIS experiment, and how many marks it's worth.
    public class VirtualLabExperimentTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExperimentId { get; set; }

        [ForeignKey("ExperimentId")]
        public virtual VirtualLabExperiment Experiment { get; set; }

        [Required]
        [StringLength(50)]
        public string TaskKey { get; set; }

        [Required]
        [StringLength(200)]
        public string TaskLabel { get; set; }

        [Required]
        public decimal MaxMarks { get; set; }

        public int SortOrder { get; set; }
    }

    // The fixed catalog of checkpoints each lab simulation can detect and
    // report (matches the data-task="..." items in BiologyLab/ChemistryLab/
    // PhysicsLab.cshtml). Used to render the teacher's task/marks picker and
    // to validate/label whatever a student's session reports at submit time.
    public class LabTaskDefinition
    {
        public string Key { get; set; }
        public string Label { get; set; }

        public LabTaskDefinition(string key, string label)
        {
            Key = key;
            Label = label;
        }
    }

    public static class VirtualLabTaskCatalog
    {
        public static readonly Dictionary<LabType, List<LabTaskDefinition>> Tasks = new Dictionary<LabType, List<LabTaskDefinition>>
        {
            {
                LabType.Biology, new List<LabTaskDefinition>
                {
                    new LabTaskDefinition("examine", "Bring a specimen slide into focus"),
                    new LabTaskDefinition("identify", "Identify a labelled structure"),
                    new LabTaskDefinition("incubate", "Incubate the petri dish culture"),
                    new LabTaskDefinition("dissect", "Dissect and reveal the flower's structure"),
                    new LabTaskDefinition("photosynthesis", "Measure the photosynthesis (O2) rate"),
                    new LabTaskDefinition("observe", "Record a written observation")
                }
            },
            {
                LabType.Chemistry, new List<LabTaskDefinition>
                {
                    new LabTaskDefinition("beaker", "Mix at least two chemicals in the beaker"),
                    new LabTaskDefinition("reaction", "Trigger a known chemical reaction"),
                    new LabTaskDefinition("heat", "Use the heating apparatus"),
                    new LabTaskDefinition("testtube", "Perform a test tube reaction"),
                    new LabTaskDefinition("ph", "Measure the solution's pH"),
                    new LabTaskDefinition("observe", "Record a written observation")
                }
            },
            {
                LabType.Physics, new List<LabTaskDefinition>
                {
                    new LabTaskDefinition("launch", "Launch the projectile at least once"),
                    new LabTaskDefinition("target", "Land a shot inside the target zone"),
                    new LabTaskDefinition("pendulum", "Time the pendulum's oscillation period"),
                    new LabTaskDefinition("circuit", "Close the circuit and read the current"),
                    new LabTaskDefinition("spring", "Measure the spring's extension under load"),
                    new LabTaskDefinition("observe", "Record a written observation")
                }
            }
        };
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
        public decimal? MaxScore { get; set; }

        // True when Score was computed by SubmitLab from completed tasks
        // rather than typed in by a teacher on the Grade Submissions screen.
        // A teacher can still open the result and override the score, which
        // does not clear this flag (it's a record of how the score
        // originated, not who last touched it — GradedBy/GradedAt track that).
        public bool AutoGraded { get; set; }

        // JSON snapshot of the task-by-task breakdown at grading time:
        // [{ "key": "beaker", "label": "...", "maxMarks": 5, "completed": true }, ...]
        public string TaskScoresJson { get; set; }

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