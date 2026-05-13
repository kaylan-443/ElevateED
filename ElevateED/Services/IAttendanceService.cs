using ElevateED.Models;
using ElevateED.ViewModels;


namespace ElevateED.Services
{
    public interface IAttendanceService
    {
        AttendanceSession StartSession(int classId, int teacherId);
        bool SubmitOTP(string otpCode, int studentId);
        string ScanQRCode(string qrCode, int studentId);  // ADD THIS
        EditAttendanceViewModel GetEditViewModel(int sessionId);
        void SaveManualOverrides(EditAttendanceViewModel model);
        AnalyticsViewModel GetAnalytics(string filter, int? classId, string teacherId);
    }

}