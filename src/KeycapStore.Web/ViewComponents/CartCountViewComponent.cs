using System.Security.Claims;

using KeycapStore.Application.Carts;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.ViewComponents;

public sealed class CartCountViewComponent(ICartService carts) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var customerId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = customerId is null ? 0 : await carts.CountItemsAsync(customerId, HttpContext.RequestAborted);

        return View(count);
    }
}
