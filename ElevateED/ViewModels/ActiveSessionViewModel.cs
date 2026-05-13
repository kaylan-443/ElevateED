using System;

namespace ElevateED.ViewModels
{
    public class ActiveSessionViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; }
        public DateTime SessionDate { get; set; }
        public string OTPCode { get; set; }
        public string QRCode { get; set; }
        public DateTime? QRCodeExpiry { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public bool IsExpired { get; set; }
        public string TimeRemaining { get; set; }  // Remove the expression body, make it a regular property
    }
}
