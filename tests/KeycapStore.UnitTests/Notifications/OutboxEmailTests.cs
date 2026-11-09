using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Notifications;

namespace KeycapStore.UnitTests.Notifications;

public class OutboxEmailTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static OutboxEmail NewEmail() => new(EmailKind.OrderPaid, Guid.NewGuid(), Start);

    [Fact]
    public void New_IsPending_AndDueRightAway()
    {
        var email = NewEmail();

        Assert.Equal(OutboxEmailStatus.Pending, email.Status);
        Assert.Equal(Start, email.NextAttemptAt);
        Assert.Equal(0, email.Attempts);
        Assert.Null(email.FinishedAt);
    }

    [Fact]
    public void RecordFailure_Temporary_WaitsLongerEachTime_ThenGivesUpAfterTheLastDelay()
    {
        var email = NewEmail();
        var now = Start;
        TimeSpan[] expected =
        [
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(12),
        ];

        foreach (var delay in expected)
        {
            email.RecordFailure(now, permanent: false);

            Assert.Equal(OutboxEmailStatus.Pending, email.Status);
            Assert.Equal(now + delay, email.NextAttemptAt);
            now = email.NextAttemptAt;
        }

        email.RecordFailure(now, permanent: false);

        Assert.Equal(OutboxEmailStatus.Failed, email.Status);
        Assert.Equal(7, email.Attempts);
        Assert.Equal(now, email.FinishedAt);
    }

    [Fact]
    public void RecordFailure_Permanent_FailsAtOnce()
    {
        var email = NewEmail();

        email.RecordFailure(Start, permanent: true);

        Assert.Equal(OutboxEmailStatus.Failed, email.Status);
        Assert.Equal(1, email.Attempts);
    }

    [Fact]
    public void MarkSent_FinishesTheEmail()
    {
        var email = NewEmail();

        email.MarkSent(Start.AddMinutes(1));

        Assert.Equal(OutboxEmailStatus.Sent, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.Equal(Start.AddMinutes(1), email.FinishedAt);
    }

    [Fact]
    public void Discard_FinishesWithoutCountingAnAttempt()
    {
        var email = NewEmail();

        email.Discard(Start);

        Assert.Equal(OutboxEmailStatus.Discarded, email.Status);
        Assert.Equal(0, email.Attempts);
    }

    [Fact]
    public void AnyChange_AfterFinished_Throws()
    {
        var email = NewEmail();
        email.MarkSent(Start);

        Assert.Throws<InvalidOperationException>(() => email.MarkSent(Start));
        Assert.Throws<InvalidOperationException>(() => email.RecordFailure(Start, permanent: false));
        Assert.Throws<InvalidOperationException>(() => email.Discard(Start));
    }
}
