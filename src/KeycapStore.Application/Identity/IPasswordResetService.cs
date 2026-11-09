namespace KeycapStore.Application.Identity;

public interface IPasswordResetService
{
    Task RequestAsync(string email, string language, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetAsync(string customerId, string token, string newPassword, string language, CancellationToken cancellationToken = default);
}

public enum PasswordResetStatus
{
    Reset,
    InvalidLink,
    Rejected,
}

public sealed record PasswordResetResult(PasswordResetStatus Status, IReadOnlyList<string> Errors)
{
    public static PasswordResetResult Of(PasswordResetStatus status) => new(status, []);
}
