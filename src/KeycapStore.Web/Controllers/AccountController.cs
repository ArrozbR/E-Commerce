using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Web.Localization;
using KeycapStore.Web.Models;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KeycapStore.Web.Controllers;

public sealed class AccountController(
    IAccountService accounts,
    IAccountDeletionService deletions,
    ISavedAddressService savedAddresses,
    IOrderQueries orders,
    IPersonalDataExport personalData,
    IPasswordResetService passwordResets,
    IStringLocalizer<SharedResource> text) : Controller
{
    private static readonly JsonSerializerOptions ExportOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
                ModelState.AddModelError(string.Empty, text[error].Value);
            }

            return View(model);
        }

        await accounts.SignInAsync(model.Email, model.Password, cancellationToken);

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
                ModelState.AddModelError(string.Empty, text[error].Value);
            }

            return View(model);
        }

        return RedirectToAction("Index", "Catalog");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await passwordResets.RequestAsync(model.Email, CultureInfo.CurrentUICulture.Name, cancellationToken);

        TempData["Message"] = text["Se existir uma conta com esse e-mail, enviamos um link para trocar a senha. Ele vale por 30 minutos; confira também a caixa de spam."].Value;
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ResetPassword(string? userId, string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.Token))
        {
            return View("ResetPasswordInvalid");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await passwordResets.ResetAsync(model.UserId, model.Token, model.NewPassword, CultureInfo.CurrentUICulture.Name, cancellationToken);

        switch (result.Status)
        {
            case PasswordResetStatus.Reset:
                TempData["Message"] = text["Senha trocada. Entre com a senha nova."].Value;
                return RedirectToAction(nameof(Login));
            case PasswordResetStatus.InvalidLink:
                return View("ResetPasswordInvalid");
            default:
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, text[error].Value);
                }

                return View(model);
        }
    }

    [HttpPost]
    public IActionResult ExternalLogin()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(ExternalLoginCallback)) };
        properties.Items["LoginProvider"] = ExternalProviders.Google;

        return Challenge(properties, ExternalProviders.Google);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(CancellationToken cancellationToken)
    {
        var result = await accounts.SignInWithExternalLoginAsync(cancellationToken);
        if (result == ExternalLoginResult.SignedIn)
        {
            return RedirectToAction("Index", "Catalog");
        }

        ModelState.AddModelError(string.Empty, text[result switch
        {
            ExternalLoginResult.EmailAlreadyRegistered => "Já existe uma conta com este e-mail. Entre com a senha e conecte o Google em Minha conta.",
            ExternalLoginResult.AdminNotAllowed => "Contas de administrador entram só com a senha.",
            _ => "Não foi possível entrar com o Google. Tente de novo.",
        }].Value);

        return View(nameof(Login), new LoginViewModel());
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
        await LoadAccountPageAsync(cancellationToken);
        return View();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAccountPageAsync(cancellationToken);
            return View(model);
        }

        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await deletions.DeleteAsync(customerId, model.Confirmation, cancellationToken);

        if (result == AccountDeletionResult.Deleted)
        {
            await accounts.SignOutAsync(cancellationToken);
            return RedirectToAction(nameof(Deleted));
        }

        ModelState.AddModelError(string.Empty, text[result switch
        {
            AccountDeletionResult.WrongPassword => "Senha incorreta.",
            AccountDeletionResult.WrongEmail => "O e-mail digitado não é o desta conta.",
            AccountDeletionResult.LockedOut => "Muitas tentativas erradas. Tente de novo em alguns minutos.",
            AccountDeletionResult.IsAdmin => "Contas de administrador não podem ser excluídas por esta página.",
            AccountDeletionResult.HasOpenOrders => "Você tem pedidos em andamento. A conta pode ser excluída quando todos estiverem finalizados, inclusive os 7 dias para desistir depois da entrega.",
            _ => "Não foi possível excluir a conta.",
        }].Value);

        await LoadAccountPageAsync(cancellationToken);
        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Address(CancellationToken cancellationToken)
    {
        var model = new AddressViewModel();

        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var saved = await savedAddresses.GetAsync(customerId, cancellationToken);
        if (saved is not null)
        {
            model.CopyFrom(saved);
        }

        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Address(AddressViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        await savedAddresses.SaveAsync(customerId, model.ToShippingAddress(), cancellationToken);

        TempData["Message"] = text["Endereço salvo."].Value;
        return RedirectToAction(nameof(Delete));
    }

    [Authorize]
    [HttpPost]
    public IActionResult LinkGoogle()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(LinkGoogleCallback)) };
        properties.Items["LoginProvider"] = ExternalProviders.Google;
        properties.Items["XsrfId"] = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Challenge(properties, ExternalProviders.Google);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> LinkGoogleCallback(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await accounts.LinkExternalLoginAsync(customerId, cancellationToken);

        if (result == LinkExternalLoginResult.Linked)
        {
            TempData["Message"] = text["Conta Google conectada. Agora você também pode entrar com o Google."].Value;
        }
        else
        {
            TempData["Error"] = text[result switch
            {
                LinkExternalLoginResult.AdminNotAllowed => "Contas de administrador entram só com a senha.",
                LinkExternalLoginResult.DifferentEmail => "O e-mail da conta Google precisa ser o mesmo desta conta.",
                LinkExternalLoginResult.LinkedToAnotherAccount => "Esta conta Google já está conectada a outra conta da loja.",
                _ => "Não foi possível conectar o Google. Tente de novo.",
            }].Value;
        }

        return RedirectToAction(nameof(Delete));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RemoveSavedAddress(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await savedAddresses.RemoveAsync(customerId, cancellationToken);

        TempData["Message"] = text["Endereço salvo removido."].Value;
        return RedirectToAction(nameof(Delete));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Password(CancellationToken cancellationToken)
    {
        ViewData["Account"] = await accounts.GetSummaryAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Password(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (ModelState.IsValid)
        {
            var result = await accounts.ChangePasswordAsync(customerId, model.CurrentPassword, model.NewPassword, cancellationToken);
            if (result.Status == ChangePasswordStatus.Changed)
            {
                TempData["Message"] = text["Senha alterada. As outras sessões abertas foram encerradas."].Value;
                return RedirectToAction(nameof(Delete));
            }

            switch (result.Status)
            {
                case ChangePasswordStatus.WrongPassword:
                    ModelState.AddModelError(nameof(model.CurrentPassword), text["Senha atual incorreta."].Value);
                    break;
                case ChangePasswordStatus.LockedOut:
                    ModelState.AddModelError(string.Empty, text["Muitas tentativas erradas. Tente de novo em alguns minutos."].Value);
                    break;
                case ChangePasswordStatus.NoPassword:
                    ModelState.AddModelError(string.Empty, text["Esta conta entra pelo Google e não tem senha para trocar."].Value);
                    break;
                default:
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(nameof(model.NewPassword), text[error].Value);
                    }

                    break;
            }
        }

        ViewData["Account"] = await accounts.GetSummaryAsync(customerId, cancellationToken);
        return View(model);
    }

    [Authorize]
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var data = await personalData.ExportAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);

        return File(JsonSerializer.SerializeToUtf8Bytes(data, ExportOptions), "application/json", "keycapstore-meus-dados.json");
    }

    [HttpGet]
    public IActionResult Deleted() => View();

    private async Task LoadAccountPageAsync(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ViewData["SavedAddress"] = await savedAddresses.GetAsync(customerId, cancellationToken);
        ViewData["Account"] = await accounts.GetSummaryAsync(customerId, cancellationToken);
        ViewData["RecentOrders"] = (await orders.ListForCustomerAsync(customerId, cancellationToken)).Take(3).ToList();
    }
}
