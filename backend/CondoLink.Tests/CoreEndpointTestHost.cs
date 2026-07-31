using System.Security.Claims;
using System.Text.Encodings.Web;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

/// <summary>
/// Shared harness for the endpoint-level integration tests added for the
/// product-core areas (requests, units, categories, unit memberships,
/// condominium members, blocks and login).
///
/// The stock <see cref="TestAuthHandler"/> issues a random NameIdentifier per
/// request, which is useless for endpoints that resolve the caller's identity
/// from the database. <see cref="IdentityAwareTestAuthHandler"/> below reads the
/// user id from an <c>X-Test-User</c> header instead, so a test can act as a
/// specific seeded user.
/// </summary>
internal sealed class CoreEndpointTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");
    private readonly List<HttpClient> _clients = [];
    private WebApplication _application = null!;

    public static async Task<CoreEndpointTestHost> StartAsync(
        Action<WebApplication> mapEndpoints,
        Action<WebApplicationBuilder>? configureServices = null)
    {
        var host = new CoreEndpointTestHost();
        await host.InitializeAsync(mapEndpoints, configureServices);
        return host;
    }

    private async Task InitializeAsync(
        Action<WebApplication> mapEndpoints,
        Action<WebApplicationBuilder>? configureServices)
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite(_connection));
        builder.Services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    IdentityAwareTestAuthHandler.TestScheme;
                options.DefaultChallengeScheme =
                    IdentityAwareTestAuthHandler.TestScheme;
                options.DefaultForbidScheme =
                    IdentityAwareTestAuthHandler.TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions,
                IdentityAwareTestAuthHandler>(
                IdentityAwareTestAuthHandler.TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(
                DependencyInjection.PlatformAdminPolicy,
                policy => policy.RequireRole(
                    DependencyInjection.PlatformAdminRole)));
        // Request endpoints take these collaborators the same way Program.cs
        // registers them, so mapping them here needs the same registrations.
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<CondominiumMembershipService>();
        builder.Services.AddScoped<AuthenticationSessionService>();
        configureServices?.Invoke(builder);

        _application = builder.Build();
        _application.UseAuthentication();
        _application.UseAuthorization();
        mapEndpoints(_application);
        await _application.StartAsync();

        await using var scope = _application.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.EnsureCreatedAsync();
    }

    /// <summary>A client that authenticates as the given seeded user id.</summary>
    public HttpClient ClientFor(Guid userId)
    {
        var client = _application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        _clients.Add(client);
        return client;
    }

    /// <summary>A client with no credentials at all.</summary>
    public HttpClient AnonymousClient()
    {
        var client = _application.GetTestClient();
        _clients.Add(client);
        return client;
    }

    /// <summary>Runs work against a fresh scoped <see cref="AppDbContext"/>.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        await using var scope = _application.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _application.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithServicesAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = _application.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _application.StopAsync();
        await _application.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>
/// Seeding helpers shared by the core-area test classes.
/// </summary>
internal static class CoreTestSeed
{
    public static ApplicationUser User(string fullName, string email)
    {
        var user = new ApplicationUser(fullName, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        return user;
    }

    public static CondominiumMembership AddMember(
        AppDbContext db,
        Guid userId,
        Guid condominiumId,
        params CondominiumRole[] roles)
    {
        var membership = new CondominiumMembership(userId, condominiumId);
        db.CondominiumMemberships.Add(membership);

        foreach (var role in roles)
        {
            db.CondominiumMembershipRoles.Add(
                new CondominiumMembershipRole(membership.Id, role));
        }

        return membership;
    }
}

internal sealed class IdentityAwareTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string TestScheme = "IdentityAwareTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test User")
        };

        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        var identity = new ClaimsIdentity(claims, TestScheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity), TestScheme)));
    }
}
