using System.Net;

using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class StockAlertPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<Guid> SaveProductAsync(WebApplicationFactory<Program> factory, string name, int stock)
    {
        var product = new Product(name, 149.90m, stock, TestPackage.Standard);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<bool> SubscribedAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.StockAlerts.AnyAsync(a => a.ProductId == productId);
    }

    private static async Task<string> PageAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        while (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently)
        {
            response = await client.GetAsync(response.Headers.Location);
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string formPage)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync(formPage));
        return await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
    }

    [Fact]
    public async Task SoldOutKit_Anonymous_IsInvitedToSignIn_WithoutAForm()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Visitante", stock: 0);

        var html = await PageAsync(PageClient.Anonymous(factory), $"/Catalog/{productId}");

        Assert.Contains("data-stock-alert", html);
        Assert.Contains("Entrar para ser avisado", html);
        Assert.DoesNotContain($"action=\"/Catalog/{productId}/alert\"", html);
    }

    [Fact]
    public async Task SoldOutKit_LoggedIn_AsksForTheAlert_ThenCancelsIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Liga Desliga", stock: 0);
        var client = await PageClient.LoggedInAsync(factory, "aviso-liga-desliga@example.com");
        var page = $"/Catalog/{productId}/kit-aviso-liga-desliga";

        Assert.Contains("Avise-me quando voltar", await PageAsync(client, page));

        var requested = await PostAsync(client, $"/Catalog/{productId}/alert", page);
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        var afterRequest = await PageAsync(client, requested.Headers.Location!.OriginalString);
        Assert.Contains("Pronto! Avisamos você por e-mail quando este kit voltar.", afterRequest);
        Assert.Contains("data-alert-requested", afterRequest);
        Assert.True(await SubscribedAsync(factory, productId));

        var cancelled = await PostAsync(client, $"/Catalog/{productId}/alert/cancel", page);
        var afterCancel = await PageAsync(client, cancelled.Headers.Location!.OriginalString);
        Assert.Contains("Aviso cancelado.", afterCancel);
        Assert.DoesNotContain("data-alert-requested", afterCancel);
        Assert.False(await SubscribedAsync(factory, productId));
    }

    [Fact]
    public async Task KitInStock_HasNoAlertBlock_AndAPostedAlertIsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Com Estoque", stock: 4);
        var client = await PageClient.LoggedInAsync(factory, "aviso-com-estoque@example.com");
        var page = $"/Catalog/{productId}/kit-aviso-com-estoque";

        Assert.DoesNotContain("data-stock-alert", await PageAsync(client, page));

        var response = await PostAsync(client, $"/Catalog/{productId}/alert", page);
        Assert.Contains("Este kit não recebe aviso agora", await PageAsync(client, response.Headers.Location!.OriginalString));
        Assert.False(await SubscribedAsync(factory, productId));
    }

    [Fact]
    public async Task SoldOutPreOrder_HasNoAlertBlock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await TestPreOrders.SaveOpenAsync(factory, "Kit Aviso Lote", slots: 1, minimum: 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 0));
        }

        var html = await PageAsync(await PageClient.LoggedInAsync(factory, "aviso-lote@example.com"), $"/Catalog/{productId}");

        Assert.DoesNotContain("data-stock-alert", html);
    }

    [Fact]
    public async Task Anonymous_PostingTheAlert_IsSentToSignIn_AndNothingIsSaved()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Sem Login", stock: 0);

        var response = await PostAsync(PageClient.Anonymous(factory), $"/Catalog/{productId}/alert", "/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.False(await SubscribedAsync(factory, productId));
    }

    [Fact]
    public async Task UnknownKit_PostingTheAlert_Is404()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "aviso-kit-inexistente@example.com");

        var response = await PostAsync(client, $"/Catalog/{Guid.NewGuid()}/alert", "/Catalog");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CatalogCard_OfASoldOutKit_LinksToTheAlertOnTheKitPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Cartao", stock: 0);

        var html = await PageAsync(PageClient.Anonymous(factory), "/Catalog?q=Kit+Aviso+Cartao");

        Assert.Contains($"href=\"/Catalog/{productId}/kit-aviso-cartao#avise-me\"", html);
    }

    [Fact]
    public async Task InEnglish_TheButtonIsInEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Aviso Ingles", stock: 0);
        var client = await PageClient.LoggedInAsync(factory, "aviso-ingles@example.com");
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US");

        var html = await PageAsync(client, $"/Catalog/{productId}");

        Assert.Contains("Notify me when it is back", html);
    }
}
