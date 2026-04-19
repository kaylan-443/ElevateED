using System;

namespace ElevateED.ViewModels
{
    public class StudentTimetableViewModel
    {
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public bool HasTimetable { get; set; }
        public string TimetableTitle { get; set; }
        public string TimetableDescription { get; set; }
        public string TimetableFilePath { get; set; }
        public DateTime? UploadedDate { get; set; }
    }
}