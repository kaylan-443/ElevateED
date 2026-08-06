using System;
using System.Collections.Generic;
using System.Linq;

namespace ElevateED.Services
{
    /// <summary>
    /// Hard constraints on when exam sessions may be scheduled during a school day.
    /// Centralised here so it's easy to tweak per school. If the school later wants
    /// these configurable per term/grade, swap this for a DB-backed lookup.
    /// </summary>
    public static class SchoolSchedule
    {
        public static readonly TimeSpan SchoolStart = new TimeSpan(7, 30, 0);
        public static readonly TimeSpan SchoolEnd = new TimeSpan(14, 30, 0);

        // Break windows during which no exam may be scheduled and no exam may overlap.
        public static readonly IReadOnlyList<BreakWindow> Breaks = new List<BreakWindow>
        {
            new BreakWindow("First Break", new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0)),
            new BreakWindow("Lunch",       new TimeSpan(12, 30, 0), new TimeSpan(13, 15, 0))
        };

        /// <summary>
        /// Returns null when the candidate slot is valid; otherwise returns a human-
        /// readable reason why it is not.
        /// </summary>
        public static string ValidateSlot(TimeSpan start, TimeSpan end)
        {
            if (end <= start) return "End time must be after start time.";
            if (start < SchoolStart) return $"Exams cannot start before {SchoolStart:hh\\:mm}.";
            if (end > SchoolEnd) return $"Exam must end by {SchoolEnd:hh\\:mm} (end of school day).";

            foreach (var brk in Breaks)
            {
                if (Overlaps(start, end, brk.Start, brk.End))
                {
                    return $"Exam overlaps the {brk.Name} ({brk.Start:hh\\:mm}–{brk.End:hh\\:mm}). Move it earlier or later.";
                }
            }
            return null;
        }

        public static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        {
            return aStart < bEnd && bStart < aEnd;
        }
    }

    public class BreakWindow
    {
        public string Name { get; }
        public TimeSpan Start { get; }
        public TimeSpan End { get; }
        public BreakWindow(string name, TimeSpan start, TimeSpan end)
        {
            Name = name;
            Start = start;
            End = end;
        }
    }
}
