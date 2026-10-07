namespace KeycapStore.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Order(string customerId, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        Id = Guid.NewGuid();
        CustomerId = customerId;
        CreatedAt = createdAt;
        Status = OrderStatus.AwaitingPayment;
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
