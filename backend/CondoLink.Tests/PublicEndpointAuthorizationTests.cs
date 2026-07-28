using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Condominiums;
using CondoLink.Api.Features.Users;
using CondoLink.Domain.Entities;
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

namespace CondoLink.Tests;

/// <summary>
/// Guards the pre-authentication attack surface: no endpoint other than
/// Only authentication bootstrap endpoints may be reachable without a token.
/// </summary>
public sealed class PublicEndpointAuthorizationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _anonymous = null!;
    private HttpClient _authenticated = null!;

    public async Task InitializeAsync()
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
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                options.DefaultForbidScheme = TestAuthHandler.TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.TestScheme, _ => { });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(
                DependencyInjection.PlatformAdminPolicy,
                policy => policy.RequireRole(
                    DependencyInjection.PlatformAdminRole)));

        _application = builder.Build();
        _application.UseAuthentication();
        _application.UseAuthorization();
        _application.MapListCondominiums();
        _application.MapGetCondominiumById();
        _application.MapCreateUser();
        await _application.StartAsync();

        _anonymous = _application.GetTestClient();
        _authenticated = _application.GetTestClient();
        _authenticated.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        db.Condominiums.Add(new Condominium("Residencial Alfa", "alfa@example.com", "1199999999"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        _authenticated.Dispose();
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Listing_condominiums_without_a_token_is_rejected()
    {
        var response = await _anonymous.GetAsync("/condominiums");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_condominium_without_a_token_is_rejected()
    {
        Guid id;
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            id = await db.Condominiums.Select(item => item.Id).FirstAsync();
        }

        var response = await _anonymous.GetAsync($"/condominiums/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_user_without_a_token_is_rejected()
    {
        var response = await _anonymous.PostAsJsonAsync(
            "/users",
            new
            {
                fullName = "Invasor",
                email = "invasor@example.com",
                password = "Passw0rd1",
                phoneNumber = (string?)null
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(
            await db.Users.AnyAsync(user => user.Email == "invasor@example.com"),
            "Anonymous account creation must not persist a user.");
    }

    [Fact]
    public async Task Authenticated_member_can_still_list_condominiums()
    {
        var response = await _authenticated.GetAsync("/condominiums");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_user_as_a_non_admin_member_is_forbidden()
    {
        var response = await _authenticated.PostAsJsonAsync(
            "/users",
            new
            {
                fullName = "Morador",
                email = "morador@example.com",
                password = "Passw0rd1",
                phoneNumber = (string?)null
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
