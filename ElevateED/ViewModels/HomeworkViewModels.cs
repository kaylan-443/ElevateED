// HomeworkViewModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ElevateED.ViewModels
{
    public class SubjectGradeViewModel
    {
        public string Subject { get; set; }
        public List<string> Grades { get; set; }
        public List<string> Classes { get; set; }
    }

    public class UploadHomeworkViewModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Instructions { get; set; }

        // NEW - Use IDs instead of strings
        public int SubjectId { get; set; }
        public int ClassId { get; set; }

        // Keep for backward compatibility
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public HttpPostedFileBase File { get; set; }
    }

    public class UploadClassworkViewModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Instructions { get; set; }

        // NEW - Use IDs instead of strings
        public int SubjectId { get; set; }
        public int ClassId { get; set; }

        // Keep for backward compatibility
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }

        [Required]
        public HttpPostedFileBase File { get; set; }
    }

    public class HomeworkListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Instructions { get; set; }
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public DateTime DueDate { get; set; }
        public string DueDateFormatted { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class ClassworkListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Instructions { get; set; }
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}