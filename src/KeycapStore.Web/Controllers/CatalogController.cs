using System.Globalization;
using System.Security.Claims;

using KeycapStore.Application.Catalog;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

public sealed class CatalogController(ICatalogQueries catalog, IStockAlertService alerts, IStringLocalizer<SharedResource> text) : Controller
{
    public const string AlertMessageKey = "StockAlertMessage";

    public async Task<IActionResult> Index(CatalogFilter filter, CancellationToken cancellationToken)
    {
        var products = await catalog.ListProductsAsync(cancellationToken);

        return View(new CatalogPageViewModel(filter, filter.Apply(products), CatalogFilter.ColorwaysOf(products)));
    }

    [HttpGet("Catalog/{id:guid}/{slug?}")]
    public async Task<IActionResult> Details(Guid id, string? slug, CancellationToken cancellationToken)
    {
        var products = await catalog.ListProductsAsync(cancellationToken);
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        var canonical = Slug.From(product.Name);
        if ((slug ?? "") != canonical)
        {
            return RedirectToActionPermanent(nameof(Details), new { id, slug = canonical });
        }

        var page = new ProductPageViewModel(product, RelatedKits.Pick(product, products, 3));
        if (page.OffersAlert && User.Identity?.IsAuthenticated == true)
        {
            page = page with { AlertRequested = await alerts.IsSubscribedAsync(CustomerId, id, cancellationToken) };
        }

        return View(page);
    }

    [Authorize]
    [DomainExceptionFilter]
    [HttpPost("Catalog/{id:guid}/alert")]
    public async Task<IActionResult> RequestAlert(Guid id, CancellationToken cancellationToken)
    {
        var result = await alerts.SubscribeAsync(CustomerId, id, CultureInfo.CurrentUICulture.Name, cancellationToken);

        TempData[AlertMessageKey] = result == StockAlertResult.NotAllowed
            ? text["Este kit não recebe aviso agora: ele já está à venda ou não é um kit normal."].Value
            : text["Pronto! Avisamos você por e-mail quando este kit voltar."].Value;

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpPost("Catalog/{id:guid}/alert/cancel")]
    public async Task<IActionResult> CancelAlert(Guid id, CancellationToken cancellationToken)
    {
        await alerts.UnsubscribeAsync(CustomerId, id, cancellationToken);

        TempData[AlertMessageKey] = text["Aviso cancelado."].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
