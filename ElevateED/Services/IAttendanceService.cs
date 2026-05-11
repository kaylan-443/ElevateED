using ElevateED.ViewModels;


namespace ElevateED.Services
{
    public interface IAttendanceService
    {
        Models.AttendanceSession StartSession(int classId, int teacherId);
        bool SubmitOTP(string otpCode, int studentId);
        EditAttendanceViewModel GetEditViewModel(int sessionId);
        void SaveManualOverrides(EditAttendanceViewModel model);
        AnalyticsViewModel GetAnalytics(string filter, int? classId, string teacherId);
    }
}