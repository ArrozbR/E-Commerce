using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class AdminProductsPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync(email)).Succeeded);

        return await PageClient.LoggedInAsync(factory, email);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string>? fields = null)
    {
        var form = new Dictionary<string, string>(fields ?? [])
        {
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        };

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static async Task<Guid> SaveProductAsync(WebApplicationFactory<Program> factory, string name, int stock)
    {
        var product = new Product(name, 100m, stock);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.Id;
    }

    private static async Task<Product?> ProductAsync(WebApplicationFactory<Program> factory, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name);
    }

    private static async Task<string> PageAsync(HttpClient client, string url) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(url));

    [Fact]
    public async Task Visitor_IsSentToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/admin/products");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Customer_GetsForbidden_AndCannotChangeTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Cliente Curioso", stock: 5);
        var customer = await PageClient.LoggedInAsync(factory, "curioso-produtos@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(customer, $"/admin/products/{productId}/adjust-stock", new() { ["delta"] = "100" })).StatusCode);
        Assert.Equal(5, (await ProductAsync(factory, "Kit Cliente Curioso"))!.Stock);
    }

    [Fact]
    public async Task Admin_CreatesAProduct_WithCommaInThePrice_AndItShowsInTheCatalog()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-cria@example.com");

        var response = await PostAsync(admin, "/admin/products", new() { ["Name"] = "Kit Criado Pela Página", ["Price"] = "199,90", ["Stock"] = "7" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var product = await ProductAsync(factory, "Kit Criado Pela Página");
        Assert.NotNull(product);
        Assert.Equal(199.90m, product.Price);
        Assert.Equal(7, product.Stock);
        Assert.Contains("Kit Criado Pela Página", await PageAsync(admin, "/Catalog"));
    }

    [Theory]
    [InlineData("199.90")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1,999")]
    [InlineData("abc")]
    public async Task Admin_CreateWithAnInvalidPrice_IsRefusedWithAMessage(string price)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, $"admin-preco-{Guid.NewGuid():N}@example.com");
        var name = $"Kit Preço Inválido {Guid.NewGuid():N}";

        var response = await PostAsync(admin, "/admin/products", new() { ["Name"] = name, ["Price"] = price, ["Stock"] = "1" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await ProductAsync(factory, name));
        Assert.Contains("Preço inválido", await PageAsync(admin, "/admin/products"));
    }

    [Fact]
    public async Task Admin_EditsNameAndPrice()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Antes da Edição", stock: 5);
        var admin = await AdminClientAsync(factory, "admin-edita@example.com");

        await PostAsync(admin, $"/admin/products/{productId}/edit", new() { ["Name"] = "Kit Depois da Edição", ["Price"] = "120,50" });

        var product = await ProductAsync(factory, "Kit Depois da Edição");
        Assert.NotNull(product);
        Assert.Equal(120.50m, product.Price);
        Assert.Equal(5, product.Stock);
    }

    [Fact]
    public async Task Admin_AdjustsTheStock_AndARemovalBelowZeroIsRefusedWithAMessage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Ajuste Pela Página", stock: 5);
        var admin = await AdminClientAsync(factory, "admin-ajusta@example.com");

        await PostAsync(admin, $"/admin/products/{productId}/adjust-stock", new() { ["delta"] = "10" });
        Assert.Equal(15, (await ProductAsync(factory, "Kit Ajuste Pela Página"))!.Stock);

        await PostAsync(admin, $"/admin/products/{productId}/adjust-stock", new() { ["delta"] = "-100" });
        Assert.Equal(15, (await ProductAsync(factory, "Kit Ajuste Pela Página"))!.Stock);
        Assert.Contains("o estoque ficaria negativo", await PageAsync(admin, "/admin/products"));
    }

    [Fact]
    public async Task Admin_TakesAProductOffSale_AndPutsItBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await SaveProductAsync(factory, "Kit Vai e Volta", stock: 5);
        var admin = await AdminClientAsync(factory, "admin-ativa@example.com");

        await PostAsync(admin, $"/admin/products/{productId}/deactivate");
        Assert.DoesNotContain("Kit Vai e Volta", await PageAsync(admin, "/Catalog"));
        Assert.Contains("Kit Vai e Volta", await PageAsync(admin, "/admin/products"));

        await PostAsync(admin, $"/admin/products/{productId}/activate");
        Assert.Contains("Kit Vai e Volta", await PageAsync(admin, "/Catalog"));
    }

    [Fact]
    public async Task Admin_ActionOnAnUnknownProduct_IsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-inexistente@example.com");

        var response = await PostAsync(admin, $"/admin/products/{Guid.NewGuid()}/deactivate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
