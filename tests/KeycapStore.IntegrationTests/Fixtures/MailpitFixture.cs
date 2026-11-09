using System.Text.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class MailpitFixture : IAsyncLifetime
{
    public const string AllowedDomain = "example.com";

    private const int SmtpPort = 1025;

    private const int ApiPort = 8025;

    public IContainer Container { get; } = new ContainerBuilder("axllent/mailpit:v1.32.0")
        .WithEnvironment("MP_SMTP_ALLOWED_RECIPIENTS", @"@example\.com$")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(ApiPort).ForPath("/api/v1/messages")))
        .Build();

    public string Host => Container.Hostname;

    public int Port => Container.GetMappedPublicPort(SmtpPort);

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();

    public async Task<IReadOnlyList<(string Subject, string To)>> MessagesAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(ApiPort)}") };
        using var json = JsonDocument.Parse(await http.GetStringAsync("/api/v1/messages"));

        return [.. json.RootElement.GetProperty("messages").EnumerateArray()
            .SelectMany(message => message.GetProperty("To").EnumerateArray()
                .Select(to => (message.GetProperty("Subject").GetString() ?? "", to.GetProperty("Address").GetString() ?? "")))];
    }
}
