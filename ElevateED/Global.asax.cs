using ElevateED.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace ElevateED
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Initialize database with admin account
            DatabaseConfig.Initialize();
        }
        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                try
                {
                    var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                    if (authTicket != null && !string.IsNullOrEmpty(authTicket.UserData))
                    {
                        var roles = new[] { authTicket.UserData };
                        var principal = new System.Security.Principal.GenericPrincipal(
                            new FormsIdentity(authTicket), roles);
                        Context.User = principal;
                    }
                }
                catch (CryptographicException)
                {
                    FormsAuthentication.SignOut();
                    var expiredCookie = new HttpCookie(FormsAuthentication.FormsCookieName)
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(expiredCookie);
                }
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
            if (exception is HttpAntiForgeryException || exception?.InnerException is HttpAntiForgeryException)
            {
                Server.ClearError();

                var redirectUrl = "~/Account/Login";
                if (Request.UrlReferrer != null && Request.Url != null && Request.UrlReferrer.Host == Request.Url.Host)
                {
                    redirectUrl = Request.UrlReferrer.PathAndQuery;
                }

                Response.Redirect(redirectUrl, false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

    }

}
