using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Orders;

public sealed class OrderMaintenanceJob(IServiceScopeFactory scopes, ILogger<OrderMaintenanceJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private const int ReconcileEveryTicks = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        var ticks = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            ticks++;

            await RunAsync<IOrphanOrderReleaser>((releaser, ct) => releaser.ReleaseOrphansAsync(ct), stoppingToken);
            await RunAsync<IRefundProcessor>((refunds, ct) => refunds.RefundPendingAsync(ct), stoppingToken);

            if (ticks % ReconcileEveryTicks == 0)
            {
                await RunAsync<IPaymentReconciler>((reconciler, ct) => reconciler.ReconcileAsync(ct), stoppingToken);
            }
        }
    }

    private async Task RunAsync<TTask>(Func<TTask, CancellationToken, Task<int>> work, CancellationToken stoppingToken)
        where TTask : notnull
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await work(scope.ServiceProvider.GetRequiredService<TTask>(), stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha na tarefa {Task}; nova tentativa na próxima rodada", typeof(TTask).Name);
        }
    }
}
