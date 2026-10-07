using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;

namespace KeycapStore.IntegrationTests.Smoke;

public class SmokeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task HomePage_ShouldRespondOk()
    {
        await using var factory = postgres.CreateFactory();

        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Postgres_ShouldAcceptSqlCommands()
    {
        var result = await postgres.Container.ExecScriptAsync("SELECT 1;");

        Assert.True(result.ExitCode == 0, $"O comando SQL falhou: {result.Stderr}");
    }
}
