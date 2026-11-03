using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class CheckoutPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

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

    private static async Task AddKitAuroraAsync(HttpClient client)
    {
        var token = TokenFrom(await client.GetStringAsync("/Catalog"));
        var add = await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAddressAsync(HttpClient client, string postalCode)
    {
        var token = TokenFrom(await client.GetStringAsync("/Checkout"));
        return await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = postalCode,
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["ShippingServiceId"] = "1",
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task Checkout_WhenNotLoggedIn_RedirectsToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Checkout_WithValidAddress_GoesToThePaymentStep_AndEmptiesTheCart()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await LoggedInClientAsync(factory, "checkout@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostAddressAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Checkout/Pay/{stripe.RequestedOrders.Single()}", response.Headers.Location?.OriginalString);
        Assert.Contains("Seu carrinho está vazio.", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Checkout_WhenStripeFails_ShowsTheOrderAsNotApproved()
    {
        var stripe = new FakePaymentGateway { Fail = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await LoggedInClientAsync(factory, "stripe-fora@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostAddressAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var orderUrl = response.Headers.Location!.OriginalString;
        Assert.Equal($"/Orders/Details/{stripe.RequestedOrders.Single()}", orderUrl);
        Assert.Contains("Pagamento não aprovado", WebUtility.HtmlDecode(await client.GetStringAsync(orderUrl)));
    }

    [Fact]
    public async Task Checkout_WithInvalidPostalCode_ShowsTheError_AndKeepsTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "cep@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostAddressAsync(client, "123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CEP inválido", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Contains("Kit Aurora (base)", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ShowsTheError()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "vazio@example.com");

        var response = await PostAddressAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Seu carrinho está vazio.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private static async Task<HttpResponseMessage> PostCuritibaAsync(HttpClient client, string url, string? shippingServiceId)
    {
        var form = new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "80020-310",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = TokenFrom(await client.GetStringAsync("/Checkout")),
        };
        if (shippingServiceId is not null)
        {
            form["ShippingServiceId"] = shippingServiceId;
        }

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task ShippingStep_ListsTheOptions_WithPriceAndDeliveryTime()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var client = await LoggedInClientAsync(factory, "frete-opcoes@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, "/Checkout/Shipping", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Correios PAC: <strong>R$ 15,00</strong>", html);
        Assert.Contains("Correios SEDEX: <strong>R$ 45,00</strong>", html);
        Assert.Contains("até 7 dias", html);
        Assert.Contains("name=\"ShippingServiceId\" id=\"shipping-1\" value=\"1\"", html);
        Assert.Contains("Continuar para o pagamento", html);
        Assert.Contains("data-val-required=\"Escolha uma opção de frete.\"", html);
        Assert.Equal("80020310", Assert.Single(shipping.Requests).PostalCode);
    }

    [Fact]
    public async Task Continue_WithoutChoosingShipping_ShowsTheOptionsAgain_AndCreatesNoOrder()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeShippingQuotes());
        var client = await LoggedInClientAsync(factory, "frete-sem-escolha@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, "/Checkout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Escolha uma opção de frete.", html);
        Assert.Contains("Correios PAC", html);
        Assert.Contains("Kit Aurora (base)", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Continue_WithAServiceThatIsGone_ShowsTheCurrentOptions()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeShippingQuotes());
        var client = await LoggedInClientAsync(factory, "frete-sumiu@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, "/Checkout", "99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("A opção de frete escolhida não está mais disponível. Escolha outra.", html);
        Assert.Contains("Correios SEDEX", html);
    }

    [Fact]
    public async Task ShippingStep_WhenTheMelhorEnvioIsDown_AsksToTryAgain()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeShippingQuotes { Unavailable = true });
        var client = await LoggedInClientAsync(factory, "frete-fora@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, "/Checkout/Shipping", null);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Não foi possível calcular o frete agora. Tente de novo em alguns minutos.", html);
        Assert.DoesNotContain("Continuar para o pagamento", html);
    }

    [Fact]
    public async Task ShippingStep_WithAnInvalidAddress_DoesNotQuote()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var client = await LoggedInClientAsync(factory, "frete-cep-invalido@example.com");
        await AddKitAuroraAsync(client);

        var token = TokenFrom(await client.GetStringAsync("/Checkout"));
        await client.PostAsync("/Checkout/Shipping", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "123",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Empty(shipping.Requests);
    }

    [Fact]
    public async Task OrderPage_ShowsTheChosenServiceAndDeliveryTime()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeShippingQuotes());
        var client = await LoggedInClientAsync(factory, "frete-no-pedido@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, "/Checkout", "2");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var orderId = response.Headers.Location!.OriginalString.Split('/')[^1];
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));
        Assert.Contains("(Correios SEDEX, até 3 dias)", html);
        Assert.Contains("R$ 45,00", html);
    }
}
