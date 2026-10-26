using System.Security.Claims;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

public sealed class AccountController(
    IAccountService accounts,
    IAccountDeletionService deletions,
    ISavedAddressService savedAddresses) : Controller
{
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await accounts.RegisterAsync(model.Email, model.Password, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return RedirectToAction("Index", "Catalog");
    }


    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await accounts.SignInAsync(model.Email, model.Password, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return RedirectToAction("Index", "Catalog");
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await accounts.SignOutAsync(cancellationToken);

        return RedirectToAction("Index", "Catalog");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await LoadSavedAddressAsync(cancellationToken);
        return View();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadSavedAddressAsync(cancellationToken);
            return View(model);
        }

        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await deletions.DeleteAsync(customerId, model.Password, cancellationToken);

        if (result == AccountDeletionResult.Deleted)
        {
            await accounts.SignOutAsync(cancellationToken);
            return RedirectToAction(nameof(Deleted));
        }

        ModelState.AddModelError(string.Empty, result switch
        {
            AccountDeletionResult.WrongPassword => "Senha incorreta.",
            AccountDeletionResult.LockedOut => "Muitas tentativas erradas. Tente de novo em alguns minutos.",
            AccountDeletionResult.IsAdmin => "Contas de administrador não podem ser excluídas por esta página.",
            AccountDeletionResult.HasOpenOrders => "Você tem pedidos em andamento. A conta pode ser excluída quando todos estiverem finalizados, inclusive os 7 dias para desistir depois da entrega.",
            _ => "Não foi possível excluir a conta.",
        });

        await LoadSavedAddressAsync(cancellationToken);
        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RemoveSavedAddress(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await savedAddresses.RemoveAsync(customerId, cancellationToken);

        TempData["Message"] = "Endereço salvo removido.";
        return RedirectToAction(nameof(Delete));
    }

    [HttpGet]
    public IActionResult Deleted() => View();

    private async Task LoadSavedAddressAsync(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ViewData["SavedAddress"] = await savedAddresses.GetAsync(customerId, cancellationToken);
    }
}
