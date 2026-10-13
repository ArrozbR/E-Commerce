using KeycapStore.Application.Orders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Orders;

public sealed class OrphanOrderJob(IServiceScopeFactory scopes, ILogger<OrphanOrderJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var releaser = scope.ServiceProvider.GetRequiredService<IOrphanOrderReleaser>();
                await releaser.ReleaseOrphansAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao liberar pedidos órfãos; nova tentativa na próxima rodada");
            }
        }
    }
}
