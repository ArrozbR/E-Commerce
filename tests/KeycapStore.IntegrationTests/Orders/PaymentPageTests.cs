using System.Net;

using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class PaymentPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static async Task<Guid> ContinueToPaymentAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));

        token = PageClient.TokenFrom(await client.GetStringAsync("/Checkout"));
        var response = await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "80020-310",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[^1]);
    }

    private static string SessionOf(Guid orderId) => $"cs_test_fake_{orderId:N}";

    [Fact]
    public async Task PaymentStep_ShowsTheAddress_TheTotal_AndTheStripeForm()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "paga-na-pagina@example.com");
        var orderId = await ContinueToPaymentAsync(client);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Checkout/Pay/{orderId}"));

        Assert.Contains("Rua XV de Novembro, 200", html);
        Assert.Contains("Pagar R$ 364,90", html.Replace('\u00A0', ' '));
        Assert.Contains("https://js.stripe.com/endive/stripe.js", html);
        Assert.Contains("pk_test_chave_publicavel_de_teste", html);
        Assert.Contains(FakePaymentGateway.ClientSecretFor(SessionOf(orderId)), html);
    }

    [Fact]
    public async Task TheSession_IsCreatedWithTheReturnPage_AndTheCustomerEmail()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "email-na-sessao@example.com");

        var orderId = await ContinueToPaymentAsync(client);

        var request = Assert.Single(stripe.SessionRequests);
        Assert.Equal("email-na-sessao@example.com", request.CustomerEmail);
        Assert.Equal($"https://localhost/orders/{orderId}/return", request.ReturnUrl.ToString());
    }

    [Fact]
    public async Task AnotherCustomer_NeverSeesTheClientSecret()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var owner = await PageClient.LoggedInAsync(factory, "dono-do-pagamento@example.com");
        var orderId = await ContinueToPaymentAsync(owner);
        var curious = await PageClient.LoggedInAsync(factory, "curioso-do-pagamento@example.com");

        var response = await curious.GetAsync($"/Checkout/Pay/{orderId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain(FakePaymentGateway.ClientSecretFor(SessionOf(orderId)), await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await curious.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task ExpiredSession_GoesToTheOrderPage()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "sessao-expirada@example.com");
        var orderId = await ContinueToPaymentAsync(client);
        stripe.Sessions[SessionOf(orderId)] = new CheckoutSessionState("expired", "unpaid", 36490, "brl", null, FakePaymentGateway.ClientSecretFor(SessionOf(orderId)));

        var response = await client.GetAsync($"/Checkout/Pay/{orderId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Orders/Details/{orderId}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PaidOrder_GoesToTheOrderPage()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "ja-pagou@example.com");
        var orderId = await ContinueToPaymentAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.ConfirmPayment("pi_test_ja_pago");
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/Checkout/Pay/{orderId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Orders/Details/{orderId}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StripeDown_WhenOpeningThePaymentStep_GoesToTheOrderPage()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "stripe-fora-no-pagamento@example.com");
        var orderId = await ContinueToPaymentAsync(client);
        stripe.FailSessionLookups = true;

        var response = await client.GetAsync($"/Checkout/Pay/{orderId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task PaymentStep_InEnglish_OpensStripeInEnglish()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "paga-em-ingles@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/Catalog",
            ["__RequestVerificationToken"] = token,
        }));
        var orderId = await ContinueToPaymentAsync(client);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Checkout/Pay/{orderId}"));

        Assert.Contains("Pay R$364.90", html);
        Assert.Contains("locale: \"en\"", html);
    }

    [Fact]
    public async Task OrderAwaitingPayment_OffersToContinueThePayment()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "volta-para-pagar@example.com");
        var orderId = await ContinueToPaymentAsync(client);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));

        Assert.Contains($"href=\"/Checkout/Pay/{orderId}\"", html);
        Assert.Contains("Continuar o pagamento", html);
    }

    [Fact]
    public async Task PaidOrder_DoesNotOfferToPayAgain()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "nao-paga-duas-vezes@example.com");
        var orderId = await ContinueToPaymentAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.ConfirmPayment("pi_test_pago_uma_vez");
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));

        Assert.DoesNotContain("Continuar o pagamento", html);
        Assert.DoesNotContain($"/Checkout/Pay/{orderId}", html);
    }
}
