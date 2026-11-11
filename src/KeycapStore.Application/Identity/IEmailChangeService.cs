namespace KeycapStore.Application.Identity;

public interface IEmailChangeService
{
    Task<EmailChangeRequestStatus> RequestAsync(string customerId, string newEmail, string currentPassword, string language, CancellationToken cancellationToken = default);

    Task<EmailChangeConfirmStatus> ConfirmAsync(string customerId, string token, string language, CancellationToken cancellationToken = default);
}

public enum EmailChangeRequestStatus
{
    Requested,
    WrongPassword,
    LockedOut,
    SameEmail,
    NotAllowed,
}

public enum EmailChangeConfirmStatus
{
    Changed,
    InvalidLink,
    EmailInUse,
}
