using System.Globalization;
using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Localization;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    [HttpPost("Checkout/Shipping")]
    public async Task<IActionResult> Shipping(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(CheckoutViewModel.ShippingServiceId));
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        return await WithShippingOptionsAsync(model, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return AddressIsValid() ? await WithShippingOptionsAsync(model, cancellationToken) : View(model);
        }

        var address = model.ToShippingAddress();

        var result = await checkout.PlaceOrderAsync(CustomerId, address, model.ShippingServiceId!.Value, CultureInfo.CurrentUICulture.Name, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, FailureMessage(result.Failure, result.ProductName));
            return result.Failure is PlaceOrderFailure.ShippingOptionGone
                ? await WithShippingOptionsAsync(model, cancellationToken)
                : View(model);
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

    private async Task<IActionResult> WithShippingOptionsAsync(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        var quote = await checkout.QuoteShippingAsync(CustomerId, model.ToShippingAddress().PostalCode, cancellationToken);
        if (quote.Succeeded)
        {
            model.ShippingOptions = quote.Options;
        }
        else
        {
            ModelState.AddModelError(string.Empty, FailureMessage(quote.Failure, null));
        }

        return View(nameof(Index), model);
    }

    private bool AddressIsValid() => ModelState
        .Where(entry => entry.Key != nameof(CheckoutViewModel.ShippingServiceId))
        .All(entry => entry.Value!.ValidationState != ModelValidationState.Invalid);

    private string FailureMessage(PlaceOrderFailure? failure, string? productName) => (failure switch
    {
        PlaceOrderFailure.EmptyCart => text["Seu carrinho está vazio."],
        PlaceOrderFailure.OutOfStock => text["{0} não tem estoque suficiente.", productName!],
        PlaceOrderFailure.NoLongerForSale => text["{0} não está mais à venda. Remova do carrinho para continuar.", productName!],
        PlaceOrderFailure.ShippingUnavailable => text["Não foi possível calcular o frete agora. Tente de novo em alguns minutos."],
        PlaceOrderFailure.NoShippingOption => text["Nenhuma transportadora entrega este pedido neste CEP."],
        PlaceOrderFailure.ShippingOptionGone => text["A opção de frete escolhida não está mais disponível. Escolha outra."],
        _ => text["Não foi possível finalizar o pedido."],
    }).Value;
}
