namespace KeycapStore.Domain.Orders;

public sealed class Order
{
    public const int MaxTrackingCodeLength = 50;
    public static readonly TimeSpan WithdrawalPeriod = TimeSpan.FromDays(7);

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
    public string? StripePaymentIntentId { get; private set; }
    public string? TrackingCode { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

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

    public void ConfirmPayment(string? paymentIntentId = null)
    {
        if (Status is OrderStatus.Paid)
        {
            return;
        }

        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Paid);
        }

        StripePaymentIntentId ??= paymentIntentId;

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


    public void ConfirmLatePayment(string? paymentIntentId = null)
    {
        if (Status is not (OrderStatus.Expired or OrderStatus.Failed))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Paid);
        }

        StripePaymentIntentId ??= paymentIntentId;

        Status = OrderStatus.Paid;
    }

    public void MarkOutOfStock(string paymentIntentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);

        if (Status is not (OrderStatus.Expired or OrderStatus.Failed))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.PaidOutOfStock);
        }

        StripePaymentIntentId = paymentIntentId;
        Status = OrderStatus.PaidOutOfStock;
    }

    public void MarkRefunded()
    {
        if (Status is OrderStatus.Withdrawn)
        {
            Status = OrderStatus.AwaitingReturn;
            return;
        }

        if (Status is not (OrderStatus.PaidOutOfStock or OrderStatus.Cancelled))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Refunded);
        }

        Status = OrderStatus.Refunded;
    }

    public void StartPicking()
    {
        if (Status is not OrderStatus.Paid)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Picking);
        }

        Status = OrderStatus.Picking;
    }

    public void MarkShipped(string trackingCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingCode);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(trackingCode.Length, MaxTrackingCodeLength, nameof(trackingCode));

        if (Status is not OrderStatus.Picking)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Shipped);
        }

        TrackingCode = trackingCode;
        Status = OrderStatus.Shipped;
    }

    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        if (Status is not OrderStatus.Shipped)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Delivered);
        }

        DeliveredAt = deliveredAt;

        Status = OrderStatus.Delivered;
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Paid or OrderStatus.Picking))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Cancelled);
        }

        Status = OrderStatus.Cancelled;
    }

    public bool CanWithdraw(DateTimeOffset now)
    {
        bool inTransit = Status is OrderStatus.Shipped;
        bool withinPeriod = Status is OrderStatus.Delivered && DeliveredAt is not null && now <= DeliveredAt.Value + WithdrawalPeriod;

        return inTransit || withinPeriod;
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (!CanWithdraw(now))
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Withdrawn);
        }

        Status = OrderStatus.Withdrawn;
    }

    public void ConfirmReturn()
    {
        if (Status is not OrderStatus.AwaitingReturn)
        {
            throw new InvalidOrderTransitionException(Status, OrderStatus.Returned);
        }

        Status = OrderStatus.Returned;
    }
}
