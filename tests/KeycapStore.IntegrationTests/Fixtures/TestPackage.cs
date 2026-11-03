using KeycapStore.Domain.Catalog;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class TestPackage
{
    public static ProductPackage Standard { get; } = new(0.8m, 30, 6, 20);
}
