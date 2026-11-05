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

    public DateTimeOffset? PreOrderEndsAt { get; private set; }
    public DateTimeOffset? PreOrderShipsAt { get; private set; }
    public int? PreOrderMinimumUnits { get; private set; }
    public PreOrderOutcome? PreOrderOutcome { get; private set; }
    public DateTimeOffset? PreOrderOpenedAt { get; private set; }

    public string PaletteKeys { get; private set; }
    public string PaletteLegends { get; private set; }
    public string PaletteAccent { get; private set; }

    public ProductPackage Package => new(WeightKg, WidthCm, HeightCm, LengthCm);

    public KitPalette Palette => new(PaletteKeys, PaletteLegends, PaletteAccent);

    public PreOrder? PreOrder => PreOrderEndsAt is { } endsAt
        ? new PreOrder(endsAt, PreOrderShipsAt!.Value, PreOrderMinimumUnits!.Value)
        : null;

    private Product()
    {
        Name = null!;
        PaletteKeys = null!;
        PaletteLegends = null!;
        PaletteAccent = null!;
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

        PaletteKeys = KitPalette.Neutral.Keys;
        PaletteLegends = KitPalette.Neutral.Legends;
        PaletteAccent = KitPalette.Neutral.Accent;
    }

    public void ChangePalette(KitPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        PaletteKeys = palette.Keys;
        PaletteLegends = palette.Legends;
        PaletteAccent = palette.Accent;
    }

    public static Product ForPreOrder(string name, decimal price, int slots, ProductPackage package, PreOrder preOrder, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(preOrder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slots);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(preOrder.EndsAt, now, nameof(preOrder));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(preOrder.ShipsAt, now + PreOrder.MaxLeadTime, nameof(preOrder));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(preOrder.MinimumUnits, slots, nameof(preOrder));

        return new Product(name, price, slots, package)
        {
            PreOrderEndsAt = preOrder.EndsAt,
            PreOrderShipsAt = preOrder.ShipsAt,
            PreOrderMinimumUnits = preOrder.MinimumUnits,
            PreOrderOpenedAt = now.ToUniversalTime(),
        };
    }

    public void ReschedulePreOrder(DateTimeOffset endsAt, DateTimeOffset shipsAt, DateTimeOffset now)
    {
        if (PreOrder is not { } current || PreOrderOpenedAt is null)
        {
            throw new InvalidOperationException("O produto não é de pré-venda.");
        }

        if (PreOrderOutcome is not null)
        {
            throw new InvalidOperationException("A pré-venda já foi fechada.");
        }

        var schedule = new PreOrder(endsAt, shipsAt, current.MinimumUnits);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(schedule.EndsAt, now, nameof(endsAt));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(schedule.ShipsAt, PreOrderOpenedAt!.Value + PreOrder.MaxLeadTime, nameof(shipsAt));

        PreOrderEndsAt = schedule.EndsAt;
        PreOrderShipsAt = schedule.ShipsAt;
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

    public void ConfirmPreOrder() => ClosePreOrder(Catalog.PreOrderOutcome.Confirmed);

    public void CancelPreOrder() => ClosePreOrder(Catalog.PreOrderOutcome.Cancelled);

    private void ClosePreOrder(PreOrderOutcome outcome)
    {
        if (PreOrder is null)
        {
            throw new InvalidOperationException("O produto não é de pré-venda.");
        }

        if (PreOrderOutcome is not null)
        {
            throw new InvalidOperationException("A pré-venda já foi fechada.");
        }

        PreOrderOutcome = outcome;
        IsActive = false;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        if (PreOrderOutcome is not null)
        {
            throw new InvalidOperationException("Uma pré-venda fechada não volta à venda.");
        }

        IsActive = true;
    }
}
