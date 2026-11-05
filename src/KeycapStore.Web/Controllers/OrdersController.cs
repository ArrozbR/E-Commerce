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
public sealed class OrdersController(
    IOrderQueries orders,
    ICancellationService cancellations,
    IWithdrawalService withdrawals,
    IStringLocalizer<SharedResource> text) : Controller
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("orders")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await orders.ListForCustomerAsync(CustomerId, cancellationToken);

        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet("orders/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet("orders/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetForCustomerAsync(id, CustomerId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Json(new { status = order.Status.ToString(), text = text[OrderStatusText.For(order.Status)].Value, tone = OrderStatusText.Tone(order.Status) });
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await cancellations.CancelAsync(id, CustomerId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("orders/{id:guid}/cancel-payment")]
    public async Task<IActionResult> CancelPayment(Guid id, CancellationToken cancellationToken)
    {
        var result = await cancellations.CancelPaymentAsync(id, CustomerId, cancellationToken);

        TempData["Message"] = (result switch
        {
            CancelPaymentResult.Requested => text["Pagamento cancelado. Em alguns segundos o pedido aparece como expirado, os itens voltam para a loja e você já pode fazer outro pedido."],
            CancelPaymentResult.NotAwaitingPayment => text["Este pedido não está mais aguardando pagamento."],
            _ => text["Não foi possível cancelar o pagamento agora. Tente de novo em alguns minutos."],
        }).Value;

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("orders/{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        await withdrawals.WithdrawAsync(id, CustomerId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }
}
