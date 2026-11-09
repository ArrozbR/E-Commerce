using KeycapStore.Domain.Orders;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> builder)
    {
        builder.ToTable("outbox_emails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.Kind, e.OrderId }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
