using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class SavedAddressServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static readonly ShippingAddress Curitiba =
        new("Ana Souza", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    [Fact]
    public async Task Get_WithoutSavedAddress_ReturnsNull()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();

        var address = await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().GetAsync("customer-none");

        Assert.Null(address);
    }

    [Fact]
    public async Task Save_FirstTime_CreatesTheAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync("customer-first-save", Paulista);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var address = await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().GetAsync("customer-first-save");

            Assert.NotNull(address);
            Assert.Equal("Avenida Paulista", address.Street);
            Assert.Equal("SP", address.State);
        }
    }

    [Fact]
    public async Task Save_Again_ReplacesTheAddress_AndKeepsOnlyOne()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync("customer-save-twice", Paulista);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync("customer-save-twice", Curitiba);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.SavedAddresses.SingleAsync(a => a.CustomerId == "customer-save-twice");

            Assert.Equal("Rua XV de Novembro", saved.Address.Street);
            Assert.Null(saved.Address.Complement);
        }
    }

    [Fact]
    public async Task Get_DoesNotReturnAnotherCustomersAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync("customer-owner", Paulista);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var address = await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().GetAsync("customer-curious");

            Assert.Null(address);
        }
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatCustomersAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var addresses = scope.ServiceProvider.GetRequiredService<ISavedAddressService>();
            await addresses.SaveAsync("customer-remove", Paulista);
            await addresses.SaveAsync("customer-keep", Curitiba);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().RemoveAsync("customer-remove");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var addresses = scope.ServiceProvider.GetRequiredService<ISavedAddressService>();

            Assert.Null(await addresses.GetAsync("customer-remove"));
            Assert.NotNull(await addresses.GetAsync("customer-keep"));
        }
    }

    [Fact]
    public async Task Remove_WithoutSavedAddress_DoesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().RemoveAsync("customer-never-saved");
    }

    [Fact]
    public async Task SimultaneousFirstSaves_DoNotFail_AndKeepOneAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var saves = Enumerable.Range(0, 10).Select(async i =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>()
                .SaveAsync("customer-race", i % 2 == 0 ? Paulista : Curitiba);
        });
        await Task.WhenAll(saves);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.SavedAddresses.CountAsync(a => a.CustomerId == "customer-race"));
        }
    }
}
