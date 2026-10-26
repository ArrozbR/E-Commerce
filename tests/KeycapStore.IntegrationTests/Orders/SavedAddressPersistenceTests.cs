using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class SavedAddressPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static readonly ShippingAddress Curitiba =
        new("Ana Souza", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    [Fact]
    public async Task SavedAddress_CanBeSavedAndReadBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SavedAddresses.Add(new SavedAddress("customer-saved-read", Paulista));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.SavedAddresses.SingleAsync(a => a.CustomerId == "customer-saved-read");

            Assert.Equal("Avenida Paulista", saved.Address.Street);
            Assert.Equal("Apto 12", saved.Address.Complement);
            Assert.Equal("SP", saved.Address.State);
            Assert.Equal("01310100", saved.Address.PostalCode);
        }
    }

    [Fact]
    public async Task ReplacedAddress_IsSaved()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SavedAddresses.Add(new SavedAddress("customer-saved-replace", Paulista));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.SavedAddresses.SingleAsync(a => a.CustomerId == "customer-saved-replace");
            saved.Replace(Curitiba);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.SavedAddresses.SingleAsync(a => a.CustomerId == "customer-saved-replace");

            Assert.Equal("Rua XV de Novembro", saved.Address.Street);
            Assert.Null(saved.Address.Complement);
            Assert.Equal("PR", saved.Address.State);
        }
    }

    [Fact]
    public async Task SecondAddressForTheSameCustomer_IsRefusedByTheDatabase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SavedAddresses.Add(new SavedAddress("customer-saved-twice", Paulista));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SavedAddresses.Add(new SavedAddress("customer-saved-twice", Curitiba));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
