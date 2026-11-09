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

    public const int MaxCustomerIdLength = 450;

    public const int MaxLanguageLength = 10;

    public OutboxEmail(EmailKind kind, Guid orderId, DateTimeOffset createdAt)
        : this(kind, createdAt)
    {
        OrderId = orderId;
    }

    private OutboxEmail(EmailKind kind, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        Status = OutboxEmailStatus.Pending;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    private OutboxEmail()
    {
    }

    public Guid Id { get; private set; }

    public EmailKind Kind { get; private set; }

    public Guid? OrderId { get; private set; }

    public string? CustomerId { get; private set; }

    public string? Language { get; private set; }

    public OutboxEmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public static OutboxEmail ForCustomer(EmailKind kind, string customerId, string language, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(customerId.Length, MaxCustomerIdLength, nameof(customerId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(language.Length, MaxLanguageLength, nameof(language));

        return new OutboxEmail(kind, createdAt) { CustomerId = customerId, Language = language };
    }

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
