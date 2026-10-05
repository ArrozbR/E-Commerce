using KeycapStore.Application.Catalog;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.Infrastructure;

// O "cadastro do almoxarifado" de tudo que a Infrastructure fornece.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // A string de conexão vem da configuração: no PC, do user-secrets; na VM, do .env.
        // Se faltar, o site NÃO SOBE, com uma mensagem clara (ADR 0014: falhar cedo).
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Falta a configuração 'ConnectionStrings:Default' (conexão com o PostgreSQL).");

        // "Quando alguém pedir AppDbContext, crie um conectado a este PostgreSQL."
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // "Quando alguém pedir ICatalogQueries, entregue um CatalogQueries."
        services.AddScoped<ICatalogQueries, CatalogQueries>();

        return services;
    }
}
