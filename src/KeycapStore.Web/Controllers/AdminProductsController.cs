using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[DomainExceptionFilter]
[Route("admin/products")]
public sealed class AdminProductsController(IProductAdminService products) : Controller
{
    private const string InvalidPrice = "Preço inválido: use vírgula para os centavos (ex.: 199,90), sem ponto, maior que zero.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await products.ListAsync(cancellationToken);

        return View(list);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(NewProductViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BackWithError(FirstError());
        }

        if (!model.TryGetPrice(out var price))
        {
            return BackWithError(InvalidPrice);
        }

        await products.CreateAsync(model.Name.Trim(), price, model.Stock, cancellationToken);

        return BackWithMessage($"Produto \"{model.Name.Trim()}\" criado.");
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BackWithError(FirstError());
        }

        if (!model.TryGetPrice(out var price))
        {
            return BackWithError(InvalidPrice);
        }

        await products.UpdateAsync(id, model.Name.Trim(), price, cancellationToken);

        return BackWithMessage($"Produto \"{model.Name.Trim()}\" atualizado.");
    }

    [HttpPost("{id:guid}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(Guid id, int delta, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || delta == 0 || delta < -AdminLimits.MaxStockChange || delta > AdminLimits.MaxStockChange)
        {
            return BackWithError($"Informe um ajuste diferente de zero, entre -{AdminLimits.MaxStockChange} e {AdminLimits.MaxStockChange}.");
        }

        var result = await products.AdjustStockAsync(id, delta, cancellationToken);

        return result == StockAdjustmentResult.Adjusted
            ? BackWithMessage($"Estoque ajustado em {delta:+#;-#}.")
            : BackWithError("Ajuste recusado: o estoque ficaria negativo.");
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await products.ActivateAsync(id, cancellationToken);

        return BackWithMessage("Produto de volta à venda.");
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await products.DeactivateAsync(id, cancellationToken);

        return BackWithMessage("Produto tirado de venda.");
    }

    private string FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => m.Length > 0)
        ?? "Dados inválidos.";

    private RedirectToActionResult BackWithMessage(string message)
    {
        TempData["Message"] = message;
        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult BackWithError(string error)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Index));
    }
}
