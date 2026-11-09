using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public class EmailOutboxDispatcherTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SendPending_OrderWithOwner_SendsToTheOwnersEmail_AndMarksSent()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, address) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderPaid, orderId);

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Sent, email!.Status);
        Assert.Equal(1, email.Attempts);
        Assert.NotNull(email.FinishedAt);
        var message = Assert.Single(sender.Sent, m => m.To == address);
        Assert.Contains(orderId.ToString()[..8], message.Subject);
    }

    [Fact]
    public async Task SendPending_OrderWithoutOwner_IsDiscarded_WithoutSending()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, address) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId, order =>
        {
            order.Expire();
            order.DetachCustomer(DateTimeOffset.UtcNow);
        });
        var emailId = await QueueAsync(factory, EmailKind.OrderPaid, orderId);

        await DispatchAsync(factory);

        Assert.Equal(OutboxEmailStatus.Discarded, (await FindAsync(factory, emailId))!.Status);
        Assert.DoesNotContain(sender.Sent, m => m.To == address);
    }

    [Fact]
    public async Task SendPending_OwnerAccountNoLongerExists_IsDiscarded()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var orderId = await SaveOrderAsync(factory, $"conta-apagada-{Guid.NewGuid():N}");
        var emailId = await QueueAsync(factory, EmailKind.OrderShipped, orderId);

        await DispatchAsync(factory);

        Assert.Equal(OutboxEmailStatus.Discarded, (await FindAsync(factory, emailId))!.Status);
    }

    [Fact]
    public async Task SendPending_TemporaryFailure_StaysPending_AndWaitsOneMinute()
    {
        var sender = new FakeEmailSender { FailWith = new EmailDeliveryException("Gmail fora do ar (simulado).", permanent: false) };
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, address) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderPaid, orderId);
        var before = DateTimeOffset.UtcNow;

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Pending, email!.Status);
        Assert.Equal(1, email.Attempts);
        Assert.InRange(email.NextAttemptAt, before.AddMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1));

        sender.FailWith = null;
        await DispatchAsync(factory);

        Assert.Equal(OutboxEmailStatus.Pending, (await FindAsync(factory, emailId))!.Status);
        Assert.DoesNotContain(sender.Sent, m => m.To == address);
    }

    [Fact]
    public async Task SendPending_UnexpectedError_CountsAsTemporaryFailure()
    {
        var sender = new FakeEmailSender { FailWith = new TimeoutException("Conexão demorou demais (simulado).") };
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, _) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.OrderRefunded, orderId);

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Pending, email!.Status);
        Assert.Equal(1, email.Attempts);
    }

    [Fact]
    public async Task SendPending_PermanentRejection_FailsAtOnce()
    {
        var sender = new FakeEmailSender { FailWith = new EmailDeliveryException("Endereço inexistente (simulado).", permanent: true) };
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, _) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        var emailId = await QueueAsync(factory, EmailKind.WithdrawalReceived, orderId);

        await DispatchAsync(factory);

        var email = await FindAsync(factory, emailId);
        Assert.Equal(OutboxEmailStatus.Failed, email!.Status);
        Assert.Equal(1, email.Attempts);
    }

    [Fact]
    public async Task Queue_SameKindForTheSameOrderTwice_IsRejectedByTheDatabase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        await QueueAsync(factory, EmailKind.OrderPaid, orderId);

        await Assert.ThrowsAsync<DbUpdateException>(() => QueueAsync(factory, EmailKind.OrderPaid, orderId));
        await QueueAsync(factory, EmailKind.OrderShipped, orderId);
    }

    [Fact]
    public async Task SendPending_FinishedEmailsOlderThan30Days_AreDeleted_RecentOnesStay()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (oldCustomerId, _) = await CreateCustomerAsync(factory);
        var (recentCustomerId, _) = await CreateCustomerAsync(factory);
        var oldOrderId = await SaveOrderAsync(factory, oldCustomerId);
        var recentOrderId = await SaveOrderAsync(factory, recentCustomerId);

        var longAgo = DateTimeOffset.UtcNow.AddDays(-31);
        var old = new OutboxEmail(EmailKind.OrderPaid, oldOrderId, longAgo);
        old.MarkSent(longAgo);
        var recent = new OutboxEmail(EmailKind.OrderPaid, recentOrderId, DateTimeOffset.UtcNow.AddDays(-29));
        recent.RecordFailure(DateTimeOffset.UtcNow.AddDays(-29), permanent: true);
        var oldId = await QueueAsync(factory, old);
        var recentId = await QueueAsync(factory, recent);

        await DispatchAsync(factory);

        Assert.Null(await FindAsync(factory, oldId));
        Assert.NotNull(await FindAsync(factory, recentId));
    }
}
