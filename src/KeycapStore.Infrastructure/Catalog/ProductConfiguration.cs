using KeycapStore.Domain.Catalog;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Catalog;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.HasData(
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), Name = "Kit Aurora (base)", Price = 349.90m, Stock = 40 },
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"), Name = "Kit Aurora (novelties)", Price = 129.90m, Stock = 3 },
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"), Name = "Kit Maré (base)", Price = 319.90m, Stock = 25 },
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"), Name = "Kit Maré (modificadores)", Price = 149.90m, Stock = 0 },
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"), Name = "Kit Zéfiro (base)", Price = 299.90m, Stock = 12 },
            new { Id = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"), Name = "Kit Zéfiro (teclas de função)", Price = 89.90m, Stock = 2 });
    }
}
