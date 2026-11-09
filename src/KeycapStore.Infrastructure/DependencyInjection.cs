using KeycapStore.Application.Carts;
using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Infrastructure.Carts;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.Infrastructure.Orders;
using KeycapStore.Infrastructure.Payments;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KeycapStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Falta a configuração 'ConnectionStrings:Default' (conexão com o PostgreSQL).");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("KeycapStore");

        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IProductAdminService, ProductAdminService>();
        services.AddScoped<IStockAlertService, StockAlertService>();
        services.AddScoped<IStockAlertNotifier, StockAlertNotifier>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IAdminOrderQueries, AdminOrderQueries>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<ICancellationService, CancellationService>();
        services.AddScoped<IWithdrawalService, WithdrawalService>();
        services.AddScoped<ISavedAddressService, SavedAddressService>();
        services.AddScoped<IOrphanOrderReleaser, OrphanOrderReleaser>();
        services.AddScoped<IPreOrderCloser, PreOrderCloser>();
        services.AddHostedService<OrderMaintenanceJob>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddOptions<MelhorEnvioOptions>()
            .Bind(configuration.GetSection(MelhorEnvioOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<IShippingQuotes, MelhorEnvioShippingQuotes>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<CheckoutSessionTransitions>();
        services.AddScoped<IPaymentWebhookHandler, StripeWebhookHandler>();
        services.AddScoped<IRefundProcessor, RefundProcessor>();
        services.AddScoped<IPaymentReconciler, PaymentReconciler>();
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PublicSiteOptions>()
            .Bind(configuration.GetSection(PublicSiteOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddScoped<EmailComposer>();
        services.AddScoped<IEmailOutboxDispatcher, EmailOutboxDispatcher>();

        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 3;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddRoles<IdentityRole>()
        .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
        .AddSignInManager()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = PasswordResetService.LinkLifetime);
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddScoped<IPersonalDataExport, PersonalDataExport>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<DiskSpaceHealthCheck>("disk");

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
