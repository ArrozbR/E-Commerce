using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class AccountDeletionService(
    AppDbContext db,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    TimeProvider clock) : IAccountDeletionService
{
    public async Task<AccountDeletionResult> DeleteAsync(string customerId, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(customerId)
            ?? throw new InvalidOperationException("Conta não encontrada.");

        if (await users.IsInRoleAsync(user, Roles.Admin))
        {
            return AccountDeletionResult.IsAdmin;
        }

        var passwordCheck = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (passwordCheck.IsLockedOut)
        {
            return AccountDeletionResult.LockedOut;
        }

        if (!passwordCheck.Succeeded)
        {
            return AccountDeletionResult.WrongPassword;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var orders = await db.Orders.Where(o => o.CustomerId == customerId).ToListAsync(cancellationToken);

        if (orders.Any(o => !o.IsFinished(now)))
        {
            return AccountDeletionResult.HasOpenOrders;
        }

        foreach (var order in orders)
        {
            order.DetachCustomer(now);
        }

        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is not null)
        {
            db.Carts.Remove(cart);
        }

        await db.SavedAddresses.Where(a => a.CustomerId == customerId).ExecuteDeleteAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            throw new InvalidOperationException("O Identity recusou apagar a conta.");
        }

        await transaction.CommitAsync(cancellationToken);

        return AccountDeletionResult.Deleted;
    }
}
