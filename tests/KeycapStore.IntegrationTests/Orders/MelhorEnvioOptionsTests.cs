using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycapStore.IntegrationTests.Orders;

public class MelhorEnvioOptionsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("MelhorEnvio:Token", "")]
    [InlineData("MelhorEnvio:ContactEmail", "")]
    [InlineData("MelhorEnvio:ContactEmail", "sem-arroba")]
    [InlineData("MelhorEnvio:OriginPostalCode", "74003-010")]
    [InlineData("MelhorEnvio:BaseUrl", "")]
    public async Task App_WithAnInvalidMelhorEnvioSetting_RefusesToStart(string setting, string value)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(setting, error.Message);
    }

    [Fact]
    public async Task SandboxAndOrigin_ComeFromTheAppSettings()
    {
        await using var factory = postgres.CreateFactory();

        var settings = factory.Services.GetRequiredService<IOptions<KeycapStore.Infrastructure.Orders.MelhorEnvioOptions>>().Value;

        Assert.Equal("https://sandbox.melhorenvio.com.br/", settings.BaseUrl);
        Assert.Equal("74003010", settings.OriginPostalCode);
    }
}
