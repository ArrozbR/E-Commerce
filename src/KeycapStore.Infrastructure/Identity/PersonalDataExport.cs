using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class PersonalDataExport(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider clock) : IPersonalDataExport
{
    public async Task<PersonalData> ExportAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(customerId)
            ?? throw new InvalidOperationException("Conta não encontrada.");

        var account = new PersonalAccount(
            user.Email ?? string.Empty,
            await userManager.HasPasswordAsync(user),
            [.. (await userManager.GetLoginsAsync(user)).Select(l => l.LoginProvider)]);

        var saved = await db.SavedAddresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .Select(a => a.Address)
            .SingleOrDefaultAsync(cancellationToken);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var alerts = await db.StockAlerts.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .Join(db.Products, a => a.ProductId, p => p.Id, (a, p) => new PersonalStockAlert(p.Name, a.CreatedAt))
            .ToListAsync(cancellationToken);

        var newsletter = await db.NewsletterSubscriptions.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (DateTimeOffset?)s.SubscribedAt)
            .SingleOrDefaultAsync(cancellationToken);

        return new PersonalData(clock.GetUtcNow(), account, saved, [.. orders.Select(o => new PersonalOrder(
            o.Id,
            o.CreatedAt,
            o.Status.ToString(),
            [.. o.Items.Select(i => new OrderLineView(i.ProductName, i.UnitPrice, i.Quantity))],
            o.ShippingFee,
            o.ShippingService,
            o.Total,
            o.ShippingAddress,
            o.TrackingCode,
            o.Language))], alerts, newsletter);
    }
}
