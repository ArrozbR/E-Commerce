using System.Net;
using System.Text.Json;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class MyAccountPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string NewPassword = "Outra#Senha456";

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> UserIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
    }

    private static async Task<Guid> SaveOrderAsync(WebApplicationFactory<Program> factory, string customerId, DateTimeOffset createdAt)
    {
        var order = new Order(customerId, createdAt, [new OrderItem(Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), "Kit Aurora (base)", 349.90m, 1)], Curitiba, TestShipping.Pac);
        order.ConfirmPayment();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    private static async Task<HttpStatusCode> LoginAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = PageClient.Anonymous(factory);
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Login"));
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));

        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string current, string next, string? confirm = null)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Password"));

        return await client.PostAsync("/Account/Password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = current,
            ["NewPassword"] = next,
            ["ConfirmNewPassword"] = confirm ?? next,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task AccountPage_ShowsTheThreeNewestOrders_AndEverySection()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "conta-recentes@example.com");
        var customerId = await UserIdAsync(factory, "conta-recentes@example.com");
        var oldest = await SaveOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-4));
        var newer = new List<Guid>();
        for (var days = 3; days >= 1; days--)
        {
            newer.Add(await SaveOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-days)));
        }

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));

        Assert.All(newer, id => Assert.Contains($"href=\"/Orders/Details/{id}\"", html));
        Assert.DoesNotContain(oldest.ToString(), html);
        Assert.True(html.IndexOf(newer[2].ToString(), StringComparison.Ordinal) < html.IndexOf(newer[0].ToString(), StringComparison.Ordinal));
        Assert.Contains("Ver todos os pedidos", html);
        Assert.Contains("href=\"/Account/Password\"", html);
        Assert.Contains("download href=\"/Account/Data\"", html);
        Assert.Contains("data-theme-choice=\"dark\"", html);
        Assert.Contains("Zona de perigo", html);
        Assert.Contains("Excluir minha conta", html);
    }

    [Fact]
    public async Task ChangePassword_WithTheCurrentPassword_ReplacesIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "senha-nova@example.com");

        var response = await ChangePasswordAsync(client, PageClient.Password, NewPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Delete", response.Headers.Location?.OriginalString);
        Assert.Contains("Senha alterada.", WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete")));
        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, "senha-nova@example.com", NewPassword));
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(factory, "senha-nova@example.com", PageClient.Password));
    }

    [Fact]
    public async Task ChangePassword_SignsOutTheOtherSessions_AndKeepsThisOne()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "senha-sessoes@example.com");
        var otherDevice = PageClient.Anonymous(factory);
        var token = PageClient.TokenFrom(await otherDevice.GetStringAsync("/Account/Login"));
        await otherDevice.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "senha-sessoes@example.com",
            ["Password"] = PageClient.Password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/Account/Delete")).StatusCode);

        await ChangePasswordAsync(client, PageClient.Password, NewPassword);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Delete")).StatusCode);
        var other = await otherDevice.GetAsync("/Account/Delete");
        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
        Assert.Contains("/Account/Login", other.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_KeepsTheOldOne()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "senha-errada@example.com");

        var response = await ChangePasswordAsync(client, "Nao#Eh123", NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Senha atual incorreta.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, "senha-errada@example.com", PageClient.Password));
    }

    [Fact]
    public async Task ChangePassword_TooShort_ShowsTheRule()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "senha-curta@example.com");

        var response = await ChangePasswordAsync(client, PageClient.Password, "Ab#1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A senha precisa ter pelo menos 6 caracteres.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ChangePassword_ForAnAccountWithoutPassword_SaysThereIsNone()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        string customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "so-google@example.com", Email = "so-google@example.com" };
            await users.CreateAsync(user);
            customerId = user.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IAccountService>().ChangePasswordAsync(customerId, "qualquer", NewPassword);

            Assert.Equal(ChangePasswordStatus.NoPassword, result.Status);
        }
    }

    [Fact]
    public async Task DownloadMyData_HasOnlyThisCustomersAccountAddressAndOrders()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "meus-dados@example.com");
        var customerId = await UserIdAsync(factory, "meus-dados@example.com");
        var mine = await SaveOrderAsync(factory, customerId, DateTimeOffset.UtcNow);
        await PageClient.LoggedInAsync(factory, "outros-dados@example.com");
        var someoneElses = await SaveOrderAsync(factory, await UserIdAsync(factory, "outros-dados@example.com"), DateTimeOffset.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync(customerId, Curitiba);
        }

        var response = await client.GetAsync("/Account/Data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("keycapstore-meus-dados.json", response.Content.Headers.ContentDisposition?.FileName);
        var json = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(json).RootElement;
        Assert.Equal("meus-dados@example.com", data.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("Rua XV de Novembro", data.GetProperty("savedAddress").GetProperty("street").GetString());
        var order = Assert.Single(data.GetProperty("orders").EnumerateArray());
        Assert.Equal(mine, order.GetProperty("id").GetGuid());
        Assert.Equal("Kit Aurora (base)", order.GetProperty("items")[0].GetProperty("productName").GetString());
        Assert.DoesNotContain(someoneElses.ToString(), json);
    }

    [Fact]
    public async Task DownloadMyData_AsAVisitor_GoesToTheLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/Account/Data");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AccountPage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "conta-nova-ingles@example.com");
        await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/Account/Delete",
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete")),
        }));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));
        var password = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Password"));

        Assert.Contains("Recent orders", html);
        Assert.Contains("Sign-in and security", html);
        Assert.Contains("Download my data", html);
        Assert.Contains("Danger zone", html);
        Assert.Contains(">Change password</h1>", password);
        Assert.Contains("New password", password);
    }
}
