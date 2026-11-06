using KeycapStore.Application.Catalog;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

public class HomeController(ICatalogQueries catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await catalog.ListProductsAsync(cancellationToken);

        return View(HomeViewModel.From(products, Random.Shared));
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
