using System.Net;
using System.Text;

using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Errors;

public class ErrorPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> PageAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task UnknownAddress_Is404_WithTheStorePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/essa-pagina-nao-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await PageAsync(response);
        Assert.Contains("<h1 class=\"mb-3 text-4xl\">Página não encontrada</h1>", html);
        Assert.Contains("<span class=\"error-key\">4</span>", html);
        Assert.Contains("KeycapStore", html);
        Assert.DoesNotContain("Código para suporte", html);
    }

    [Fact]
    public async Task CustomerOnAnAdminPage_Is403_WithTheStorePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "erro-403@example.com");

        var response = await client.GetAsync("/admin/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Você não tem acesso a esta página", await PageAsync(response));
    }

    [Fact]
    public async Task ActionThatNoLongerApplies_Is409_WithTheStorePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "erro-409@example.com");
        Guid orderId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var customerId = (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync("erro-409@example.com"))!.Id;
            var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), "Kit Aurora (base)", 349.90m, 1)], Curitiba, TestShipping.Pac);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        var response = await client.PostAsync($"/orders/{orderId}/cancel",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Essa ação não pode mais ser feita", await PageAsync(response));
    }

    [Fact]
    public async Task StripeWebhook_WithoutSignature_Is400_WithoutAPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Error/500", HttpStatusCode.InternalServerError, "Algo deu errado do nosso lado")]
    [InlineData("/Error/999", HttpStatusCode.NotFound, "Página não encontrada")]
    public async Task VisitingTheErrorPage_KeepsAnErrorCode(string path, HttpStatusCode status, string title)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync(path);

        Assert.Equal(status, response.StatusCode);
        var html = await PageAsync(response);
        Assert.Contains(title, html);
        Assert.Equal(status == HttpStatusCode.InternalServerError, html.Contains("Código para suporte", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorPage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/",
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/")),
        }));

        var html = await PageAsync(await client.GetAsync("/essa-pagina-nao-existe"));

        Assert.Contains("Page not found", html);
        Assert.Contains(">Back to home</a>", html);
    }
}
