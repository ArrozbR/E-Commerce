using KeycapStore.Application.Payments;
using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Orders;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
    {
        builder.UseSetting("ConnectionStrings:Default", Container.GetConnectionString());
        builder.UseSetting("Stripe:SecretKey", "sk_test_fake_key_for_tests");
        builder.UseSetting("Stripe:WebhookSecret", StripeWebhookSigner.TestSecret);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IPaymentGateway>(new FakePaymentGateway());
            services.Remove(services.Single(d => d.ImplementationType == typeof(OrderMaintenanceJob)));
        });
    });

    public async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync()
    {
        var factory = CreateFactory();
        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }

    public async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync(IPaymentGateway payments)
    {
        var factory = CreateFactory()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(payments)));
        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }
}
