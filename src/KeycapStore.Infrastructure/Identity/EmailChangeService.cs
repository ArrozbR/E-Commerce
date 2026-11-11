using System.Text;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class EmailChangeService(
    AppDbContext db,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    TimeProvider clock,
    ILogger<EmailChangeService> logger) : IEmailChangeService
{
    public async Task<EmailChangeRequestStatus> RequestAsync(string customerId, string newEmail, string currentPassword, string language, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(customerId);
        if (user is null || !await CanChangeAsync(user))
        {
            return EmailChangeRequestStatus.NotAllowed;
        }

        var target = newEmail.Trim();
        if (string.Equals(target, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return EmailChangeRequestStatus.SameEmail;
        }

        var check = await signIn.CheckPasswordSignInAsync(user, currentPassword, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return EmailChangeRequestStatus.LockedOut;
        }

        if (!check.Succeeded)
        {
            return EmailChangeRequestStatus.WrongPassword;
        }

        db.OutboxEmails.Add(OutboxEmail.ToAddressOf(EmailKind.EmailChangeConfirm, user.Id, target, language, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Troca de e-mail pedida pela conta {CustomerId}", user.Id);

        return EmailChangeRequestStatus.Requested;
    }

    public async Task<EmailChangeConfirmStatus> ConfirmAsync(string customerId, string token, string language, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(customerId);
        if (user is null || !await CanChangeAsync(user))
        {
            return EmailChangeConfirmStatus.InvalidLink;
        }

        var newEmail = await LatestRequestedAddressAsync(user.Id, cancellationToken);
        if (newEmail is null || !TryDecode(token, out var decoded))
        {
            return EmailChangeConfirmStatus.InvalidLink;
        }

        var oldEmail = user.Email!;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var changed = await users.ChangeEmailAsync(user, newEmail, decoded);
        if (!changed.Succeeded)
        {
            return StatusOf(changed);
        }

        var renamed = await users.SetUserNameAsync(user, newEmail);
        if (!renamed.Succeeded)
        {
            return StatusOf(renamed);
        }

        db.OutboxEmails.Add(OutboxEmail.ToAddressOf(EmailKind.EmailChanged, user.Id, oldEmail, language, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("E-mail da conta {CustomerId} trocado pelo link de confirmação", user.Id);
        return EmailChangeConfirmStatus.Changed;
    }

    public Task<string?> LatestRequestedAddressAsync(string customerId, CancellationToken cancellationToken) =>
        db.OutboxEmails
            .Where(e => e.CustomerId == customerId && e.Kind == EmailKind.EmailChangeConfirm)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.ToAddress)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<bool> CanChangeAsync(IdentityUser user) =>
        await users.HasPasswordAsync(user) && !await users.IsInRoleAsync(user, Roles.Admin);

    private static bool TryDecode(string token, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static EmailChangeConfirmStatus StatusOf(IdentityResult result) =>
        result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
            ? EmailChangeConfirmStatus.EmailInUse
            : EmailChangeConfirmStatus.InvalidLink;
}
