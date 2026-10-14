using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[DomainExceptionFilter]
[Route("admin/orders")]
public sealed class AdminOrdersController(IAdminOrderQueries orders, IShippingService shipping) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var queue = await orders.GetShippingQueueAsync(cancellationToken);

        return View(queue);
    }

    [HttpPost("{id:guid}/start-picking")]
    public async Task<IActionResult> StartPicking(Guid id, CancellationToken cancellationToken)
    {
        await shipping.StartPickingAsync(id, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, string trackingCode, CancellationToken cancellationToken)
    {
        await shipping.ShipAsync(id, trackingCode, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken cancellationToken)
    {
        await shipping.DeliverAsync(id, cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
