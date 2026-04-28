using System.ComponentModel.DataAnnotations;

namespace ElevateED.Models.ViewModels
{
    /// <summary>
    /// ViewModel for students to mark attendance via OTP.
    /// </summary>
    public class MarkAttendanceViewModel
    {
        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must contain only digits")]
        [Display(Name = "Enter 6-Digit OTP")]
        public string OTPInput { get; set; }

        public string ErrorMessage { get; set; }

        public string SuccessMessage { get; set; }
    }
}
