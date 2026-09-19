using System.Web.Helpers;
using System.Web.Mvc;

namespace ElevateED.Filters
{
    // Drop-in replacement for [ValidateAntiForgeryToken] for endpoints called
    // via fetch()/JSON instead of a real HTML form post. A plain form post
    // carries the token as a hidden field (Request.Form["__RequestVerificationToken"]),
    // which is what the stock attribute checks — but a JSON body isn't form
    // data, so that field is always empty and the stock attribute rejects
    // every request. Here the page still renders @Html.AntiForgeryToken()
    // once (any hidden input with that name on the page will do) and the
    // client copies its value into a "RequestVerificationToken" header
    // instead of a form field; this attribute validates against that header.
    public class ValidateJsonAntiForgeryTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var httpContext = filterContext.HttpContext;
            var cookie = httpContext.Request.Cookies[AntiForgeryConfig.CookieName];
            var cookieToken = cookie != null ? cookie.Value : null;
            var headerToken = httpContext.Request.Headers["RequestVerificationToken"];

            AntiForgery.Validate(cookieToken, headerToken);
        }
    }
}
