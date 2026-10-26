using KeycapStore.Application.Identity;
using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class AccountDeletionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{

    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> RegisterAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    private static async Task<Guid> SaveOrderAsync(WebApplicationFactory<Program> factory, string customerId, Action<Order> advance)
    {
        var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);
        advance(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    private static async Task SaveCartAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        var cart = new Cart(customerId);
        cart.AddItem(KitAurora, 1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
    }

    private static async Task SaveAddressAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SavedAddresses.Add(new SavedAddress(customerId, Curitiba));
        await db.SaveChangesAsync();
    }

    private static async Task<bool> HasSavedAddressAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SavedAddresses.AnyAsync(a => a.CustomerId == customerId);
    }

    private static async Task<AccountDeletionResult> DeleteAsync(WebApplicationFactory<Program> factory, string customerId, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(customerId, password);
    }

    private static async Task<(bool AccountExists, string? OrderCustomerId, bool HasCart)> StateAsync(
        WebApplicationFactory<Program> factory, string customerId, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return (
            await users.FindByIdAsync(customerId) is not null,
            await db.Orders.Where(o => o.Id == orderId).Select(o => o.CustomerId).SingleAsync(),
            await db.Carts.AnyAsync(c => c.CustomerId == customerId));
    }

    [Fact]
    public async Task CustomerWithFinishedOrders_IsDeleted_AndTheOrderStaysWithoutOwner()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "sai-da-loja@example.com");
        var orderId = await SaveOrderAsync(factory, customerId, o => o.Expire());
        await SaveCartAsync(factory, customerId);

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.Deleted, result);
        Assert.Equal((false, null, false), await StateAsync(factory, customerId, orderId));
    }

    [Fact]
    public async Task DeletedAccount_TakesTheSavedAddressWithIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "leva-o-endereco@example.com");
        await SaveAddressAsync(factory, customerId);

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.Deleted, result);
        Assert.False(await HasSavedAddressAsync(factory, customerId));
    }

    [Fact]
    public async Task RefusedDeletion_KeepsTheSavedAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "endereco-fica@example.com");
        await SaveOrderAsync(factory, customerId, o => o.ConfirmPayment());
        await SaveAddressAsync(factory, customerId);

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.HasOpenOrders, result);
        Assert.True(await HasSavedAddressAsync(factory, customerId));
    }

    [Fact]
    public async Task CustomerWithAPaidOrder_IsRefused_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "com-pedido-pago@example.com");
        var orderId = await SaveOrderAsync(factory, customerId, o => o.ConfirmPayment());
        await SaveCartAsync(factory, customerId);

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.HasOpenOrders, result);
        Assert.Equal((true, customerId, true), await StateAsync(factory, customerId, orderId));
    }

    [Fact]
    public async Task CustomerWithAnOrderDeliveredTwoDaysAgo_IsRefused_BecauseTheWithdrawalIsStillOpen()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "recebeu-ontem@example.com");
        var orderId = await SaveOrderAsync(factory, customerId, o =>
        {
            o.ConfirmPayment();
            o.StartPicking();
            o.MarkShipped("BR123456789");
            o.MarkDelivered(DateTimeOffset.UtcNow.AddDays(-2));
        });

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.HasOpenOrders, result);
        Assert.Equal((true, customerId, false), await StateAsync(factory, customerId, orderId));
    }

    [Fact]
    public async Task WrongPassword_IsRefused_AndTheAccountStays()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "senha-errada@example.com");
        var orderId = await SaveOrderAsync(factory, customerId, o => o.Expire());

        var result = await DeleteAsync(factory, customerId, "outra-senha-qualquer");

        Assert.Equal(AccountDeletionResult.WrongPassword, result);
        Assert.Equal((true, customerId, false), await StateAsync(factory, customerId, orderId));
    }

    [Fact]
    public async Task Admin_IsRefused_AndTheAccountStays()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customerId = await RegisterAsync(factory, "admin-quer-sair@example.com");
        Assert.True((await factory.Services.CreateAdminAsync("admin-quer-sair@example.com")).Succeeded);
        var orderId = await SaveOrderAsync(factory, customerId, o => o.Expire());

        var result = await DeleteAsync(factory, customerId, PageClient.Password);

        Assert.Equal(AccountDeletionResult.IsAdmin, result);
        Assert.Equal((true, customerId, false), await StateAsync(factory, customerId, orderId));
    }
}
