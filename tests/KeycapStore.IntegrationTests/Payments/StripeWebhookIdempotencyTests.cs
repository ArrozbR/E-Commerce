using System.Net;
using System.Text;

using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeWebhookIdempotencyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> PostSignedAsync(HttpClient client, string payload, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);

        return await client.SendAsync(request);
    }

    private static async Task<int> CountRecordedAsync(WebApplicationFactory<Program> factory, string eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProcessedStripeEvents.CountAsync(e => e.EventId == eventId);
    }

    [Fact]
    public async Task Webhook_SameEventTwice_IsRecordedOnce_AndBothGet200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_twice", "checkout.session.completed");
        var signature = StripeWebhookSigner.Sign(payload);

        var first = await PostSignedAsync(client, payload, signature);
        var second = await PostSignedAsync(client, payload, signature);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, await CountRecordedAsync(factory, "evt_test_twice"));
    }

    [Fact]
    public async Task Webhook_SameEventInParallel_IsRecordedOnce_AndAllGet200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_parallel", "checkout.session.completed");
        var signature = StripeWebhookSigner.Sign(payload);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PostSignedAsync(client, payload, signature)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await CountRecordedAsync(factory, "evt_test_parallel"));
    }

    [Fact]
    public async Task Webhook_WithAnInvalidSignature_RecordsNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient();
        var payload = StripeWebhookSigner.Event("evt_test_forged", "checkout.session.completed");

        var response = await PostSignedAsync(client, payload, StripeWebhookSigner.Sign(payload, secret: "whsec_segredo_de_um_impostor"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountRecordedAsync(factory, "evt_test_forged"));
    }
}
