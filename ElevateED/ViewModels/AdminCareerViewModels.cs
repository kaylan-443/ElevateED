using System.Collections.Generic;
using ElevateED.Models;

namespace ElevateED.ViewModels
{
    public class FieldCountItem
    {
        public string FieldName { get; set; }
        public int Count { get; set; }
    }

    public class AdminCareerOverviewViewModel
    {
        public int FieldCount { get; set; }
        public int CareerCount { get; set; }
        public int QuestionCount { get; set; }
        public int QuizzesTaken { get; set; }
        public List<FieldCountItem> InterestDistribution { get; set; }

        public AdminCareerOverviewViewModel()
        {
            InterestDistribution = new List<FieldCountItem>();
        }
    }

    public class AdminRequirementRow
    {
        public int SubjectId { get; set; }
        public int MinimumLevel { get; set; }
        public bool IsCompulsory { get; set; }

        public AdminRequirementRow()
        {
            MinimumLevel = 4;
            IsCompulsory = true;
        }
    }

    public class AdminCareerFormViewModel
    {
        public int Id { get; set; }
        public int CareerFieldId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypicalQualification { get; set; }
        public string WhereToStudy { get; set; }
        public int MinimumAps { get; set; }
        public bool IsActive { get; set; }

        public List<AdminRequirementRow> Requirements { get; set; }
        public List<CareerField> Fields { get; set; }
        public List<Subject> Subjects { get; set; }

        public AdminCareerFormViewModel()
        {
            IsActive = true;
            MinimumAps = 30;
            Requirements = new List<AdminRequirementRow>();
            Fields = new List<CareerField>();
            Subjects = new List<Subject>();
        }
    }
}
