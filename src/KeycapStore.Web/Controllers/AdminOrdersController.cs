using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[DomainExceptionFilter]
[Route("admin/orders")]
public sealed class AdminOrdersController(IAdminOrderQueries orders) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var queue = await orders.GetShippingQueueAsync(cancellationToken);

        return View(queue);
    }
}
