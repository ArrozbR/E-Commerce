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
