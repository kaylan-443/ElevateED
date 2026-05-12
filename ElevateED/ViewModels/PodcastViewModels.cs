using ElevateED.Models;
using System.Collections.Generic;

namespace ElevateED.ViewModels
{
    public class PodcastIndexViewModel
    {
        public List<PodcastHistory> PodcastHistory { get; set; }
    }

    public class PodcastUploadViewModel
    {
        public string Title { get; set; }
        public string NotesText { get; set; }
    }
}