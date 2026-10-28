using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class FakeGoogleAccount
{
    public string Id { get; set; } = "google-id-de-teste";

    public string? Email { get; set; }
}

public sealed class FakeGoogleHandler(FakeGoogleAccount account) : IAuthenticationHandler
{
    private HttpContext _context = null!;

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

    public async Task ChallengeAsync(AuthenticationProperties? properties)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, account.Id) };
        if (account.Email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, account.Email));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
        await _context.SignInAsync(IdentityConstants.ExternalScheme, principal, properties);
        _context.Response.Redirect(properties!.RedirectUri!);
    }

    public Task ForbidAsync(AuthenticationProperties? properties)
    {
        _context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
