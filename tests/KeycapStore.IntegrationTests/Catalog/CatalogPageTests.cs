using System.Net;

using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class CatalogPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task LowStock_IsShownInTheSingular_WhenOnlyOneIsLeft()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product("Kit Ultimo", 100m, 1));
            db.Products.Add(new Product("Kit Poucos", 100m, 3));
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Catalog"));

        Assert.Contains("Última unidade", html);
        Assert.Contains("Últimas 3 unidades", html);
        Assert.DoesNotContain("Últimas 1 unidades", html);
    }
}
