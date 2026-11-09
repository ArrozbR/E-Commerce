using KeycapStore.Application.Notifications;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Route("Newsletter")]
public sealed class NewsletterController(INewsletterService newsletter) : Controller
{
    [HttpGet("Unsubscribe")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Unsubscribe(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrEmpty(token) || !newsletter.IsValidUnsubscribeToken(token))
        {
            return Invalid();
        }

        return View(model: token);
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
