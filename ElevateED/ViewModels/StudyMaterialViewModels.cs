using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ElevateED.ViewModels
{
    public class StudyMaterialUploadViewModel
    {
        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        // NEW - Use IDs instead of strings
        public int SubjectId { get; set; }
        public int GradeId { get; set; }

        // Keep for backward compatibility
        public string Subject { get; set; }
        public string GradeLevel { get; set; }

        [Required]
        public HttpPostedFileBase File { get; set; }
    }

    public class StudyMaterialListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Subject { get; set; }
        public string GradeLevel { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedDate { get; set; }
        public int DownloadCount { get; set; }
    }

    public class SubjectMaterialViewModel
    {
        public string SubjectName { get; set; }
        public int MaterialCount { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
    }
}