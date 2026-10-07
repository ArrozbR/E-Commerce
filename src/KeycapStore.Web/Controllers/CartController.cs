using System.Security.Claims;

using KeycapStore.Application.Carts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
public sealed class CartController(ICartService carts) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(CustomerId, cancellationToken);

        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid productId, CancellationToken cancellationToken)
    {
        await carts.AddItemAsync(CustomerId, productId, 1, cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
