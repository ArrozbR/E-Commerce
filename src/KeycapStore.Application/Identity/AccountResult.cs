namespace KeycapStore.Application.Identity;

public sealed record AccountResult(bool Succeeded, IReadOnlyList<string> Errors, string? CustomerId = null)
{
    public static AccountResult Success(string? customerId = null) => new(true, [], customerId);

    public static AccountResult Failure(IEnumerable<string> errors) => new(false, [.. errors]);
}
