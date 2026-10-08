using System.Security.Claims;

using KeycapStore.Application.Orders;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
public sealed class OrdersController(IOrderQueries orders) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }
}
