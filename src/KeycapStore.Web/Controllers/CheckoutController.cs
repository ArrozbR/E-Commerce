using System.Security.Claims;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Web.Filters;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Authorize]
[DomainExceptionFilter]
public sealed class CheckoutController(ICheckoutService checkout, ISavedAddressService savedAddresses) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var saved = await savedAddresses.GetAsync(CustomerId, cancellationToken);
        if (saved is null)
        {
            return View();
        }

        ViewData["FromSavedAddress"] = true;
        return View(new CheckoutViewModel
        {
            RecipientName = saved.RecipientName,
            Street = saved.Street,
            Number = saved.Number,
            Complement = saved.Complement,
            District = saved.District,
            City = saved.City,
            State = saved.State,
            PostalCode = saved.PostalCode,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var address = new ShippingAddress(
            model.RecipientName, model.Street, model.Number, model.Complement,
            model.District, model.City, model.State, model.PostalCode);

        var result = await checkout.PlaceOrderAsync(CustomerId, address, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        if (model.SaveAddress)
        {
            await savedAddresses.SaveAsync(CustomerId, address, cancellationToken);
        }

        var orderId = result.OrderId!.Value;
        var successUrl = new Uri(Url.Action("Return", "Orders", new { id = orderId }, Request.Scheme)!);
        var cancelUrl = new Uri(Url.Action("Index", "Cart", null, Request.Scheme)!);

        var payment = await checkout.StartPaymentAsync(orderId, CustomerId, successUrl, cancelUrl, cancellationToken);

        if (!payment.Succeeded)
        {
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        return Redirect(payment.PaymentUrl!.ToString());
    }
}
