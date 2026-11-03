using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Orders;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KeycapStore.UnitTests.Infrastructure;

public class MelhorEnvioShippingQuotesTests
{
    private const string SandboxAnswer = """
        [
          { "id": 1, "name": "PAC", "price": "30.63", "custom_price": "30.63", "currency": "R$", "delivery_time": 7, "custom_delivery_time": 7, "company": { "id": 1, "name": "Correios" } },
          { "id": 2, "name": "SEDEX", "price": "59.53", "custom_price": "59.53", "currency": "R$", "delivery_time": 3, "custom_delivery_time": 3, "company": { "id": 1, "name": "Correios" } },
          { "id": 3, "name": ".Package", "price": "30.17", "custom_price": "30.17", "currency": "R$", "delivery_time": 5, "custom_delivery_time": 5, "company": { "id": 2, "name": "Jadlog" } },
          { "id": 17, "name": "Mini Envios", "error": "Valor segurado ultrapassa o limite máximo de R$ 104,62.", "company": { "id": 1, "name": "Correios" } }
        ]
        """;

    private static readonly ShippingQuoteItem Kit = new(
        Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), new ProductPackage(0.8m, 30, 6, 20), 349.90m, 2);

    private sealed class FakeMelhorEnvio(Func<CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await answer(cancellationToken);
        }
    }

    private static FakeMelhorEnvio Answering(HttpStatusCode status, string body) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private static MelhorEnvioShippingQuotes Quotes(HttpMessageHandler handler, TimeSpan? timeout = null) => new(
        new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(10) },
        Options.Create(new MelhorEnvioOptions
        {
            Token = "token-de-teste",
            BaseUrl = "https://sandbox.melhorenvio.com.br/",
            OriginPostalCode = "74003010",
            ContactEmail = "contato@example.com",
        }),
        NullLogger<MelhorEnvioShippingQuotes>.Instance);

    [Fact]
    public async Task Quote_ReadsTheServices_CheapestFirst_AndSkipsTheOneWithAnError()
    {
        var quote = await Quotes(Answering(HttpStatusCode.OK, SandboxAnswer)).QuoteAsync("01310100", [Kit]);

        Assert.True(quote.Available);
        Assert.Equal(
            [
                new ShippingOption(3, "Jadlog", ".Package", 30.17m, 5),
                new ShippingOption(1, "Correios", "PAC", 30.63m, 7),
                new ShippingOption(2, "Correios", "SEDEX", 59.53m, 3),
            ],
            quote.Options);
    }

    [Fact]
    public async Task Quote_ReadsThePriceWithAPoint_EvenWhenTheServerIsInPortuguese()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            var quote = await Quotes(Answering(HttpStatusCode.OK, SandboxAnswer)).QuoteAsync("01310100", [Kit]);

            Assert.Contains(quote.Options, option => option.Service == "PAC" && option.Price == 30.63m);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Quote_SendsWhatTheMelhorEnvioExpects()
    {
        var melhorEnvio = Answering(HttpStatusCode.OK, SandboxAnswer);

        await Quotes(melhorEnvio).QuoteAsync("01310100", [Kit]);

        var request = melhorEnvio.Request!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://sandbox.melhorenvio.com.br/api/v2/me/shipment/calculate", request.RequestUri!.ToString());
        Assert.Equal("Bearer token-de-teste", request.Headers.Authorization!.ToString());
        Assert.Contains("application/json", request.Headers.Accept.ToString());
        Assert.Contains("contato@example.com", string.Join(" ", request.Headers.GetValues("User-Agent")));

        using var body = JsonDocument.Parse(melhorEnvio.Body!);
        Assert.Equal("74003010", body.RootElement.GetProperty("from").GetProperty("postal_code").GetString());
        Assert.Equal("01310100", body.RootElement.GetProperty("to").GetProperty("postal_code").GetString());
        var product = body.RootElement.GetProperty("products")[0];
        Assert.Equal(Kit.ProductId.ToString(), product.GetProperty("id").GetString());
        Assert.Equal(30, product.GetProperty("width").GetInt32());
        Assert.Equal(6, product.GetProperty("height").GetInt32());
        Assert.Equal(20, product.GetProperty("length").GetInt32());
        Assert.Equal(0.8m, product.GetProperty("weight").GetDecimal());
        Assert.Equal(349.90m, product.GetProperty("insurance_value").GetDecimal());
        Assert.Equal(2, product.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Quote_WhenNoServiceDelivers_IsAvailableWithNoOptions()
    {
        const string noService = """[{ "id": 17, "name": "Mini Envios", "error": "Valor segurado ultrapassa o limite.", "company": { "id": 1, "name": "Correios" } }]""";

        var quote = await Quotes(Answering(HttpStatusCode.OK, noService)).QuoteAsync("01310100", [Kit]);

        Assert.True(quote.Available);
        Assert.Empty(quote.Options);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Quote_WhenTheMelhorEnvioRefuses_IsUnavailable(HttpStatusCode status)
    {
        var quote = await Quotes(Answering(status, """{ "message": "erro" }""")).QuoteAsync("01310100", [Kit]);

        Assert.False(quote.Available);
    }

    [Theory]
    [InlineData("<html>fora do ar</html>")]
    [InlineData("""{ "message": "não é uma lista" }""")]
    [InlineData("""[{ "id": 1, "name": "PAC", "custom_price": "trinta", "custom_delivery_time": 7, "company": { "name": "Correios" } }]""")]
    public async Task Quote_WithAnAnswerItCannotRead_IsUnavailable(string body)
    {
        var quote = await Quotes(Answering(HttpStatusCode.OK, body)).QuoteAsync("01310100", [Kit]);

        Assert.False(quote.Available);
    }

    [Fact]
    public async Task Quote_WhenTheMelhorEnvioIsDown_IsUnavailable()
    {
        var down = new FakeMelhorEnvio(_ => throw new HttpRequestException("sem conexão"));

        var quote = await Quotes(down).QuoteAsync("01310100", [Kit]);

        Assert.False(quote.Available);
    }

    [Fact]
    public async Task Quote_WhenTheMelhorEnvioTakesTooLong_IsUnavailable()
    {
        var slow = new FakeMelhorEnvio(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var quote = await Quotes(slow, TimeSpan.FromMilliseconds(100)).QuoteAsync("01310100", [Kit]);

        Assert.False(quote.Available);
    }

    [Fact]
    public async Task Quote_CancelledByTheCaller_IsNotHidden()
    {
        var slow = new FakeMelhorEnvio(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Quotes(slow).QuoteAsync("01310100", [Kit], cancellation.Token));
    }
}
