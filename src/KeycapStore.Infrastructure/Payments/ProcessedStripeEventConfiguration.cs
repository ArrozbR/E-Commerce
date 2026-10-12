using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("processed_stripe_events");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId).HasMaxLength(255);
        builder.Property(e => e.Type).HasMaxLength(100);
    }
}
