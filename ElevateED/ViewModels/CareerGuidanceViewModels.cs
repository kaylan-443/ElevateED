using System.Collections.Generic;
using ElevateED.Models;
using ElevateED.Services;

namespace ElevateED.ViewModels
{
    // "My APS" dashboard landing page.
    public class CareerDashboardViewModel
    {
        public string StudentName { get; set; }
        public string Grade { get; set; }
        public ApsResult Aps { get; set; }
        public int QualifyingCount { get; set; }
        public int CloseCount { get; set; }
        public int BookmarkCount { get; set; }
        public bool HasTakenQuiz { get; set; }
        public List<string> TopFieldNames { get; set; }

        public CareerDashboardViewModel()
        {
            TopFieldNames = new List<string>();
        }
    }

    // "What should I work on first" breakdown for one career the learner
    // doesn't yet qualify for.
    public class PrioritySubjectsViewModel
    {
        public Career Career { get; set; }
        public CareerMatchResult Match { get; set; }
        public PriorityBreakdown Breakdown { get; set; }
    }

    // Side-by-side comparison of 2-3 shortlisted careers.
    public class CareerCompareViewModel
    {
        public CareerComparison Comparison { get; set; }
        public ApsResult Aps { get; set; }
    }

    // Admission outcome simulator: sliders react instantly using the payload
    // serialised into the page, so the controller only needs to hand it over.
    public class SimulatorViewModel
    {
        public string StudentName { get; set; }
        public SimulatorPayload Payload { get; set; }
        public string PayloadJson { get; set; }
        public bool HasBookmarks { get; set; }
    }

    // Career explorer with filtering and match verdicts.
    public class CareerExplorerViewModel
    {
        public List<CareerField> Fields { get; set; }
        public int? SelectedFieldId { get; set; }
        public string VerdictFilter { get; set; } // "", "qualifies", "close"
        public List<CareerMatchResult> Matches { get; set; }
        public HashSet<int> BookmarkedCareerIds { get; set; }
        public bool HasReportCard { get; set; }
        public int StudentAps { get; set; }

        public CareerExplorerViewModel()
        {
            Fields = new List<CareerField>();
            Matches = new List<CareerMatchResult>();
            BookmarkedCareerIds = new HashSet<int>();
        }
    }

    // A single career's detail page.
    public class CareerDetailViewModel
    {
        public CareerMatchResult Match { get; set; }
        public ApsResult Aps { get; set; }
        public bool IsBookmarked { get; set; }
        // All requirements including optional/recommended ones for display.
        public List<CareerSubjectRequirement> AllRequirements { get; set; }

        public CareerDetailViewModel()
        {
            AllRequirements = new List<CareerSubjectRequirement>();
        }
    }

    // Interest quiz.
    public class InterestQuizViewModel
    {
        public List<InterestQuestion> Questions { get; set; }

        public InterestQuizViewModel()
        {
            Questions = new List<InterestQuestion>();
        }
    }

    public class InterestAnswer
    {
        public int QuestionId { get; set; }
        public int Score { get; set; } // 1 (Strongly Disagree) .. 5 (Strongly Agree)
    }

    public class InterestQuizSubmission
    {
        public List<InterestAnswer> Answers { get; set; }

        public InterestQuizSubmission()
        {
            Answers = new List<InterestAnswer>();
        }
    }

    // Interest quiz result: top fields plus recommended careers.
    public class InterestResultViewModel
    {
        public List<CareerField> TopFields { get; set; }
        public List<CareerMatchResult> RecommendedCareers { get; set; }
        public HashSet<int> BookmarkedCareerIds { get; set; }
        public bool HasReportCard { get; set; }

        public InterestResultViewModel()
        {
            TopFields = new List<CareerField>();
            RecommendedCareers = new List<CareerMatchResult>();
            BookmarkedCareerIds = new HashSet<int>();
        }
    }

    // Bookmarks page.
    public class CareerBookmarksViewModel
    {
        public List<CareerMatchResult> Matches { get; set; }
        public bool HasReportCard { get; set; }

        public CareerBookmarksViewModel()
        {
            Matches = new List<CareerMatchResult>();
        }
    }
}
