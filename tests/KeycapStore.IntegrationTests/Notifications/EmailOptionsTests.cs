using KeycapStore.Infrastructure.Notifications;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycapStore.IntegrationTests.Notifications;

public class EmailOptionsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("Email:Host", "", "Email:Host")]
    [InlineData("Email:Port", "0", "Email:Port")]
    [InlineData("Email:FromAddress", "sem-arroba", "Email:FromAddress")]
    [InlineData("Email:FromName", "", "Email:FromName")]
    public async Task App_WithAnInvalidEmailSetting_RefusesToStart(string setting, string value, string expected)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task App_WithAUsernameButNoPassword_RefusesToStart()
    {
        await using var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:Username", "loja@example.com");
            builder.UseSetting("Email:UseStartTls", "true");
        });

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Email:Password", error.Message);
    }

    [Fact]
    public async Task App_WithLoginButWithoutTls_RefusesToStart()
    {
        await using var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:Username", "loja@example.com");
            builder.UseSetting("Email:Password", "senha-de-teste");
            builder.UseSetting("Email:UseStartTls", "false");
        });

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Email:UseStartTls", error.Message);
    }

    [Fact]
    public async Task Production_UsesGmailWithTls_FromTheAppSettings()
    {
        await using var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Email:FromAddress", "loja@example.com");
        });

        var settings = factory.Services.GetRequiredService<IOptions<EmailOptions>>().Value;

        Assert.Equal("smtp.gmail.com", settings.Host);
        Assert.Equal(587, settings.Port);
        Assert.True(settings.UseStartTls);
        Assert.Equal("KeycapStore", settings.FromName);
    }
}
