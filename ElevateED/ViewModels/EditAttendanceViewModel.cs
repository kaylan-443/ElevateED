using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models.ViewModels
{
    /// <summary>
    /// ViewModel for editing attendance records with manual overrides.
    /// </summary>
    public class EditAttendanceViewModel
    {
        public int SessionId { get; set; }

        [Display(Name = "Class Name")]
        public string ClassName { get; set; }

        [Display(Name = "Session Date")]
        public DateTime SessionDate { get; set; }

        public List<AttendanceEditRow> Records { get; set; } = new List<AttendanceEditRow>();
    }

    /// <summary>
    /// Represents a single attendance record row for editing.
    /// </summary>
    public class AttendanceEditRow
    {
        public int RecordId { get; set; }

        public int StudentId { get; set; }

        [Display(Name = "Student Name")]
        public string FullName { get; set; }

        [Display(Name = "Present")]
        public bool IsPresent { get; set; }
    }
}
