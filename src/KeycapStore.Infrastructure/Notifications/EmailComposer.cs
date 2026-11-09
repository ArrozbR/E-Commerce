using System.Text;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class EmailComposer(AppDbContext db, UserManager<IdentityUser> users, INewsletterService newsletter, IOptions<PublicSiteOptions> site)
{
    public Task<EmailMessage?> ComposeAsync(OutboxEmail email, CancellationToken cancellationToken = default) =>
        email.CustomerId is null ? ComposeForOrderAsync(email, cancellationToken) : ComposeForAccountAsync(email);

    private async Task<EmailMessage?> ComposeForOrderAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == email.OrderId, cancellationToken);

        if (order?.CustomerId is null)
        {
            return null;
        }

        var to = await db.Users
            .Where(u => u.Id == order.CustomerId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(to))
        {
            return null;
        }

        var batchCancelled = email.Kind == EmailKind.OrderRefunded
            && order.PreOrderShipsAt is not null
            && await db.PreOrderOutcomeAsync(order, cancellationToken) == PreOrderOutcome.Cancelled;

        return EmailTemplates.Compose(email.Kind, order, to, Link($"Orders/Details/{order.Id}"), email.CreatedAt, batchCancelled);
    }

    private async Task<EmailMessage?> ComposeForAccountAsync(OutboxEmail email)
    {
        var user = await users.FindByIdAsync(email.CustomerId!);
        if (user?.Email is not { Length: > 0 } to)
        {
            return null;
        }

        var language = email.Language ?? Order.DefaultLanguage;
        var login = Link("Account/Login");

        if (email.Kind == EmailKind.PasswordChanged)
        {
            return EmailTemplates.PasswordChanged(language, to, login, email.CreatedAt);
        }

        if (email.Kind == EmailKind.BackInStock)
        {
            var product = await db.Products
                .AsNoTracking()
                .Where(p => p.Id == email.ProductId && p.IsActive)
                .Select(p => new { p.Id, p.Name, p.Price })
                .SingleOrDefaultAsync();

            return product is null
                ? null
                : EmailTemplates.BackInStock(language, to, product.Name, product.Price, Link($"Catalog/{product.Id}"));
        }

        if (email.Kind is EmailKind.NewKit or EmailKind.PreOrderOpened)
        {
            if (!await db.NewsletterSubscriptions.AnyAsync(s => s.CustomerId == user.Id))
            {
                return null;
            }

            var kit = await db.Products
                .AsNoTracking()
                .Where(p => p.Id == email.ProductId && p.IsActive)
                .Select(p => new { p.Id, p.Name, p.Price, p.PreOrderEndsAt, p.PreOrderShipsAt })
                .SingleOrDefaultAsync();

            if (kit is null)
            {
                return null;
            }

            var kitLink = Link($"Catalog/{kit.Id}");
            var unsubscribe = Link($"Newsletter/Unsubscribe?token={Uri.EscapeDataString(newsletter.UnsubscribeTokenFor(user.Id))}");
            return email.Kind == EmailKind.PreOrderOpened && kit.PreOrderEndsAt is { } endsAt && kit.PreOrderShipsAt is { } shipsAt
                ? EmailTemplates.PreOrderOpened(language, to, kit.Name, kit.Price, endsAt, shipsAt, kitLink, unsubscribe)
                : EmailTemplates.NewKit(language, to, kit.Name, kit.Price, kitLink, unsubscribe);
        }

        if (email.Kind != EmailKind.PasswordReset)
        {
            throw new ArgumentOutOfRangeException(nameof(email), email.Kind, "Tipo de e-mail de conta desconhecido.");
        }

        if (await users.IsInRoleAsync(user, Roles.Admin))
        {
            return EmailTemplates.PasswordResetForAdmin(language, to);
        }

        if (!await users.HasPasswordAsync(user))
        {
            return EmailTemplates.PasswordResetForGoogle(language, to, login);
        }

        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var resetLink = Link($"Account/ResetPassword?userId={Uri.EscapeDataString(user.Id)}&token={token}");

        return EmailTemplates.PasswordReset(language, to, resetLink, PasswordResetService.LinkLifetime);
    }

    private Uri Link(string relative) => new(new Uri(site.Value.PublicUrl), relative);
}
