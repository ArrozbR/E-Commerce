using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
public sealed class CheckoutController(ICheckoutService checkout) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var address = new ShippingAddress(
            model.RecipientName, model.Street, model.Number, model.Complement,
            model.District, model.City, model.State, model.PostalCode);

        var result = await checkout.PlaceOrderAsync(CustomerId, address, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        return RedirectToAction("Details", "Orders", new { id = result.OrderId });
    }
}
