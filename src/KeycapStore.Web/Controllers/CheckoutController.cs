using System.Globalization;
using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
public sealed class CheckoutController(
    ICheckoutService checkout,
    ISavedAddressService savedAddresses,
    IOrderQueries orders,
    IStringLocalizer<SharedResource> text) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new CheckoutViewModel();

        var saved = await savedAddresses.GetAsync(CustomerId, cancellationToken);
        if (saved is not null)
        {
            model.CopyFrom(saved);
            ViewData["FromSavedAddress"] = true;
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var address = model.ToShippingAddress();

        var result = await checkout.PlaceOrderAsync(CustomerId, address, CultureInfo.CurrentUICulture.Name, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, FailureMessage(result));
            return View(model);
        }

        if (model.SaveAddress)
        {
            await savedAddresses.SaveAsync(CustomerId, address, cancellationToken);
        }

        var orderId = result.OrderId!.Value;
        var returnUrl = new Uri(Url.Action("Return", "Orders", new { id = orderId }, Request.Scheme)!);

        var payment = await checkout.StartPaymentAsync(orderId, CustomerId, User.Identity!.Name!, returnUrl, cancellationToken);

        if (!payment.Succeeded)
        {
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        return RedirectToAction(nameof(Pay), new { id = orderId });
    }

    [HttpGet("Checkout/Pay/{id:guid}")]
    public async Task<IActionResult> Pay(Guid id, CancellationToken cancellationToken)
    {
        var form = await checkout.GetPaymentFormAsync(id, CustomerId, cancellationToken);
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (form is null || order is null)
        {
            return RedirectToAction("Details", "Orders", new { id });
        }

        return View(new PaymentPageViewModel(order, form));
    }

    private string FailureMessage(PlaceOrderResult result) => (result.Failure switch
    {
        PlaceOrderFailure.EmptyCart => text["Seu carrinho está vazio."],
        PlaceOrderFailure.OutOfStock => text["{0} não tem estoque suficiente.", result.ProductName!],
        PlaceOrderFailure.NoLongerForSale => text["{0} não está mais à venda. Remova do carrinho para continuar.", result.ProductName!],
        _ => text["Não foi possível finalizar o pedido."],
    }).Value;
}
