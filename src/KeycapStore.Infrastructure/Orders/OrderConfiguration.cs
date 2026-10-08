using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId)
            .HasMaxLength(450)
            .IsRequired();
        builder.HasIndex(o => o.CustomerId);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.ShippingFee)
            .HasPrecision(10, 2);

        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("ShippingRecipientName").HasMaxLength(150);
            address.Property(a => a.Street).HasColumnName("ShippingStreet").HasMaxLength(200);
            address.Property(a => a.Number).HasColumnName("ShippingNumber").HasMaxLength(20);
            address.Property(a => a.Complement).HasColumnName("ShippingComplement").HasMaxLength(100);
            address.Property(a => a.District).HasColumnName("ShippingDistrict").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("ShippingCity").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("ShippingState").HasMaxLength(2);
            address.Property(a => a.PostalCode).HasColumnName("ShippingPostalCode").HasMaxLength(8);
        });
        builder.Navigation(o => o.ShippingAddress).IsRequired();

        builder.OwnsMany(o => o.Items, items =>
        {
            items.ToTable("order_items");
            items.WithOwner().HasForeignKey("OrderId");
            items.HasKey("OrderId", nameof(OrderItem.ProductId));

            items.Property(i => i.ProductName)
                .HasMaxLength(Product.MaxNameLength);
            items.Property(i => i.UnitPrice)
                .HasPrecision(10, 2);

            items.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
