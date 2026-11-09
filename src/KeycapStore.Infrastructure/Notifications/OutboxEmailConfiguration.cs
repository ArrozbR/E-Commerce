using KeycapStore.Domain.Orders;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> builder)
    {
        builder.ToTable("outbox_emails", table => table.HasCheckConstraint(
            "CK_outbox_emails_order_or_customer",
            "\"OrderId\" IS NOT NULL OR \"CustomerId\" IS NOT NULL"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(e => e.CustomerId).HasMaxLength(OutboxEmail.MaxCustomerIdLength);
        builder.Property(e => e.Language).HasMaxLength(OutboxEmail.MaxLanguageLength);

        builder.HasIndex(e => new { e.Kind, e.OrderId }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => new { e.CustomerId, e.Kind, e.CreatedAt });

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
