namespace KeycapStore.Application.Notifications;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class EmailDeliveryException(string message, bool permanent, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool Permanent { get; } = permanent;
}
