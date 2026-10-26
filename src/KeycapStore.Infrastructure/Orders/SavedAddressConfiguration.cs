using KeycapStore.Domain.Orders;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class SavedAddressConfiguration : IEntityTypeConfiguration<SavedAddress>
{
    public void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        builder.ToTable("saved_addresses");
        builder.HasKey(a => a.CustomerId);

        builder.Property(a => a.CustomerId)
            .HasMaxLength(450);

        builder.OwnsOne(a => a.Address, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("RecipientName").HasMaxLength(150);
            address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200);
            address.Property(a => a.Number).HasColumnName("Number").HasMaxLength(20);
            address.Property(a => a.Complement).HasColumnName("Complement").HasMaxLength(100);
            address.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("State").HasMaxLength(2);
            address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(8);
        });
        builder.Navigation(a => a.Address).IsRequired();
    }
}
