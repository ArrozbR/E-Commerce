using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class UserPasswordTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CreateUser_StoresPasswordAsHash()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await factory.Services.MigrateDatabaseAsync();

        const string email = "maria@example.com";
        const string password = "Senha#Forte123";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var result = await userManager.CreateAsync(new IdentityUser { UserName = email, Email = email }, password);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Users.SingleAsync(u => u.Email == email);

            Assert.NotNull(saved.PasswordHash);
            Assert.DoesNotContain(password, saved.PasswordHash);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True(await userManager.CheckPasswordAsync(saved, password));
        }
    }
}
