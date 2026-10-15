using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;

namespace KeycapStore.IntegrationTests.Smoke;

public class HealthEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Health_WithTheDatabaseUp_Returns200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_WithTheDatabaseDown_Returns503_WithoutDetails()
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=nada;Username=x;Password=x;Timeout=2"));

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }
}
