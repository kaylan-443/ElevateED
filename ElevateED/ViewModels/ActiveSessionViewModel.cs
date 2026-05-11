using System;

namespace ElevateED.ViewModels
{
    public class ActiveSessionViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public string OTPCode { get; set; }
        public DateTime SessionDate { get; set; }
        public DateTime OTPExpiry { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public bool IsExpired { get; set; }

        public string TimeRemaining
        {
            get
            {
                if (IsExpired) return "Expired";
                var remaining = OTPExpiry - DateTime.Now;
                if (remaining.TotalMinutes < 1)
                    return $"{(int)remaining.TotalSeconds}s remaining";
                return $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s remaining";
            }
        }
    }
}