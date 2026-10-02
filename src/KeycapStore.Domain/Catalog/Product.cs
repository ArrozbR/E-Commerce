namespace KeycapStore.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }

    public Product(string name, decimal price, int stock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        Id = Guid.NewGuid();
        Name = name;
        Price = price;
        Stock = stock;
    }
}
