using System.Security.Claims;

using KeycapStore.Application.Carts;
using KeycapStore.Domain.Carts;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
public sealed class CartController(ICartService carts, IStringLocalizer<SharedResource> text) : Controller
{
    public const string CartAddedKey = "CartAdded";

    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private bool WantsJson => Request.GetTypedHeaders().Accept
        .Any(a => a.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase));

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(CustomerId, cancellationToken);

        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid productId, string? returnUrl, CancellationToken cancellationToken)
    {
        string? refusal = null;
        try
        {
            await carts.AddItemAsync(CustomerId, productId, 1, cancellationToken);
        }
        catch (PreOrderMustBeAloneException)
        {
            refusal = text["Kits de pré-venda são comprados sozinhos. Finalize ou esvazie o carrinho antes de adicionar este."].Value;
        }
        catch (CartItemLimitException)
        {
            refusal = LimitMessage();
        }
        catch (CartStockLimitException ex)
        {
            refusal = StockMessage(ex.Available);
        }

        if (WantsJson)
        {
            return refusal is null
                ? Ok(new { count = await carts.CountItemsAsync(CustomerId, cancellationToken) })
                : Conflict(new { message = refusal });
        }

        if (refusal is not null)
        {
            TempData["Error"] = refusal;
            return RedirectToAction(nameof(Index));
        }

        if (!Url.IsLocalUrl(returnUrl))
        {
            return RedirectToAction(nameof(Index));
        }

        TempData[CartAddedKey] = true;
        return LocalRedirect(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeQuantity(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        string? refusal = null;
        try
        {
            await carts.ChangeQuantityAsync(CustomerId, productId, quantity, cancellationToken);
        }
        catch (CartItemLimitException)
        {
            refusal = LimitMessage();
        }
        catch (CartStockLimitException ex)
        {
            refusal = StockMessage(ex.Available);
        }

        if (WantsJson)
        {
            return refusal is null ? await CartStateAsync(cancellationToken) : Conflict(new { message = refusal });
        }

        if (refusal is not null)
        {
            TempData["Error"] = refusal;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken cancellationToken)
    {
        await carts.RemoveItemAsync(CustomerId, productId, cancellationToken);

        return WantsJson ? await CartStateAsync(cancellationToken) : RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> CartStateAsync(CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(CustomerId, cancellationToken);

        return Ok(new
        {
            count = cart.Units,
            total = Display.Price(cart.Total),
            hasUnavailableItems = cart.HasUnavailableItems,
            lines = cart.Lines.Select(line => new
            {
                productId = line.ProductId,
                quantity = line.Quantity,
                max = line.MaxQuantity,
                lineTotal = Display.Price(line.LineTotal),
            }),
        });
    }

    private string LimitMessage() => text["Cada kit vai no máximo {0} unidades por pedido.", Cart.MaxQuantityPerItem].Value;

    private string StockMessage(int available) => available > 0
        ? text["Só há {0} unidades deste kit disponíveis.", available].Value
        : text["Este kit está esgotado."].Value;
}
