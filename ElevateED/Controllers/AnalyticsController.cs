using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using ElevateED.ViewModels;
using ElevateED.Services;

namespace ElevateED.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly IAttendanceService _attendanceService;

        public AnalyticsController()
        {
            _attendanceService = new AttendanceService();
        }

        [HttpGet]
        [Authorize(Roles = "Teacher,Admin")]
        public ActionResult Index(string filter = "weekly", int? classId = null)
        {
            string teacherId = null;

            using (var db = new ElevateEDContext())
            {
                var studentNumber = User.Identity.Name;

                if (User.IsInRole("Teacher"))
                {
                    var user = db.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);
                    if (user != null)
                    {
                        teacherId = user.StudentNumber;
                    }
                }
            }

            var viewModel = _attendanceService.GetAnalytics(filter, classId, teacherId);
            return View(viewModel);
        }
    }
}