using ElevateED.Models.ViewModels;

namespace ElevateED.Services
{
    /// <summary>
    /// Service interface for attendance management operations.
    /// </summary>
    public interface IAttendanceService
    {
        /// <summary>
        /// Start a new attendance session with OTP generation.
        /// </summary>
        Models.AttendanceSession StartSession(int classId, string teacherId);

        /// <summary>
        /// Submit OTP and mark student as present.
        /// </summary>
        bool SubmitOTP(string otpCode, int studentId);

        /// <summary>
        /// Get edit view model for manual attendance override.
        /// </summary>
        EditAttendanceViewModel GetEditViewModel(int sessionId);

        /// <summary>
        /// Save manual attendance overrides.
        /// </summary>
        void SaveManualOverrides(EditAttendanceViewModel model);

        /// <summary>
        /// Get analytics data for attendance statistics.
        /// </summary>
        AnalyticsViewModel GetAnalytics(string filter, int? classId, string teacherId);
    }
}
