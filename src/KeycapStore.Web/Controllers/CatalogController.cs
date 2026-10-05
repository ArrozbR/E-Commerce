using KeycapStore.Application.Catalog;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

// Controller FINO: não sabe nada de banco. Só pede a lista à "porta" e entrega para a página.
public sealed class CatalogController(ICatalogQueries catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await catalog.ListProductsAsync(cancellationToken);

        return View(products);
    }
}
