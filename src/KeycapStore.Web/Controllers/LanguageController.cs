using KeycapStore.Web.Localization;

using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

public sealed class LanguageController : Controller
{
    [HttpPost]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        if (!SiteCultures.All.Contains(culture))
        {
            culture = SiteCultures.Portuguese;
        }

        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture!)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
            });

        return LocalRedirect(returnUrl!);
    }
}
