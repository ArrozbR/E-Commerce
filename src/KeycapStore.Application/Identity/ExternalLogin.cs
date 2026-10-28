namespace KeycapStore.Application.Identity;

public static class ExternalProviders
{
    public const string Google = "Google";
}

public enum ExternalLoginResult
{
    SignedIn,
    EmailAlreadyRegistered,
    AdminNotAllowed,
    Failed,
}

public enum LinkExternalLoginResult
{
    Linked,
    AdminNotAllowed,
    DifferentEmail,
    LinkedToAnotherAccount,
    Failed,
}

public sealed record AccountSummary(bool HasPassword, bool HasGoogle, bool IsAdmin);
