using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ElevateED.ViewModels
{
    public class PastPaperViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string Year { get; set; }
        public string Term { get; set; }
        public string ExamType { get; set; }
        public string Description { get; set; }
        public string FilePath { get; set; }
        public string MemoPath { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
        public int DownloadCount { get; set; }
    }

    public class UploadPastPaperViewModel
    {
        [Required]
        [Display(Name = "Title")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [Required]
        [Display(Name = "Grade")]
        public string Grade { get; set; }

        [Required]
        [Display(Name = "Year")]
        public string Year { get; set; }

        [Required]
        [Display(Name = "Term")]
        public string Term { get; set; }

        [Required]
        [Display(Name = "Exam Type")]
        public string ExamType { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Past Paper File (PDF)")]
        public HttpPostedFileBase PaperFile { get; set; }

        [Display(Name = "Memo File (PDF) - Optional")]
        public HttpPostedFileBase MemoFile { get; set; }
    }

    public class GradeCardViewModel
    {
        public string Grade { get; set; }
        public int PaperCount { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
    }

    public class PastPaperListViewModel
    {
        public string Grade { get; set; }
        public List<PastPaperDisplayViewModel> Papers { get; set; }
        public List<string> Subjects { get; set; }
        public string SelectedSubject { get; set; }
        public string SelectedYear { get; set; }
        public string SelectedTerm { get; set; }
    }

    public class PastPaperDisplayViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Subject { get; set; }
        public int Year { get; set; }
        public string Term { get; set; }
        public string ExamType { get; set; }
        public string Description { get; set; }
        public string FilePath { get; set; }
        public string MemoPath { get; set; }
    }
}