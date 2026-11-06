using KeycapStore.Application.Catalog;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

// Controller FINO: não sabe nada de banco. Só pede a lista à "porta" e entrega para a página.
public sealed class CatalogController(ICatalogQueries catalog) : Controller
{
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

        return View(new ProductPageViewModel(product, RelatedKits.Pick(product, products, 3)));
    }
}
