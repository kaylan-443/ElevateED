using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ElevateED.ViewModels
{
    public class VirtualLabDashboardViewModel
    {
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string Stream { get; set; }
        public List<LabType> AllowedLabTypes { get; set; }
        public List<VirtualLabExperiment> AssignedExperiments { get; set; }
        public List<VirtualLabExperiment> InProgressExperiments { get; set; }
        public List<VirtualLabResult> CompletedExperiments { get; set; }
        public List<VirtualLabExperiment> AvailableExperiments { get; set; }

        public VirtualLabDashboardViewModel()
        {
            AllowedLabTypes = new List<LabType>();
            AssignedExperiments = new List<VirtualLabExperiment>();
            InProgressExperiments = new List<VirtualLabExperiment>();
            CompletedExperiments = new List<VirtualLabResult>();
            AvailableExperiments = new List<VirtualLabExperiment>();
        }
    }

    public class TeacherLabDashboardViewModel
    {
        public List<VirtualLabExperiment> Experiments { get; set; }
        public List<VirtualLabAssignment> Assignments { get; set; }
        public List<VirtualLabResult> PendingSubmissions { get; set; }
        public string TeacherName { get; set; }

        public TeacherLabDashboardViewModel()
        {
            Experiments = new List<VirtualLabExperiment>();
            Assignments = new List<VirtualLabAssignment>();
            PendingSubmissions = new List<VirtualLabResult>();
        }
    }

    public class CreateExperimentViewModel
    {
        [Required]
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
        [Range(5, 120)]
        public int DurationMinutes { get; set; }

        [Required]
        public string GradeLevel { get; set; }

        public string LearningObjectives { get; set; }
        public string PreLabQuestions { get; set; }
        public string PostLabQuestions { get; set; }
        public string EquipmentList { get; set; }
        
    }
}