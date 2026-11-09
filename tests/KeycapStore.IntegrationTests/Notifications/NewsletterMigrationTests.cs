using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Notifications;

public class NewsletterMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_MarksEveryKitThatAlreadyExisted_AsAnnounced()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var products = await db.Products.CountAsync();
        var notAnnounced = await db.Products.CountAsync(p => !db.ProductAnnouncements.Any(a => a.ProductId == p.Id));

        Assert.True(products > 0);
        Assert.Equal(0, notAnnounced);
    }
}
