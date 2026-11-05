using System.Net;

using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class ProductPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private const string AuroraPage = "/Catalog/" + KitAurora + "/kit-aurora-base";

    private static async Task<string> PageAsync(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(path));

    [Fact]
    public async Task Catalog_LinksEachKitToItsPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageAsync(PageClient.Anonymous(factory), "/Catalog");

        Assert.Contains($"href=\"{AuroraPage}\"", html);
    }

    [Fact]
    public async Task Details_ShowsTheKit_ItsColors_AndTheAddButtonThatComesBackHere()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageAsync(PageClient.Anonymous(factory), AuroraPage);

        Assert.Contains("<h1 class=\"mb-2 text-4xl sm:text-5xl\">Kit Aurora (base)</h1>", html);
        Assert.Contains("R$ 349,90", html);
        Assert.Contains("Letras, números e os modificadores principais", html);
        Assert.Contains("#1e1b4b", html);
        Assert.Contains("data-typeable", html);
        Assert.Contains("data-key=\"Q\"", html);
        Assert.Contains("data-cart-add", html);
        Assert.Contains($"name=\"returnUrl\" value=\"{AuroraPage}\"", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"https://localhost{AuroraPage}\"", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/nome-antigo")]
    [InlineData("/KIT-AURORA-BASE")]
    public async Task Details_WithoutOrWithAnotherSlug_RedirectsPermanentlyToTheKitAddress(string slug)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync($"/Catalog/{KitAurora}{slug}");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(AuroraPage, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Details_OfAnUnknownOrInactiveKit_IsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var product = new Product("Kit Fora De Venda", 100m, 5, TestPackage.Standard);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        }

        var client = PageClient.Anonymous(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Catalog/{product.Id}/kit-fora-de-venda")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Catalog/{Guid.NewGuid()}/kit")).StatusCode);
    }

    [Fact]
    public async Task Details_SuggestsTheSameColorwayFirst()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageAsync(PageClient.Anonymous(factory), AuroraPage);

        var related = html[html.IndexOf("id=\"related-title\"", StringComparison.Ordinal)..];
        Assert.StartsWith("Kit Aurora (novelties)", related[(related.IndexOf("<span>", StringComparison.Ordinal) + "<span>".Length)..]);
        Assert.DoesNotContain("<span>Kit Aurora (base)</span>", related);
    }

    [Fact]
    public async Task Details_OfAPreOrder_ShowsTheCampaign()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Página Própria");

        var html = await PageAsync(PageClient.Anonymous(factory), $"/Catalog/{preOrder}/kit-pre-venda-pagina-propria");

        Assert.Contains(">Pré-venda<", html);
        Assert.Contains("para o lote sair", html);
        Assert.Contains(">Reservar na pré-venda<", html);
    }
}
