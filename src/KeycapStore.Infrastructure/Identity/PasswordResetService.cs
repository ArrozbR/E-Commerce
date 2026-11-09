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

internal sealed class PasswordResetService(
    AppDbContext db,
    UserManager<IdentityUser> users,
    TimeProvider clock,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    public async Task RequestAsync(string email, string language, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var since = now - Cooldown;
        var recentlyRequested = await db.OutboxEmails.AnyAsync(
            e => e.CustomerId == user.Id && e.Kind == EmailKind.PasswordReset && e.CreatedAt > since,
            cancellationToken);

        if (recentlyRequested)
        {
            logger.LogInformation("Troca de senha da conta {CustomerId} pedida de novo em menos de 5 min; nenhum e-mail novo", user.Id);
            return;
        }

        db.OutboxEmails.Add(OutboxEmail.ForCustomer(EmailKind.PasswordReset, user.Id, language, now));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Troca de senha pedida para a conta {CustomerId}", user.Id);
    }

    public async Task<PasswordResetResult> ResetAsync(string customerId, string token, string newPassword, string language, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(customerId);
        if (user is null || await users.IsInRoleAsync(user, Roles.Admin) || !await users.HasPasswordAsync(user))
        {
            return PasswordResetResult.Of(PasswordResetStatus.InvalidLink);
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return PasswordResetResult.Of(PasswordResetStatus.InvalidLink);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await users.ResetPasswordAsync(user, decoded, newPassword);
        if (!result.Succeeded)
        {
            var invalidLink = result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.InvalidToken) or nameof(IdentityErrorDescriber.ConcurrencyFailure));
            return invalidLink
                ? PasswordResetResult.Of(PasswordResetStatus.InvalidLink)
                : new PasswordResetResult(PasswordResetStatus.Rejected, [.. result.Errors.Select(e => e.Description)]);
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        db.OutboxEmails.Add(OutboxEmail.ForCustomer(EmailKind.PasswordChanged, user.Id, language, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Senha da conta {CustomerId} trocada pelo link do e-mail", user.Id);
        return PasswordResetResult.Of(PasswordResetStatus.Reset);
    }
}
