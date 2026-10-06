using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddIdentityCore<IdentityUser>(options =>
                options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
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
