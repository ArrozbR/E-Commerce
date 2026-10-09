namespace KeycapStore.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public ShippingAddress ShippingAddress { get; private set; }
    public decimal ShippingFee { get; private set; }
    public decimal Total => Items.Sum(i => i.LineTotal) + ShippingFee;
    public string? StripeSessionId { get; private set; }

    private Order()
    {
        CustomerId = null!;
        ShippingAddress = null!;
    }

    public Order(string customerId, DateTimeOffset createdAt, IEnumerable<OrderItem> items, ShippingAddress shippingAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(shippingAddress);

        _items.AddRange(items);

        if (_items.Count == 0)
        {
            throw new ArgumentException("O pedido precisa de pelo menos um item.", nameof(items));
        }

        if (_items.DistinctBy(i => i.ProductId).Count() != _items.Count)
        {
            throw new ArgumentException("O mesmo produto aparece mais de uma vez no pedido.", nameof(items));
        }

        Id = Guid.NewGuid();
        CustomerId = customerId;
        CreatedAt = createdAt;
        Status = OrderStatus.AwaitingPayment;
        ShippingAddress = shippingAddress;
        ShippingFee = ShippingPolicy.FeeFor(shippingAddress.State);
    }

    public void AttachStripeSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (StripeSessionId == sessionId)
        {
            return;
        }

        if (StripeSessionId is not null)
        {
            throw new InvalidOperationException("O pedido já tem outra sessão da Stripe.");
        }

        if (Status is not OrderStatus.AwaitingPayment)
        {
            throw new InvalidOperationException("Só um pedido aguardando pagamento recebe uma sessão da Stripe.");
        }

        StripeSessionId = sessionId;
    }

    public void ConfirmPayment()
    {
        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Paid);
        }

        Status = OrderStatus.Paid;
    }

    public void MarkAwaitingConfirmation()
    {
        if (Status is not OrderStatus.AwaitingPayment)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.AwaitingConfirmation);
        }

        Status = OrderStatus.AwaitingConfirmation;
    }

    public void Expire()
    {
        if (Status is not OrderStatus.AwaitingPayment)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Expired);
        }

        Status = OrderStatus.Expired;
    }

    public void MarkFailed()
    {
        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Failed);
        }

        Status = OrderStatus.Failed;
    }
}
