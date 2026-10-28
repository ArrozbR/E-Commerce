using System.ComponentModel.DataAnnotations;

using KeycapStore.Application.Identity;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.Infrastructure.Identity;

public sealed class GoogleLoginOptions
{
    public const string SectionName = "Authentication:Google";

    [Required(ErrorMessage = "Falta a configuração 'Authentication:Google:ClientId' (ID do cliente OAuth do Google).")]
    [RegularExpression(@"^\S+\.apps\.googleusercontent\.com$", ErrorMessage = "'Authentication:Google:ClientId' não parece um ID de cliente do Google (deve terminar com .apps.googleusercontent.com).")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'Authentication:Google:ClientSecret' (segredo do cliente OAuth do Google).")]
    public string ClientSecret { get; set; } = string.Empty;
}

public static class GoogleLogin
{
    public static AuthenticationBuilder AddGoogleLogin(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        var section = configuration.GetSection(GoogleLoginOptions.SectionName);

        authentication.Services.AddOptions<GoogleLoginOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        authentication.AddGoogle(ExternalProviders.Google, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.ClientId = section["ClientId"] ?? string.Empty;
            options.ClientSecret = section["ClientSecret"] ?? string.Empty;
        });

        return authentication;
    }
}
