using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace KeycapStore.Infrastructure.Notifications;

public sealed class NewsletterSubscription(string customerId, string language, DateTimeOffset subscribedAt)
{
    public string CustomerId { get; private set; } = customerId;

    public string Language { get; private set; } = language;

    public DateTimeOffset SubscribedAt { get; private set; } = subscribedAt;
}

public sealed class ProductAnnouncement(Guid productId, DateTimeOffset announcedAt)
{
    public const string PrimaryKey = "PK_product_announcements";

    public Guid ProductId { get; private set; } = productId;

    public DateTimeOffset AnnouncedAt { get; private set; } = announcedAt;
}

internal sealed class NewsletterSubscriptionConfiguration : IEntityTypeConfiguration<NewsletterSubscription>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscription> builder)
    {
        builder.ToTable("newsletter_subscriptions");
        builder.HasKey(s => s.CustomerId);
        builder.Property(s => s.CustomerId).HasMaxLength(OutboxEmail.MaxCustomerIdLength);
        builder.Property(s => s.Language).HasMaxLength(OutboxEmail.MaxLanguageLength);
    }
}

internal sealed class ProductAnnouncementConfiguration : IEntityTypeConfiguration<ProductAnnouncement>
{
    public void Configure(EntityTypeBuilder<ProductAnnouncement> builder)
    {
        builder.ToTable("product_announcements");
        builder.HasKey(a => a.ProductId).HasName(ProductAnnouncement.PrimaryKey);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NewsletterService(AppDbContext db, TimeProvider clock) : INewsletterService
{
    public async Task SubscribeAsync(string customerId, string language, CancellationToken cancellationToken = default)
    {
        if (await IsSubscribedAsync(customerId, cancellationToken))
        {
            return;
        }

        db.NewsletterSubscriptions.Add(new NewsletterSubscription(customerId, language, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
        }
    }

    public Task UnsubscribeAsync(string customerId, CancellationToken cancellationToken = default) =>
        db.NewsletterSubscriptions.Where(s => s.CustomerId == customerId).ExecuteDeleteAsync(cancellationToken);

    public Task<bool> IsSubscribedAsync(string customerId, CancellationToken cancellationToken = default) =>
        db.NewsletterSubscriptions.AnyAsync(s => s.CustomerId == customerId, cancellationToken);
}

internal sealed class ProductAnnouncer(AppDbContext db, TimeProvider clock, ILogger<ProductAnnouncer> logger) : IProductAnnouncer
{
    public const int ProductsPerRound = 10;

    public async Task<int> AnnounceAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Stock > 0
                && (p.PreOrderEndsAt == null || (p.PreOrderEndsAt > now && p.PreOrderOutcome == null))
                && !db.ProductAnnouncements.Any(a => a.ProductId == p.Id))
            .Select(p => new { p.Id, IsPreOrder = p.PreOrderEndsAt != null })
            .Take(ProductsPerRound)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return 0;
        }

        var subscribers = await db.NewsletterSubscriptions
            .AsNoTracking()
            .Select(s => new { s.CustomerId, s.Language })
            .ToListAsync(cancellationToken);

        var announced = 0;
        foreach (var product in products)
        {
            var kind = product.IsPreOrder ? EmailKind.PreOrderOpened : EmailKind.NewKit;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            db.ProductAnnouncements.Add(new ProductAnnouncement(product.Id, now));
            foreach (var subscriber in subscribers)
            {
                db.OutboxEmails.Add(OutboxEmail.AboutProduct(kind, subscriber.CustomerId, product.Id, subscriber.Language, now));
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                announced++;
                logger.LogInformation("Kit {ProductId} anunciado ({Kind}) para {Count} inscritos", product.Id, kind, subscribers.Count);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ProductAnnouncement.PrimaryKey })
            {
                logger.LogInformation("Kit {ProductId} já foi anunciado por outra rodada", product.Id);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return announced;
    }
}
