using System.Diagnostics;

using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[IgnoreAntiforgeryToken]
public sealed class ErrorController : Controller
{
    [Route("Error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int statusCode)
    {
        var code = statusCode is >= 400 and <= 599 ? statusCode : StatusCodes.Status404NotFound;
        Response.StatusCode = code;

        return View(new ErrorPageViewModel(code, code >= 500 ? Activity.Current?.Id ?? HttpContext.TraceIdentifier : null));
    }
}
