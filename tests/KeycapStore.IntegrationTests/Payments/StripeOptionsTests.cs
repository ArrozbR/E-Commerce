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

    [Theory]
    [InlineData("")]
    [InlineData("sk_test_chave_secreta_no_lugar_errado")]
    public async Task App_WithoutAValidWebhookSecret_RefusesToStart(string webhookSecret)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Stripe:WebhookSecret", webhookSecret));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Stripe:WebhookSecret", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sk_test_chave_secreta_no_lugar_da_publicavel")]
    public async Task App_WithoutAValidPublishableKey_RefusesToStart(string publishableKey)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Stripe:PublishableKey", publishableKey));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Stripe:PublishableKey", error.Message);
    }

    [Fact]
    public async Task App_WithASecretKey_Starts()
    {
        await using var factory = postgres.CreateFactory();

        var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
