using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace ElevateED.Services
{
    public class PodcastService
    {
        private readonly ElevateEDContext _context;

        public PodcastService()
        {
            _context = new ElevateEDContext();
        }

        public List<PodcastHistory> GetStudentPodcasts(int studentId)
        {
            return _context.PodcastHistories
                .Where(p => p.StudentId == studentId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();
        }

        public PodcastHistory GetPodcast(int podcastId)
        {
            return _context.PodcastHistories.Find(podcastId);
        }

        public int GetStudentId(string studentNumber)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
            if (user == null) return 0;

            var student = _context.Students.FirstOrDefault(s => s.UserId == user.Id);
            return student?.Id ?? 0;
        }
    }
}