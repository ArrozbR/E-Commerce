using KeycapStore.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Catalog;

public sealed class StockAlert(string customerId, Guid productId, string language, DateTimeOffset createdAt)
{
    public const int MaxCustomerIdLength = 450;

    public const int MaxLanguageLength = 10;

    public string CustomerId { get; private set; } = customerId;

    public Guid ProductId { get; private set; } = productId;

    public string Language { get; private set; } = language;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;
}

internal sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("stock_alerts");
        builder.HasKey(a => new { a.CustomerId, a.ProductId });

        builder.Property(a => a.CustomerId).HasMaxLength(StockAlert.MaxCustomerIdLength);
        builder.Property(a => a.Language).HasMaxLength(StockAlert.MaxLanguageLength);

        builder.HasIndex(a => a.ProductId);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
