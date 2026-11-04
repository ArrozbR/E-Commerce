using System.Security.Claims;

using KeycapStore.Application.Carts;
using KeycapStore.Domain.Carts;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
public sealed class CartController(ICartService carts, IStringLocalizer<SharedResource> text) : Controller
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
        try
        {
            await carts.AddItemAsync(CustomerId, productId, 1, cancellationToken);
        }
        catch (PreOrderMustBeAloneException)
        {
            TempData["Error"] = text["Kits de pré-venda são comprados sozinhos. Finalize ou esvazie o carrinho antes de adicionar este."].Value;
        }
        catch (CartItemLimitException)
        {
            TempData["Error"] = LimitMessage();
        }
        catch (CartStockLimitException ex)
        {
            TempData["Error"] = StockMessage(ex.Available);
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    public async Task<IActionResult> ChangeQuantity(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            await carts.ChangeQuantityAsync(CustomerId, productId, quantity, cancellationToken);
        }
        catch (CartItemLimitException)
        {
            TempData["Error"] = LimitMessage();
        }
        catch (CartStockLimitException ex)
        {
            TempData["Error"] = StockMessage(ex.Available);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken cancellationToken)
    {
        await carts.RemoveItemAsync(CustomerId, productId, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private string LimitMessage() => text["Cada kit vai no máximo {0} unidades por pedido.", Cart.MaxQuantityPerItem].Value;

    private string StockMessage(int available) => available > 0
        ? text["Só há {0} unidades deste kit disponíveis.", available].Value
        : text["Este kit está esgotado."].Value;
}
