using System.Globalization;

using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public class MailKitEmailSenderTests(PostgresFixture postgres, MailpitFixture mailpit)
    : IClassFixture<PostgresFixture>, IClassFixture<MailpitFixture>
{
    private async Task<WebApplicationFactory<Program>> CreateFactoryWithRealSenderAsync(int? port = null)
    {
        var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:Host", mailpit.Host);
            builder.UseSetting("Email:Port", (port ?? mailpit.Port).ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("Email:UseStartTls", "false");
            builder.ConfigureTestServices(services =>
            {
                var real = services.First(d => d.ServiceType == typeof(IEmailSender));
                services.RemoveAll<IEmailSender>();
                services.Add(real);
            });
        });

        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }

    [Fact]
    public async Task Send_ThroughSmtp_ArrivesAtTheServer_AndTheEmailIsMarkedSent()
    {
        await using var factory = await CreateFactoryWithRealSenderAsync();
        var (customerId, address) = await CreateCustomerAsync(factory, MailpitFixture.AllowedDomain);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderPaid, orderId);

        await DispatchAsync(factory);

        Assert.Equal(OutboxEmailStatus.Sent, (await FindAsync(factory, emailId))!.Status);
        Assert.Contains(await mailpit.MessagesAsync(), m => m.To == address && m.Subject.Contains(orderId.ToString()[..8]));
    }

    [Fact]
    public async Task Send_RecipientRefusedByTheServer_IsAPermanentFailure()
    {
        await using var factory = await CreateFactoryWithRealSenderAsync();
        var (customerId, address) = await CreateCustomerAsync(factory, "recusado.test");
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderShipped, orderId);

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Failed, email!.Status);
        Assert.Equal(1, email.Attempts);
        Assert.DoesNotContain(await mailpit.MessagesAsync(), m => m.To == address);
    }

    [Fact]
    public async Task Send_ServerUnreachable_IsATemporaryFailure()
    {
        await using var factory = await CreateFactoryWithRealSenderAsync(port: 1);
        var (customerId, _) = await CreateCustomerAsync(factory, MailpitFixture.AllowedDomain);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderRefunded, orderId);

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Pending, email!.Status);
        Assert.Equal(1, email.Attempts);
    }
}
