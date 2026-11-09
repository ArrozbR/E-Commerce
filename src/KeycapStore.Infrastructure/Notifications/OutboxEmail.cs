using KeycapStore.Application.Notifications;

namespace KeycapStore.Infrastructure.Notifications;

public enum OutboxEmailStatus
{
    Pending,
    Sent,
    Failed,
    Discarded,
}

public sealed class OutboxEmail
{
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
    ];

    public OutboxEmail(EmailKind kind, Guid orderId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        OrderId = orderId;
        Status = OutboxEmailStatus.Pending;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }

    public EmailKind Kind { get; private set; }

    public Guid OrderId { get; private set; }

    public OutboxEmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public void MarkSent(DateTimeOffset now)
    {
        EnsurePending();
        Attempts++;
        Finish(OutboxEmailStatus.Sent, now);
    }

    public void Discard(DateTimeOffset now)
    {
        EnsurePending();
        Finish(OutboxEmailStatus.Discarded, now);
    }

    public void RecordFailure(DateTimeOffset now, bool permanent)
    {
        EnsurePending();
        Attempts++;

        if (permanent || Attempts > RetryDelays.Count)
        {
            Finish(OutboxEmailStatus.Failed, now);
            return;
        }

        NextAttemptAt = now + RetryDelays[Attempts - 1];
    }

    private void Finish(OutboxEmailStatus status, DateTimeOffset now)
    {
        Status = status;
        FinishedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != OutboxEmailStatus.Pending)
        {
            throw new InvalidOperationException($"O e-mail {Id} já foi finalizado ({Status}).");
        }
    }
}
