using System.Collections.Concurrent;

using KeycapStore.Application.Notifications;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = [];

    public Exception? FailWith { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
