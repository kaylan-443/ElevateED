// ClassRegisterViewModel.cs
using System;

namespace ElevateED.ViewModels
{
    public class ClassRegisterViewModel
    {
        public int Id { get; set; }
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string Term { get; set; }
        public int Year { get; set; }
        public string FilePath { get; set; }
        public string Description { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class UploadClassRegisterViewModel
    {
        public string Grade { get; set; }
        public string ClassName { get; set; }
        public string Term { get; set; }
        public int Year { get; set; }
        public string Description { get; set; }
        public System.Web.HttpPostedFileBase RegisterFile { get; set; }
    }
}