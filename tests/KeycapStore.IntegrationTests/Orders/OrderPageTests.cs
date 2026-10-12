using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class OrderPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

    // Kit Aurora (base), R$ 349,90 e 40 unidades no seed.
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static string TokenFrom(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;

    private static async Task<HttpClient> LoggedInClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, Password);
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var token = TokenFrom(await client.GetStringAsync("/Account/Login"));
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        return client;
    }

    // Compra 1 Kit Aurora para Curitiba (PR, frete R$ 15,00) e devolve o endereço da página do pedido.
    // O "Finalizar" leva para a Stripe de mentira (https://checkout.stripe.test/{id do pedido}):
    // o id do pedido é o último pedaço desse endereço.
    private static async Task<string> PlaceOrderAsync(HttpClient client)
    {
        var token = TokenFrom(await client.GetStringAsync("/Catalog"));
        await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));

        token = TokenFrom(await client.GetStringAsync("/Checkout"));
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

        var orderId = Guid.Parse(response.Headers.Location!.Segments[^1]);
        return $"/Orders/Details/{orderId}";
    }

    private static Guid OrderIdFrom(string orderUrl) => Guid.Parse(orderUrl.Split('/')[^1]);

    [Fact]
    public async Task Owner_SeesTheOrder_WithShippingAndTotal()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "dono@example.com");
        var orderUrl = await PlaceOrderAsync(client);

        var page = WebUtility.HtmlDecode(await client.GetStringAsync(orderUrl));

        Assert.Contains("Kit Aurora (base)", page);
        Assert.Contains("Aguardando pagamento", page);
        Assert.Contains("R$ 15,00", page);
        Assert.Contains("R$ 364,90", page);
        Assert.Contains("CEP 80020-310", page);
    }

    [Fact]
    public async Task AnotherCustomer_GetsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var owner = await LoggedInClientAsync(factory, "comprador@example.com");
        var orderUrl = await PlaceOrderAsync(owner);

        var stranger = await LoggedInClientAsync(factory, "curioso@example.com");
        var response = await stranger.GetAsync(orderUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReturnPage_ShowsTheOrder_AndIsOnlyForTheOwner()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var owner = await LoggedInClientAsync(factory, "retorno@example.com");
        var orderId = OrderIdFrom(await PlaceOrderAsync(owner));

        var page = WebUtility.HtmlDecode(await owner.GetStringAsync($"/orders/{orderId}/return"));
        Assert.Contains("Confirmando seu pagamento", page);
        Assert.Contains($"/orders/{orderId}/status", page);

        var stranger = await LoggedInClientAsync(factory, "curioso-retorno@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/orders/{orderId}/return")).StatusCode);
    }

    [Fact]
    public async Task Status_ChangesToPaid_AfterTheWebhook_AndIsOnlyForTheOwner()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var owner = await LoggedInClientAsync(factory, "situacao@example.com");
        var orderId = OrderIdFrom(await PlaceOrderAsync(owner));

        Assert.Contains("\"status\":\"AwaitingPayment\"", await owner.GetStringAsync($"/orders/{orderId}/status"));

        // A Stripe avisa: R$ 364,90 = 36490 centavos (Kit Aurora + frete para o PR).
        var payload = StripeWebhookSigner.CheckoutCompleted("evt_test_status_page", orderId, 36490);
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        webhook.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().SendAsync(webhook)).StatusCode);

        Assert.Contains("\"status\":\"Paid\"", await owner.GetStringAsync($"/orders/{orderId}/status"));

        var stranger = await LoggedInClientAsync(factory, "curioso-situacao@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/orders/{orderId}/status")).StatusCode);
    }

    [Fact]
    public async Task UnknownOrder_GetsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "inexistente@example.com");

        var response = await client.GetAsync($"/Orders/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
