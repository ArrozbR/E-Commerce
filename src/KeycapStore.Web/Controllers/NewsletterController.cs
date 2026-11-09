using KeycapStore.Application.Notifications;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Route("Newsletter")]
public sealed class NewsletterController(INewsletterService newsletter) : Controller
{
    [HttpGet("Unsubscribe")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Unsubscribe(string? token, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrEmpty(token) || await newsletter.EmailForUnsubscribeTokenAsync(token, cancellationToken) is not { } email)
        {
            return Invalid();
        }

        return View(new UnsubscribePageViewModel(token, EmailMask.Mask(email)));
    }

    [HttpPost("Unsubscribe")]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ConfirmUnsubscribe(string? token, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrEmpty(token) || !await newsletter.UnsubscribeWithTokenAsync(token, cancellationToken))
        {
            return Invalid();
        }

        return View("Unsubscribed");
    }

    private ViewResult Invalid()
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("UnsubscribeInvalid");
    }
}
