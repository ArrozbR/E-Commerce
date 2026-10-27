using System.Globalization;

using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[DomainExceptionFilter]
[Route("admin/products")]
public sealed class AdminProductsController(IProductAdminService products, IStringLocalizer<SharedResource> text) : Controller
{
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
            return BackWithError(text["Preço inválido: use {0} para os centavos (ex.: {1}), sem {2}, maior que zero.", text["vírgula"], ProductFormViewModel.FormatPrice(199.90m), text["ponto"]].Value);
        }

        await products.CreateAsync(model.Name.Trim(), price, model.Stock, cancellationToken);

        return BackWithMessage(text["Produto \"{0}\" criado.", model.Name.Trim()].Value);
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
            return BackWithError(text["Preço inválido: use {0} para os centavos (ex.: {1}), sem {2}, maior que zero.", text["vírgula"], ProductFormViewModel.FormatPrice(199.90m), text["ponto"]].Value);
        }

        await products.UpdateAsync(id, model.Name.Trim(), price, cancellationToken);

        return BackWithMessage(text["Produto \"{0}\" atualizado.", model.Name.Trim()].Value);
    }

    [HttpPost("{id:guid}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(Guid id, int delta, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || delta == 0 || delta < -AdminLimits.MaxStockChange || delta > AdminLimits.MaxStockChange)
        {
            return BackWithError(text["Informe um ajuste diferente de zero, entre -{0} e {0}.", AdminLimits.MaxStockChange].Value);
        }

        var result = await products.AdjustStockAsync(id, delta, cancellationToken);

        return result == StockAdjustmentResult.Adjusted
            ? BackWithMessage(text["Estoque ajustado em {0}.", delta.ToString("+#;-#", CultureInfo.InvariantCulture)].Value)
            : BackWithError(text["Ajuste recusado: o estoque ficaria negativo."].Value);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await products.ActivateAsync(id, cancellationToken);

        return BackWithMessage(text["Produto de volta à venda."].Value);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await products.DeactivateAsync(id, cancellationToken);

        return BackWithMessage(text["Produto tirado de venda."].Value);
    }

    private string FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => m.Length > 0)
        ?? text["Dados inválidos."].Value;

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
