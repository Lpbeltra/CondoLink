using CondoLink.Api;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class PlatformAdminInitializerTests
{
    [Fact]
    public async Task Existing_active_platform_admin_prevents_configured_seed_duplicate()
    {
        var (app, connection) = await CreateAppAsync();
        await using (app)
        await using (connection)
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var roles = scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var users = scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(
                    DependencyInjection.PlatformAdminRole))).Succeeded);
                var existing = new ApplicationUser(
                    "Existing Admin", "existing-admin@example.test", null);
                Assert.True((await users.CreateAsync(existing, "Valid1!Password")).Succeeded);
                Assert.True((await users.AddToRoleAsync(existing,
                    DependencyInjection.PlatformAdminRole)).Succeeded);
            }

            await app.InitializePlatformAdminAsync();

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Null(await db.Users.SingleOrDefaultAsync(user =>
                user.NormalizedEmail == "CONFIGURED-SEED@EXAMPLE.TEST"));
        }
    }

    [Fact]
    public async Task Empty_installation_creates_configured_platform_admin()
    {
        var (app, connection) = await CreateAppAsync();
        await using (app)
        await using (connection)
        {
            await app.InitializePlatformAdminAsync();

            await using var scope = app.Services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var created = await users.FindByEmailAsync(
                "configured-seed@example.test");
            Assert.NotNull(created);
            Assert.True(created.IsActive);
            Assert.True(await users.IsInRoleAsync(created,
                DependencyInjection.PlatformAdminRole));
        }
    }

    private static async Task<(WebApplication App, SqliteConnection Connection)>
        CreateAppAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformAdmin:Email"] = "configured-seed@example.test",
            ["PlatformAdmin:Password"] = "Valid1!Password"
        });
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connection));
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.EnsureCreatedAsync();
        return (app, connection);
    }
}
