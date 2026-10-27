using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Localization;

public class EnglishAdminTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpClient> EnglishAdminAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync(email)).Succeeded);
        var client = await PageClient.LoggedInAsync(factory, email);

        var response = await PostAsync(client, "/Language/Set", new() { ["culture"] = "en-US", ["returnUrl"] = "/admin/products" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string> fields)
    {
        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        };

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static async Task<Product?> ProductAsync(WebApplicationFactory<Program> factory, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name);
    }

    private static async Task<string> PageAsync(HttpClient client, string url) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(url));

    [Fact]
    public async Task ProductsPage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await EnglishAdminAsync(factory, "admin-pagina-ingles@example.com");

        var html = await PageAsync(admin, "/admin/products");

        Assert.Contains("<h1>Products</h1>", html);
        Assert.Contains("Create product", html);
        Assert.Contains("placeholder=\"199.90\"", html);
    }

    [Fact]
    public async Task CreatingAProduct_InEnglish_ReadsThePriceWithAPeriod()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await EnglishAdminAsync(factory, "admin-preco-ingles@example.com");

        var response = await PostAsync(admin, "/admin/products", new() { ["Name"] = "Kit Preco Ingles", ["Price"] = "199.90", ["Stock"] = "3" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(199.90m, (await ProductAsync(factory, "Kit Preco Ingles"))!.Price);
        Assert.Contains("Product \"Kit Preco Ingles\" created.", await PageAsync(admin, "/admin/products"));
    }

    [Fact]
    public async Task CreatingAProduct_InEnglish_RefusesTheComma()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await EnglishAdminAsync(factory, "admin-virgula-ingles@example.com");

        await PostAsync(admin, "/admin/products", new() { ["Name"] = "Kit Virgula Ingles", ["Price"] = "199,90", ["Stock"] = "3" });

        Assert.Null(await ProductAsync(factory, "Kit Virgula Ingles"));
        Assert.Contains("Invalid price: use a period for the cents (e.g. 199.90), no comma, greater than zero.", await PageAsync(admin, "/admin/products"));
    }

    [Fact]
    public async Task EditForm_InEnglish_ShowsThePriceWithAPeriod()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await EnglishAdminAsync(factory, "admin-edicao-ingles@example.com");
        await PostAsync(admin, "/admin/products", new() { ["Name"] = "Kit Edicao Ingles", ["Price"] = "120.50", ["Stock"] = "1" });

        var html = await PageAsync(admin, "/admin/products");

        Assert.Contains("value=\"120.50\"", html);
    }

    [Fact]
    public async Task ShippingPage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await EnglishAdminAsync(factory, "admin-envio-ingles@example.com");

        var html = await PageAsync(admin, "/admin/orders");

        Assert.Contains("<h1>Order shipping</h1>", html);
    }
}
