namespace KeycapStore.Application.Identity;

public enum ChangePasswordStatus
{
    Changed,
    WrongPassword,
    LockedOut,
    NoPassword,
    Rejected,
}

public sealed record ChangePasswordResult(ChangePasswordStatus Status, IReadOnlyList<string> Errors)
{
    public static ChangePasswordResult Of(ChangePasswordStatus status) => new(status, []);
}
