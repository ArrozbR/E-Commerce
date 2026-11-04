using System.Net;

using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class PreOrderCatalogTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<PreOrderSummary> SummaryAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var products = await scope.ServiceProvider.GetRequiredService<ICatalogQueries>().ListProductsAsync();
        return products.Single(p => p.Id == productId).PreOrder!;
    }

    [Fact]
    public async Task SoldUnits_CountOnlyPaidOrders()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Contagem", slots: 50, minimum: 20);
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 3, o => o.ConfirmPayment());
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 2, o => o.ConfirmPayment());
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 4);
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 5, o => { o.ConfirmPayment(); o.Cancel(); });
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 6, o => o.Expire());

        var summary = await SummaryAsync(factory, preOrder);

        Assert.Equal(5, summary.SoldUnits);
        Assert.Equal(20, summary.MinimumUnits);
        Assert.False(summary.MinimumReached);
        Assert.True(summary.IsOpen);
    }

    [Fact]
    public async Task OtherProductsSales_DoNotCount()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Contagem Isolada", slots: 50, minimum: 2);
        var other = await TestPreOrders.SaveOpenAsync(factory, "Kit Outro Lote", slots: 50, minimum: 2);
        await TestPreOrders.SaveOrderAsync(factory, other, 7, o => o.ConfirmPayment());
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 2, o => o.ConfirmPayment());

        var summary = await SummaryAsync(factory, preOrder);

        Assert.Equal(2, summary.SoldUnits);
        Assert.True(summary.MinimumReached);
    }

    [Fact]
    public async Task CatalogPage_ShowsTheCampaign_AndThePreOrderButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Página Pré-venda", slots: 50, minimum: 20);
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 12, o => o.ConfirmPayment());

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Catalog"));

        Assert.Contains("Kit Página Pré-venda", html);
        Assert.Contains("12 de 20 para o lote sair", html);
        Assert.Contains("50 vagas", html);
        Assert.Contains("Reservar na pré-venda", html);
    }

    [Fact]
    public async Task CatalogPage_ClosedCampaign_HasNoButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Campanha Fechada");
        await TestPreOrders.EndCampaignAsync(factory, preOrder);

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Catalog"));

        Assert.Contains("Pré-venda encerrada", html);
        Assert.DoesNotContain($"value=\"{preOrder}\"", html);
    }

    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync(email)).Succeeded);
        return await PageClient.LoggedInAsync(factory, email);
    }

    private static async Task CreateAsync(HttpClient admin, string name, string endsAt, string shipsOn, string minimum, string slots = "50")
    {
        var token = PageClient.TokenFrom(await admin.GetStringAsync("/Catalog"));
        await admin.PostAsync("/admin/products", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Price"] = "399,90",
            ["Stock"] = slots,
            ["Weight"] = "0,8",
            ["WidthCm"] = "30",
            ["HeightCm"] = "6",
            ["LengthCm"] = "20",
            ["IsPreOrder"] = "true",
            ["PreOrderEndsAt"] = endsAt,
            ["PreOrderShipsOn"] = shipsOn,
            ["PreOrderMinimum"] = minimum,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static async Task<Product?> ProductAsync(WebApplicationFactory<Program> factory, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name);
    }

    private static string InBrasilia(DateTimeOffset moment, string format) =>
        TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Admin_CreatesAPreOrder_InBrasiliaTime()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminAsync(factory, "admin-pre-venda@example.com");
        var ends = DateTimeOffset.UtcNow.AddDays(10);
        var ships = DateTimeOffset.UtcNow.AddDays(40);

        await CreateAsync(admin, "Kit Nebulosa (base)", InBrasilia(ends, "yyyy-MM-dd'T'HH:mm"), InBrasilia(ships, "yyyy-MM-dd"), "20");

        var product = await ProductAsync(factory, "Kit Nebulosa (base)");
        Assert.NotNull(product?.PreOrder);
        Assert.Equal(50, product.Stock);
        Assert.Equal(20, product.PreOrder.MinimumUnits);
        Assert.InRange((product.PreOrder.EndsAt - ends).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.Zero, product.PreOrder.EndsAt.Offset);
    }

    [Theory]
    [InlineData(10, 90, "20", "50")]
    [InlineData(10, 40, "51", "50")]
    [InlineData(-1, 40, "20", "50")]
    [InlineData(10, 40, "20", "0")]
    public async Task Admin_InvalidPreOrder_IsRefusedWithAMessage(int endsInDays, int shipsInDays, string minimum, string slots)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminAsync(factory, $"admin-pre-venda-{Guid.NewGuid():N}@example.com");
        var name = $"Kit Pré-venda Inválida {Guid.NewGuid():N}";

        await CreateAsync(admin, name, InBrasilia(DateTimeOffset.UtcNow.AddDays(endsInDays), "yyyy-MM-dd'T'HH:mm"), InBrasilia(DateTimeOffset.UtcNow.AddDays(shipsInDays), "yyyy-MM-dd"), minimum, slots);

        Assert.Null(await ProductAsync(factory, name));
        Assert.Contains("Pré-venda recusada", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }

    [Fact]
    public async Task Admin_PreOrderWithoutDates_IsRefusedWithAMessage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminAsync(factory, "admin-pre-venda-sem-datas@example.com");

        await CreateAsync(admin, "Kit Sem Datas", "", "", "");

        Assert.Null(await ProductAsync(factory, "Kit Sem Datas"));
        Assert.Contains("Pré-venda: preencha o fim da campanha", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }

    [Fact]
    public async Task CartAndOrder_ShowTheExpectedShipDate()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeShippingQuotes());
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Envio Previsto");
        var client = await PageClient.LoggedInAsync(factory, "pre-venda-envio@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = preOrder.ToString(), ["__RequestVerificationToken"] = token }));

        var cart = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));

        Assert.Contains("Pré-venda · envio previsto em", cart);
    }

    [Fact]
    public async Task CatalogPage_UsesTheSingular_ForOneSoldAndOneSlot()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await TestPreOrders.SaveOpenAsync(factory, "Kit Singular", slots: 1, minimum: 1);
        var sold = await TestPreOrders.SaveOpenAsync(factory, "Kit Uma Vendida", slots: 5, minimum: 1);
        await TestPreOrders.SaveOrderAsync(factory, sold, 1, o => o.ConfirmPayment());

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Catalog"));

        Assert.Contains("1 vaga<", html);
        Assert.Contains("Mínimo atingido: 1 vendida", html);
        Assert.DoesNotContain("1 vendidas", html);
        Assert.DoesNotContain("1 vagas", html);
    }

    [Fact]
    public async Task Admin_ClosedPreOrder_HasNoBackOnSaleButton_AndIsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Fechado Sem Volta");
        await TestPreOrders.CloseAsync(factory, preOrder, confirmed: false);
        var admin = await AdminAsync(factory, "admin-sem-volta@example.com");

        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products"));
        Assert.DoesNotContain($"/admin/products/{preOrder}/activate", html);
        Assert.Contains("Pré-venda fechada", html);

        var token = PageClient.TokenFrom(html);
        await admin.PostAsync($"/admin/products/{preOrder}/activate", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.False((await ProductAsync(factory, "Kit Fechado Sem Volta"))!.IsActive);
        Assert.Contains("Uma pré-venda fechada não volta à venda.", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }
}
