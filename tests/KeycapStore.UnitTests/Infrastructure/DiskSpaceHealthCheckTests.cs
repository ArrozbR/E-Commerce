using KeycapStore.Infrastructure;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KeycapStore.UnitTests.Infrastructure;

public class DiskSpaceHealthCheckTests
{
    private const long Total = 100_000;

    [Fact]
    public void Disk84PercentUsed_IsHealthy()
    {
        Assert.Equal(HealthStatus.Healthy, DiskSpaceHealthCheck.Evaluate(Total, availableBytes: 16_000).Status);
    }

    [Fact]
    public void Disk85PercentUsed_IsUnhealthy()
    {
        Assert.Equal(HealthStatus.Unhealthy, DiskSpaceHealthCheck.Evaluate(Total, availableBytes: 15_000).Status);
    }

    [Fact]
    public void FullDisk_IsUnhealthy()
    {
        Assert.Equal(HealthStatus.Unhealthy, DiskSpaceHealthCheck.Evaluate(Total, availableBytes: 0).Status);
    }
}
