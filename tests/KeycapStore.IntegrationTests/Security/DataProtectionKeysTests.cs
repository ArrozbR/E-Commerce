using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Security;

public class DataProtectionKeysTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", postgres.Container.GetConnectionString()));

    [Fact]
    public async Task ProtectedData_CanBeReadByANewContainer()
    {
        string locked;

        await using (var oldContainer = CreateFactory())
        {
            await oldContainer.Services.MigrateDatabaseAsync();
            var protector = oldContainer.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("teste");
            locked = protector.Protect("Maria está logada");
        }

        await using (var newContainer = CreateFactory())
        {
            await using var scope = newContainer.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotEmpty(await db.DataProtectionKeys.ToListAsync());

            var protector = newContainer.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("teste");
            Assert.Equal("Maria está logada", protector.Unprotect(locked));
        }
    }
}
