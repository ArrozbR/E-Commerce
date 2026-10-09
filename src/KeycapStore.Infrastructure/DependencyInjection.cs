using KeycapStore.Application.Carts;
using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Infrastructure.Carts;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.Infrastructure.Payments;

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
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 3;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddRoles<IdentityRole>()
        .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
        .AddSignInManager()
        .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IAccountService, AccountService>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
