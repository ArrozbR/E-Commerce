using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class SavedAddressService(AppDbContext db) : ISavedAddressService
{
    public async Task<ShippingAddress?> GetAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var saved = await db.SavedAddresses.AsNoTracking()
            .SingleOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        return saved?.Address;
    }

    public async Task SaveAsync(string customerId, ShippingAddress address, CancellationToken cancellationToken = default)
    {
        var saved = await db.SavedAddresses.SingleOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        if (saved is null)
        {
            saved = new SavedAddress(customerId, address);
            db.SavedAddresses.Add(saved);
        }
        else
        {
            saved.Replace(address);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task RemoveAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await db.SavedAddresses.Where(a => a.CustomerId == customerId).ExecuteDeleteAsync(cancellationToken);
    }
}
