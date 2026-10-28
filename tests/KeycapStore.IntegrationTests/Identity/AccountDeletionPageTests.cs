using System.Net;

using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class AccountDeletionPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string password)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete"));

        return await client.PostAsync("/Account/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Confirmation"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static async Task<IdentityUser?> UserAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
    }

    [Fact]
    public async Task Visitor_IsSentToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/Account/Delete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Customer_WithTheRightPassword_IsDeleted_AndSignedOut()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "exclui-pela-pagina@example.com");

        var response = await DeleteAsync(client, PageClient.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Deleted", response.Headers.Location!.ToString());
        Assert.Null(await UserAsync(factory, "exclui-pela-pagina@example.com"));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Cart")).StatusCode);
    }

    [Fact]
    public async Task WrongPassword_ShowsTheMessage_AndKeepsTheAccount()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "erra-a-senha@example.com");

        var response = await DeleteAsync(client, "nao-e-essa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Senha incorreta.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.NotNull(await UserAsync(factory, "erra-a-senha@example.com"));
    }

    [Fact]
    public async Task OrderInProgress_ShowsTheMessage_AndKeepsTheAccount()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "tem-pedido-pago@example.com");
        var user = await UserAsync(factory, "tem-pedido-pago@example.com");
        var order = new Order(user!.Id, DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);
        order.ConfirmPayment();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var response = await DeleteAsync(client, PageClient.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Você tem pedidos em andamento.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.NotNull(await UserAsync(factory, "tem-pedido-pago@example.com"));
    }

    [Fact]
    public async Task Admin_ShowsTheMessage_AndKeepsTheAccount()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "admin-pagina@example.com");
        Assert.True((await factory.Services.CreateAdminAsync("admin-pagina@example.com")).Succeeded);

        var response = await DeleteAsync(client, PageClient.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Contas de administrador não podem ser excluídas", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.NotNull(await UserAsync(factory, "admin-pagina@example.com"));
    }
}
