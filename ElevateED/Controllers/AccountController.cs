using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Security.Cryptography;
using System.Text;
using ElevateED.Models;
using ElevateED.ViewModels;

namespace ElevateED.Controllers
{
    public class AccountController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == model.StudentNumber);

            if (user != null && VerifyPassword(model.Password, user.PasswordHash))
            {
                user.LastLogin = DateTime.Now;
                _context.SaveChanges();

                var authTicket = new FormsAuthenticationTicket(
                    1,
                    user.StudentNumber,
                    DateTime.Now,
                    DateTime.Now.AddMinutes(30),
                    model.RememberMe,
                    user.Role.ToString()
                );

                string encryptedTicket = FormsAuthentication.Encrypt(authTicket);
                var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
                Response.Cookies.Add(authCookie);

                if ((user.Role == UserRole.Student || user.Role == UserRole.Teacher) && !user.HasChangedPassword)
                {
                    return RedirectToAction("ChangePassword", "Account", new { firstLogin = true });
                }

                if (user.Role == UserRole.Admin)
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                if (user.Role == UserRole.Teacher)
                {
                    return RedirectToAction("Dashboard", "Teacher");
                }

                if (user.Role == UserRole.Student)
                {
                    return RedirectToAction("Dashboard", "Student");
                }

                if (user.Role == UserRole.Applicant)
                {
                    return RedirectToAction("PendingApproval", "Application");
                }

                return RedirectToLocal(returnUrl);
            }

            ModelState.AddModelError("", "Invalid student number or password.");
            return View(model);
        }

        [Authorize]
        public ActionResult ChangePassword(bool firstLogin = false)
        {
            ViewBag.FirstLogin = firstLogin;
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword, bool firstLogin = false)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "New password and confirmation do not match.");
                ViewBag.FirstLogin = firstLogin;
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Password must be at least 6 characters.");
                ViewBag.FirstLogin = firstLogin;
                return View();
            }

            var studentNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (user.HasChangedPassword && !VerifyPassword(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                ViewBag.FirstLogin = firstLogin;
                return View();
            }

            // Store new password as hash
            user.PasswordHash = HashPassword(newPassword);
            user.HasChangedPassword = true;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Password changed successfully!";

            if (user.Role == UserRole.Admin)
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (user.Role == UserRole.Teacher)
            {
                return RedirectToAction("Dashboard", "Teacher");
            }
            else if (user.Role == UserRole.Student)
            {
                return RedirectToAction("Dashboard", "Student");
            }
            else if (user.Role == UserRole.Applicant)
            {
                return RedirectToAction("PendingApproval", "Application");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            // Check if it's a hash (Base64 format)
            if (hash != null && hash.Length > 20 && hash.Contains("="))
            {
                // It's a hash - verify normally
                return HashPassword(password) == hash;
            }
            else
            {
                // It's plain text - direct comparison
                return password == hash;
            }
        }
    }
}