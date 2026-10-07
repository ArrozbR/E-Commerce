using KeycapStore.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;

using Testcontainers.PostgreSql;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();

    public WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", Container.GetConnectionString()));

    public async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync()
    {
        var factory = CreateFactory();
        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }
}
