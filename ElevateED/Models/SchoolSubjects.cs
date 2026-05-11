using System.Collections.Generic;

namespace ElevateED.Models
{
    public static class SchoolSubjects
    {
        public static List<string> GetAllSubjects()
        {
            return new List<string>
            {
                // Languages
                "English",
                "isiZulu",
                
                // Core Subjects
                "Mathematics",
                "Mathematical Literacy",
                "Life Orientation",
                
                // Sciences
                "Physical Sciences",
                "Life Sciences",
                "Natural Science",
                "Agricultural Sciences",
                
                // Humanities
                "History",
                "Geography",
                "Social Science",
               
                
                // Technology & Creative
                "Information Technology",
                "Computer Applications Technology",
                "Technology",
                "Creative Arts"
            };
        }

        public static string GetSubjectIcon(string subject)
        {
            switch (subject.ToLower())
            {
                case "english":
                    return "fas fa-language";
                case "isizulu":
                    return "fas fa-comments";
                case "mathematics":
                    return "fas fa-calculator";
                case "mathematical literacy":
                    return "fas fa-calculator";
                case "life orientation":
                    return "fas fa-heart";
                case "physical sciences":
                    return "fas fa-atom";
                case "life sciences":
                    return "fas fa-dna";
                case "natural science":
                    return "fas fa-flask";
                case "agricultural sciences":
                    return "fas fa-tractor";
                case "history":
                    return "fas fa-landmark";
                case "geography":
                    return "fas fa-map";
                case "social science":
                    return "fas fa-globe";
                case "information technology":
                    return "fas fa-laptop-code";
                case "computer applications technology":
                    return "fas fa-desktop";
                case "technology":
                    return "fas fa-microchip";
                case "creative arts":
                    return "fas fa-palette";
                default:
                    return "fas fa-book";
            }
        }

        public static string GetSubjectColor(string subject)
        {
            var colors = new[] { "primary", "info", "success", "warning", "danger", "secondary" };
            var hash = subject.GetHashCode();
            return colors[System.Math.Abs(hash) % colors.Length];
        }
    }
}