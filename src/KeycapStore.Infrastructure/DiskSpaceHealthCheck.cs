using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KeycapStore.Infrastructure;

public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    public const double MaxUsedFraction = 0.85;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory)!);

        return Task.FromResult(Evaluate(drive.TotalSize, drive.AvailableFreeSpace));
    }

    public static HealthCheckResult Evaluate(long totalBytes, long availableBytes)
    {
        var usedFraction = (double)(totalBytes - availableBytes) / totalBytes;

        return usedFraction < MaxUsedFraction
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Disco com {usedFraction:P0} de uso.");
    }
}
