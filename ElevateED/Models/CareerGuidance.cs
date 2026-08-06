using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElevateED.Models
{
    // A broad grouping of careers, e.g. Engineering, Health Sciences, Commerce.
    public class CareerField
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        // Font Awesome icon name (without the "fas fa-" prefix), e.g. "cogs".
        [StringLength(50)]
        public string IconClass { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<Career> Careers { get; set; }
        public virtual ICollection<InterestQuestion> InterestQuestions { get; set; }

        public CareerField()
        {
            IsActive = true;
            IconClass = "briefcase";
            Careers = new HashSet<Career>();
            InterestQuestions = new HashSet<InterestQuestion>();
        }
    }

    // A specific career and the entry requirements for the qualification it needs.
    public class Career
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CareerFieldId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        // Typical qualification, e.g. "BEng Mechanical Engineering".
        [StringLength(200)]
        public string TypicalQualification { get; set; }

        // Free-text list of institutions, e.g. "UCT, Wits, UKZN".
        [StringLength(500)]
        public string WhereToStudy { get; set; }

        // Minimum Admission Point Score (APS) generally required.
        public int MinimumAps { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("CareerFieldId")]
        public virtual CareerField CareerField { get; set; }

        public virtual ICollection<CareerSubjectRequirement> SubjectRequirements { get; set; }

        public Career()
        {
            IsActive = true;
            SubjectRequirements = new HashSet<CareerSubjectRequirement>();
        }
    }

    // A single subject requirement for a career, e.g. Mathematics at level 5+.
    public class CareerSubjectRequirement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CareerId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        // Minimum NSC achievement level 1-7 the learner must reach in this subject.
        public int MinimumLevel { get; set; }

        // If true the subject must be taken; if false it is a "nice to have".
        public bool IsCompulsory { get; set; }

        [ForeignKey("CareerId")]
        public virtual Career Career { get; set; }

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; }

        public CareerSubjectRequirement()
        {
            MinimumLevel = 4;
            IsCompulsory = true;
        }
    }

    // A single Likert-style interest question that points toward a career field.
    public class InterestQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(300)]
        public string Text { get; set; }

        // The field a strong "agree" answer points toward.
        [Required]
        public int CareerFieldId { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("CareerFieldId")]
        public virtual CareerField CareerField { get; set; }

        public InterestQuestion()
        {
            IsActive = true;
        }
    }

    // The stored outcome of a learner completing the interest quiz.
    public class StudentInterestResult
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        public DateTime TakenAt { get; set; }

        public int? TopField1Id { get; set; }
        public int? TopField2Id { get; set; }
        public int? TopField3Id { get; set; }

        // Raw per-field scores serialised as JSON, e.g. {"1":18,"2":9}.
        public string RawScoresJson { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("TopField1Id")]
        public virtual CareerField TopField1 { get; set; }

        [ForeignKey("TopField2Id")]
        public virtual CareerField TopField2 { get; set; }

        [ForeignKey("TopField3Id")]
        public virtual CareerField TopField3 { get; set; }

        public StudentInterestResult()
        {
            TakenAt = DateTime.Now;
        }
    }

    // A career a learner has saved to their shortlist.
    public class StudentCareerBookmark
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CareerId { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("CareerId")]
        public virtual Career Career { get; set; }

        public StudentCareerBookmark()
        {
            CreatedAt = DateTime.Now;
        }
    }
}
