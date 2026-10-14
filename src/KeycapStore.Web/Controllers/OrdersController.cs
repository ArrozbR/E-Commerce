using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
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

    [HttpGet("orders/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet("orders/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Json(new { status = order.Status.ToString(), text = OrderStatusText.For(order.Status) });
    }
}
