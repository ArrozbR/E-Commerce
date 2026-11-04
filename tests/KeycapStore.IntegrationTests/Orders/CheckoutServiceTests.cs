using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class CheckoutServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    // Cada teste cria os próprios produtos, para não depender do estoque do seed.
    private static async Task<Guid> NewProductAsync(WebApplicationFactory<Program> factory, string name, int stock)
    {
        var product = new Product(name, 100m, stock, TestPackage.Standard);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task AddToCartAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId, int quantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(customerId, productId, quantity);
    }

    private static async Task<PlaceOrderResult> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba, TestShipping.PacServiceId);
    }

    private static async Task<PlaceOrderResult> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId, int shippingServiceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba, shippingServiceId);
    }

    private static async Task<ShippingOptionsResult> QuoteAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>().QuoteShippingAsync(customerId, Curitiba.PostalCode);
    }

    private static async Task<int> StockOfAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
    }

    [Fact]
    public async Task PlaceOrder_ReservesStock_SavesTheOrder_AndEmptiesTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Teste Sucesso", stock: 5);
        await AddToCartAsync(factory, "customer-success", kit, 2);

        var result = await PlaceOrderAsync(factory, "customer-success");

        Assert.True(result.Succeeded);
        Assert.Equal(3, await StockOfAsync(factory, kit));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == result.OrderId);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);
        Assert.Equal(215.00m, order.Total);
        Assert.False(await db.Carts.AnyAsync(c => c.CustomerId == "customer-success"));
    }

    [Fact]
    public async Task PlaceOrder_WhenOneItemIsOutOfStock_ChangesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var available = await NewProductAsync(factory, "Kit Teste Disponível", stock: 5);
        var scarce = await NewProductAsync(factory, "Kit Teste Escasso", stock: 1);
        await AddToCartAsync(factory, "customer-rollback", available, 1);
        await AddToCartAsync(factory, "customer-rollback", scarce, 2);

        var result = await PlaceOrderAsync(factory, "customer-rollback");

        Assert.False(result.Succeeded);
        Assert.Equal(PlaceOrderFailure.OutOfStock, result.Failure);
        Assert.Equal("Kit Teste Escasso", result.ProductName);
        Assert.Equal(5, await StockOfAsync(factory, available));
        Assert.Equal(1, await StockOfAsync(factory, scarce));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Orders.AnyAsync(o => o.CustomerId == "customer-rollback"));
        Assert.Equal(2, (await db.Carts.SingleAsync(c => c.CustomerId == "customer-rollback")).Items.Count);
    }

    [Fact]
    public async Task PlaceOrder_WithEmptyCart_Fails()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var result = await PlaceOrderAsync(factory, "customer-without-cart");

        Assert.False(result.Succeeded);
        Assert.Equal(PlaceOrderFailure.EmptyCart, result.Failure);
    }

    [Fact]
    public async Task PlaceOrder_LastUnit_TwoCustomersAtTheSameTime_OnlyOneGetsIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var lastUnit = await NewProductAsync(factory, "Kit Teste Última Unidade", stock: 1);
        await AddToCartAsync(factory, "customer-race-a", lastUnit, 1);
        await AddToCartAsync(factory, "customer-race-b", lastUnit, 1);

        var results = await Task.WhenAll(
            PlaceOrderAsync(factory, "customer-race-a"),
            PlaceOrderAsync(factory, "customer-race-b"));

        Assert.Single(results, r => r.Succeeded);
        Assert.Equal(0, await StockOfAsync(factory, lastUnit));
    }

    [Fact]
    public async Task PlaceOrder_WithAProductTakenOffSale_ChangesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var available = await NewProductAsync(factory, "Kit Teste Ativo", stock: 5);
        var inactive = await NewProductAsync(factory, "Kit Teste Inativo", stock: 5);
        await AddToCartAsync(factory, "customer-inactive", available, 1);
        await AddToCartAsync(factory, "customer-inactive", inactive, 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.Where(p => p.Id == inactive).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        }

        var result = await PlaceOrderAsync(factory, "customer-inactive");

        Assert.False(result.Succeeded);
        Assert.Equal(PlaceOrderFailure.NoLongerForSale, result.Failure);
        Assert.Equal("Kit Teste Inativo", result.ProductName);
        Assert.Equal(5, await StockOfAsync(factory, available));
        Assert.Equal(5, await StockOfAsync(factory, inactive));

        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AnyAsync(o => o.CustomerId == "customer-inactive"));
    }

    [Fact]
    public async Task QuoteShipping_SendsTheCartWithPackagesAndCatalogPrices()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Cotação", stock: 5);
        await AddToCartAsync(factory, "customer-quote", kit, 2);

        var result = await QuoteAsync(factory, "customer-quote");

        Assert.True(result.Succeeded);
        Assert.Equal(shipping.Options, result.Options);
        var request = Assert.Single(shipping.Requests);
        Assert.Equal("80020310", request.PostalCode);
        Assert.Equal(new ShippingQuoteItem(kit, TestPackage.Standard, 100m, 2), Assert.Single(request.Items));
    }

    [Fact]
    public async Task QuoteShipping_WithAnEmptyCart_DoesNotCallTheMelhorEnvio()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);

        var result = await QuoteAsync(factory, "customer-quote-empty");

        Assert.Equal(PlaceOrderFailure.EmptyCart, result.Failure);
        Assert.Empty(shipping.Requests);
    }

    [Fact]
    public async Task QuoteShipping_WhenTheMelhorEnvioIsDown_SaysSo()
    {
        var shipping = new FakeShippingQuotes { Unavailable = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Cotação Fora", stock: 5);
        await AddToCartAsync(factory, "customer-quote-down", kit, 1);

        Assert.Equal(PlaceOrderFailure.ShippingUnavailable, (await QuoteAsync(factory, "customer-quote-down")).Failure);
    }

    [Fact]
    public async Task QuoteShipping_WhenNoCarrierDelivers_SaysSo()
    {
        var shipping = new FakeShippingQuotes();
        shipping.Options.Clear();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Sem Transportadora", stock: 5);
        await AddToCartAsync(factory, "customer-quote-none", kit, 1);

        Assert.Equal(PlaceOrderFailure.NoShippingOption, (await QuoteAsync(factory, "customer-quote-none")).Failure);
    }

    [Fact]
    public async Task PlaceOrder_UsesThePriceOfANewQuote_NotTheOneShownBefore()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Frete Mudou", stock: 5);
        await AddToCartAsync(factory, "customer-new-quote", kit, 1);
        await QuoteAsync(factory, "customer-new-quote");
        shipping.Options[1] = new ShippingOption(2, "Correios", "SEDEX", 52.40m, 4);

        var result = await PlaceOrderAsync(factory, "customer-new-quote", shippingServiceId: 2);

        Assert.True(result.Succeeded);
        await using var scope = factory.Services.CreateAsyncScope();
        var order = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().SingleAsync(o => o.Id == result.OrderId);
        Assert.Equal(52.40m, order.ShippingFee);
        Assert.Equal("Correios SEDEX", order.ShippingService);
        Assert.Equal(4, order.ShippingDeliveryDays);
        Assert.Equal(152.40m, order.Total);
    }

    [Fact]
    public async Task PlaceOrder_WithAServiceThatIsNoLongerOffered_IsRefused_AndNothingIsReserved()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Serviço Sumiu", stock: 5);
        await AddToCartAsync(factory, "customer-gone", kit, 1);

        var result = await PlaceOrderAsync(factory, "customer-gone", shippingServiceId: 99);

        Assert.Equal(PlaceOrderFailure.ShippingOptionGone, result.Failure);
        Assert.Equal(5, await StockOfAsync(factory, kit));
    }

    [Fact]
    public async Task PlaceOrder_WhenTheMelhorEnvioIsDown_IsRefused_AndNothingIsReserved()
    {
        var shipping = new FakeShippingQuotes { Unavailable = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Melhor Envio Fora", stock: 5);
        await AddToCartAsync(factory, "customer-shipping-down", kit, 1);

        var result = await PlaceOrderAsync(factory, "customer-shipping-down", shippingServiceId: TestShipping.PacServiceId);

        Assert.Equal(PlaceOrderFailure.ShippingUnavailable, result.Failure);
        Assert.Equal(5, await StockOfAsync(factory, kit));
    }

    [Fact]
    public async Task PlaceOrder_PreOrderOpen_TakesASlot_AndKeepsTheExpectedShipDate()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Pedido", slots: 50);
        await AddToCartAsync(factory, "customer-preorder", preOrder, 2);

        var result = await PlaceOrderAsync(factory, "customer-preorder");

        Assert.True(result.Succeeded);
        Assert.Equal(48, await StockOfAsync(factory, preOrder));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == result.OrderId);
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == preOrder);
        Assert.Equal(product.PreOrderShipsAt, order.PreOrderShipsAt);
    }

    [Fact]
    public async Task PlaceOrder_NormalKit_HasNoExpectedShipDate()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Pronta Entrega", stock: 5);
        await AddToCartAsync(factory, "customer-ready", kit, 1);

        var result = await PlaceOrderAsync(factory, "customer-ready");

        await using var scope = factory.Services.CreateAsyncScope();
        var order = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().SingleAsync(o => o.Id == result.OrderId);
        Assert.Null(order.PreOrderShipsAt);
    }

    [Fact]
    public async Task PlaceOrder_AfterTheCampaignEnded_IsRefused_AndNoSlotIsTaken()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Fechou", slots: 50);
        await AddToCartAsync(factory, "customer-preorder-ended", preOrder, 1);
        await TestPreOrders.EndCampaignAsync(factory, preOrder);

        var result = await PlaceOrderAsync(factory, "customer-preorder-ended");

        Assert.Equal(PlaceOrderFailure.PreOrderClosed, result.Failure);
        Assert.Equal("Kit Pré-venda Fechou", result.ProductName);
        Assert.Equal(50, await StockOfAsync(factory, preOrder));
    }

    [Fact]
    public async Task PlaceOrder_CartMixingAPreOrderWithAnotherKit_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Misturado", slots: 50);
        var kit = await NewProductAsync(factory, "Kit Misturado", stock: 5);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cart = new KeycapStore.Domain.Carts.Cart("customer-mixed");
            cart.AddItem(preOrder, 1);
            cart.AddItem(kit, 1);
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        var result = await PlaceOrderAsync(factory, "customer-mixed");

        Assert.Equal(PlaceOrderFailure.PreOrderMustBeAlone, result.Failure);
        Assert.Equal(50, await StockOfAsync(factory, preOrder));
        Assert.Equal(5, await StockOfAsync(factory, kit));
    }

    [Fact]
    public async Task PlaceOrder_WithAnotherOrderAwaitingPayment_IsRefused_AndNothingIsReserved()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Pedido Aberto", stock: 5);
        await TestPreOrders.SaveOrderAsync(factory, kit, 1, customerId: "customer-open-order");
        await AddToCartAsync(factory, "customer-open-order", kit, 2);

        var result = await PlaceOrderAsync(factory, "customer-open-order");

        Assert.Equal(PlaceOrderFailure.OpenOrderExists, result.Failure);
        Assert.Equal(5, await StockOfAsync(factory, kit));
        Assert.Empty(shipping.Requests);
    }

    [Fact]
    public async Task QuoteShipping_WithAnotherOrderAwaitingPayment_IsRefused_WithoutCallingTheMelhorEnvio()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Cotação Com Pedido Aberto", stock: 5);
        await TestPreOrders.SaveOrderAsync(factory, kit, 1, o => o.MarkAwaitingConfirmation(), customerId: "customer-open-quote");
        await AddToCartAsync(factory, "customer-open-quote", kit, 1);

        var result = await QuoteAsync(factory, "customer-open-quote");

        Assert.Equal(PlaceOrderFailure.OpenOrderExists, result.Failure);
        Assert.Empty(shipping.Requests);
    }

    [Fact]
    public async Task PlaceOrder_AfterTheOpenOrderExpired_IsAccepted()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Pedido Expirado", stock: 5);
        await TestPreOrders.SaveOrderAsync(factory, kit, 1, o => o.Expire(), customerId: "customer-expired-order");
        await AddToCartAsync(factory, "customer-expired-order", kit, 1);

        var result = await PlaceOrderAsync(factory, "customer-expired-order");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PlaceOrder_WhenAnOrderAppearsDuringTheQuote_IsRefusedByTheDatabase_AndNothingIsReserved()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Pedido Em Outra Aba", stock: 5);
        await AddToCartAsync(factory, "customer-other-tab", kit, 2);
        shipping.DuringQuote = () => TestPreOrders.SaveOrderAsync(factory, kit, 1, customerId: "customer-other-tab");

        var result = await PlaceOrderAsync(factory, "customer-other-tab");

        Assert.Equal(PlaceOrderFailure.OpenOrderExists, result.Failure);
        Assert.Equal(5, await StockOfAsync(factory, kit));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.CustomerId == "customer-other-tab"));
        Assert.True(await db.Carts.AnyAsync(c => c.CustomerId == "customer-other-tab"));
    }

    [Fact]
    public async Task Database_RefusesTwoOrdersAwaitingPaymentForTheSameCustomer()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Índice Pedido Aberto", stock: 5);
        await TestPreOrders.SaveOrderAsync(factory, kit, 1, customerId: "customer-index");

        var second = await Assert.ThrowsAsync<DbUpdateException>(() => TestPreOrders.SaveOrderAsync(factory, kit, 1, customerId: "customer-index"));

        Assert.Contains("IX_orders_CustomerId_Open", second.InnerException!.Message);
    }

    [Fact]
    public async Task PlaceOrder_WhenTheCartChangesDuringTheQuote_IsRefused_AndNothingIsReserved()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var kit = await NewProductAsync(factory, "Kit Carrinho Mudou", stock: 10);
        await AddToCartAsync(factory, "customer-cart-changed", kit, 1);
        shipping.DuringQuote = () => AddToCartAsync(factory, "customer-cart-changed", kit, 4);

        var result = await PlaceOrderAsync(factory, "customer-cart-changed");

        Assert.Equal(PlaceOrderFailure.CartChanged, result.Failure);
        Assert.Equal(10, await StockOfAsync(factory, kit));
    }

    [Fact]
    public async Task PlaceOrder_WithAnOldCartOverTheLimit_IsRefused_AndNothingIsReserved()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Carrinho Antigo", stock: 50);
        await AddToCartAsync(factory, "customer-old-cart", kit, 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cart_items SET \"Quantity\" = 30 WHERE \"ProductId\" = {kit}");
        }

        var result = await PlaceOrderAsync(factory, "customer-old-cart");

        Assert.Equal(PlaceOrderFailure.QuantityLimit, result.Failure);
        Assert.Equal("Kit Carrinho Antigo", result.ProductName);
        Assert.Equal(50, await StockOfAsync(factory, kit));
    }
}
