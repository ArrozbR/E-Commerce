using KeycapStore.Application.Identity;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

public sealed class AccountController(IAccountService accounts) : Controller
{
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await accounts.RegisterAsync(model.Email, model.Password, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return RedirectToAction("Index", "Catalog");
    }
}
