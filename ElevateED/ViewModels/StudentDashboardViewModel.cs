using System;

namespace ElevateED.ViewModels
{
    public class StudentDashboardViewModel
    {
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string ClassTeacher { get; set; }
        public string Status { get; set; }
    }

    public class StudentSettingsViewModel
    {
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string CellPhone { get; set; }
        public string PhysicalAddress { get; set; }
        public string ParentName { get; set; }
        public string ParentCellPhone { get; set; }
        public string ParentEmail { get; set; }
    }
}