using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.Models.ViewModels;
using ElevateED.Services;

namespace ElevateED.Controllers
{
    /// <summary>
    /// Controller for displaying attendance analytics and statistics.
    /// </summary>
    public class AnalyticsController : Controller
    {
        private readonly IAttendanceService _attendanceService;

        public AnalyticsController()
        {
            _attendanceService = new AttendanceService();
        }

        /// <summary>
        /// GET: Display attendance analytics with filtering options.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Teacher,Admin")]
        public ActionResult Index(string filter = "weekly", int? classId = null)
        {
            string teacherId = null;

            using (var db = new ElevateEDContext())
            {
                var user = User.Identity.Name;

                // Get teacher info if teacher role
                if (User.IsInRole("Teacher"))
                {
                    var teacher = db.Teachers.FirstOrDefault(t => t.User.Email == user);
                    teacherId = teacher?.User.StudentNumber;
                }
            }

            var viewModel = _attendanceService.GetAnalytics(filter, classId, teacherId);
            return View(viewModel);
        }
    }
}
