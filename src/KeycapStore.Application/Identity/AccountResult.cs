namespace KeycapStore.Application.Identity;

public sealed record AccountResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static AccountResult Success() => new(true, []);

    public static AccountResult Failure(IEnumerable<string> errors) => new(false, [.. errors]);
}
