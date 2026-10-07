using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Carts;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerId)
            .IsRequired();
        builder.HasIndex(c => c.CustomerId)
            .IsUnique();

        builder.OwnsMany(c => c.Items, items =>
        {
            items.ToTable("cart_items");
            items.WithOwner().HasForeignKey("CartId");
            items.HasKey("CartId", nameof(CartItem.ProductId));

            items.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(c => c.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
