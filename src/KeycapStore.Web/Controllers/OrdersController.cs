using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
public sealed class OrdersController(IOrderQueries orders, ICancellationService cancellations, IWithdrawalService withdrawals) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("orders")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await orders.ListForCustomerAsync(CustomerId, cancellationToken);

        return View(list);
    }

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

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await cancellations.CancelAsync(id, CustomerId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("orders/{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        await withdrawals.WithdrawAsync(id, CustomerId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }
}
