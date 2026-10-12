using System.Net;
using System.Text;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeWebhookTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string payload, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Webhook_WithAValidSignature_Returns200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_valid", "checkout.session.completed");

        var response = await PostAsync(client, payload, StripeWebhookSigner.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithoutSignature_Returns400()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_unsigned", "checkout.session.completed");

        var response = await PostAsync(client, payload, signature: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_SignedWithAnotherSecret_Returns400()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_wrong_secret", "checkout.session.completed");

        var response = await PostAsync(client, payload, StripeWebhookSigner.Sign(payload, secret: "whsec_segredo_de_um_impostor"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithATamperedBody_Returns400()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_tampered", "checkout.session.completed");
        var signature = StripeWebhookSigner.Sign(payload);

        var response = await PostAsync(client, payload.Replace("cs_test_fake", "cs_test_other"), signature);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithAnOldSignature_Returns400()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_old", "checkout.session.completed");

        var response = await PostAsync(client, payload, StripeWebhookSigner.Sign(payload, signedAt: DateTimeOffset.UtcNow.AddMinutes(-10)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
