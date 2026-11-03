namespace KeycapStore.Domain.Catalog;

public sealed class Product
{
    public const int MaxNameLength = 150;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }
    public decimal WeightKg { get; private set; }
    public int WidthCm { get; private set; }
    public int HeightCm { get; private set; }
    public int LengthCm { get; private set; }

    public ProductPackage Package => new(WeightKg, WidthCm, HeightCm, LengthCm);

    private Product()
    {
        Name = null!;
    }

    public Product(string name, decimal price, int stock, ProductPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, MaxNameLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        Id = Guid.NewGuid();
        Name = name;
        Price = price;
        Stock = stock;
        IsActive = true;
        ChangePackage(package);
    }

    public void Update(string name, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, MaxNameLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        Name = name;
        Price = price;
    }

    public void ChangePackage(ProductPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        WeightKg = package.WeightKg;
        WidthCm = package.WidthCm;
        HeightCm = package.HeightCm;
        LengthCm = package.LengthCm;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
