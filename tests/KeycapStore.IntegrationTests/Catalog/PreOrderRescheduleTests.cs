using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class PreOrderRescheduleTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static string InBrasilia(DateTimeOffset moment, string format) =>
        TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync(email)).Succeeded);
        return await PageClient.LoggedInAsync(factory, email);
    }

    private static async Task RescheduleAsync(HttpClient admin, Guid productId, DateTimeOffset endsAt, DateTimeOffset shipsAt)
    {
        var token = PageClient.TokenFrom(await admin.GetStringAsync("/Catalog"));
        await admin.PostAsync($"/admin/products/{productId}/reschedule", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PreOrderEndsAt"] = InBrasilia(endsAt, "yyyy-MM-dd'T'HH:mm"),
            ["PreOrderShipsOn"] = InBrasilia(shipsAt, "yyyy-MM-dd"),
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static async Task<Product> ProductAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    [Fact]
    public async Task Admin_ReopensACampaignThatEnded()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Reabrir Pelo Admin");
        await TestPreOrders.EndCampaignAsync(factory, preOrder);
        var admin = await AdminAsync(factory, "admin-reabre@example.com");
        var newEnd = DateTimeOffset.UtcNow.AddDays(3);

        await RescheduleAsync(admin, preOrder, newEnd, DateTimeOffset.UtcNow.AddDays(30));

        var product = await ProductAsync(factory, preOrder);
        Assert.True(product.PreOrder!.IsOpenAt(DateTimeOffset.UtcNow));
        Assert.InRange((product.PreOrder.EndsAt - newEnd).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Assert.Contains("Datas da pré-venda atualizadas.", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }

    [Fact]
    public async Task Admin_ShippingTooFar_IsRefusedWithAMessage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Envio Longe");
        var admin = await AdminAsync(factory, "admin-envio-longe@example.com");
        var before = await ProductAsync(factory, preOrder);

        await RescheduleAsync(admin, preOrder, DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(85));

        Assert.Equal(before.PreOrder, (await ProductAsync(factory, preOrder)).PreOrder);
        Assert.Contains("Datas recusadas: o fim precisa ser depois de agora", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }

    [Fact]
    public async Task Admin_ClosedBatch_IsRefusedWithAMessage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Lote Já Fechado");
        await TestPreOrders.CloseAsync(factory, preOrder, confirmed: true);
        var admin = await AdminAsync(factory, "admin-lote-fechado@example.com");

        await RescheduleAsync(admin, preOrder, DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(30));

        Assert.Contains("Datas recusadas: o lote desta pré-venda já foi fechado.", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/products")));
    }

    [Fact]
    public async Task OrderPage_ShowsTheNewForecast_AndTheOneAtPurchase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var email = "dono-nova-data@example.com";
        var client = await PageClient.LoggedInAsync(factory, email);
        string customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            customerId = (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
        }

        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Data Nova");
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment(), customerId);
        var newShip = DateTimeOffset.UtcNow.AddDays(60);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<KeycapStore.Application.Catalog.IProductAdminService>()
                .ReschedulePreOrderAsync(preOrder, DateTimeOffset.UtcNow.AddDays(10), newShip);
        }

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{order}"));

        Assert.Contains($"envio previsto em {InBrasilia(newShip, "dd/MM/yyyy")}", html);
        Assert.Contains("(na compra:", html);
    }
}
