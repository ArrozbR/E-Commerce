using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeOptionsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("")]
    [InlineData("pk_test_chave_publicavel_por_engano")]
    public async Task App_WithoutAValidSecretKey_RefusesToStart(string secretKey)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Stripe:SecretKey", secretKey));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Stripe:SecretKey", error.Message);
    }

    [Fact]
    public async Task App_WithASecretKey_Starts()
    {
        await using var factory = postgres.CreateFactory();

        var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
