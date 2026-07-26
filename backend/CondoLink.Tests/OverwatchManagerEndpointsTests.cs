using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Api.Features.Overwatch.Managers;
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

namespace CondoLink.Tests;

public sealed class OverwatchManagerEndpointsTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite(_connection));
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddScoped<ManagerOnboardingService>();
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
        _application.MapCreateOverwatchManager();
        _application.MapListOverwatchManagers();
        _application.MapGetOverwatchManager();
        _application.MapUpdateOverwatchManagerStatus();
        _application.MapCreateOverwatchManagementMembership();
        _application.MapListManagerCondominiums();
        _application.MapRemoveManagerCondominium();
        _application.MapListOverwatchCondominiumManagers();
        await _application.StartAsync();

        _admin = _application.GetTestClient();
        _admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        await using var scope = _application.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _admin.Dispose();
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Creates_lists_and_gets_manager_with_temporary_credentials()
    {
        var created = await CreateManagerAsync("  Manager One  ", "  MANAGER@EXAMPLE.COM ");
        var items = await _admin.GetFromJsonAsync<List<ManagerResponse>>(
            "/overwatch/managers");
        var details = await _admin.GetFromJsonAsync<ManagerResponse>(
            $"/overwatch/managers/{created.Id}");

        Assert.Equal("Manager One", created.FullName);
        Assert.Equal("manager@example.com", created.Email);
        Assert.False(string.IsNullOrWhiteSpace(created.TemporaryPassword));
        Assert.Equal(created.Id, Assert.Single(items!).Id);
        Assert.Equal(0, details!.CondominiumCount);
    }

    [Fact]
    public async Task Rejects_duplicate_email_and_protects_endpoints()
    {
        await CreateManagerAsync("First", "duplicate@example.com");
        var duplicate = await _admin.PostAsJsonAsync(
            "/overwatch/managers",
            new { fullName = "Second", email = "duplicate@example.com" });
        using var common = _application!.GetTestClient();
        common.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await common.GetAsync("/overwatch/managers")).StatusCode);
    }

    [Fact]
    public async Task Updates_status_and_missing_details_return_404()
    {
        var manager = await CreateManagerAsync("Status", "status@example.com");
        var response = await _admin.PatchAsJsonAsync(
            $"/overwatch/managers/{manager.Id}/status",
            new { isActive = false });
        var updated = await response.Content.ReadFromJsonAsync<StatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(updated!.IsActive);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _admin.GetAsync($"/overwatch/managers/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Link_rejects_missing_condominium_and_user_without_manager_role()
    {
        var manager = await CreateManagerAsync("Valid", "valid@example.com");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await LinkAsync(manager.Id, Guid.NewGuid())).StatusCode);

        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var condominium = new Condominium("Target", null, null);
        var resident = new ApplicationUser(
            "Resident", "resident-only@example.com", null);
        resident.NormalizedUserName = "RESIDENT-ONLY@EXAMPLE.COM";
        resident.NormalizedEmail = "RESIDENT-ONLY@EXAMPLE.COM";
        db.AddRange(condominium, resident);
        await db.SaveChangesAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await LinkAsync(resident.Id, condominium.Id)).StatusCode);
    }

    [Fact]
    public async Task Links_from_both_perspectives_counts_and_rejects_duplicate()
    {
        var manager = await CreateManagerAsync("Linked", "linked@example.com");
        var other = await CreateManagerAsync("Other", "other@example.com");
        var (firstId, secondId) = await CreateCondominiumsAsync();

        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, secondId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(other.Id, firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await LinkAsync(manager.Id, firstId)).StatusCode);

        var details = await _admin.GetFromJsonAsync<ManagerResponse>(
            $"/overwatch/managers/{manager.Id}");
        var condominiums = await _admin.GetFromJsonAsync<List<CondominiumResponse>>(
            $"/overwatch/managers/{manager.Id}/condominiums");
        var managers = await _admin.GetFromJsonAsync<List<CondominiumManagerResponse>>(
            $"/overwatch/condominiums/{firstId}/managers");

        Assert.Equal(2, details!.CondominiumCount);
        Assert.Equal(2, condominiums!.Count);
        Assert.Equal(2, managers!.Count);
    }

    [Fact]
    public async Task Removal_only_revokes_manager_role_and_clears_active_context()
    {
        var manager = await CreateManagerAsync("Removal", "removal@example.com");
        var (firstId, secondId) = await CreateCondominiumsAsync();
        await LinkAsync(manager.Id, firstId);
        await LinkAsync(manager.Id, secondId);

        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == manager.Id);
            user.SetActiveManagementCondominium(firstId);
            var membership = await db.CondominiumMemberships.SingleAsync(item =>
                item.UserId == manager.Id && item.CondominiumId == firstId);
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(
                membership.Id, CondominiumRole.Resident));
            await db.SaveChangesAsync();
        }

        var response = await _admin.DeleteAsync(
            $"/overwatch/managers/{manager.Id}/condominiums/{firstId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyScope = _application!.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await verify.Users.SingleAsync(item => item.Id == manager.Id);
        var membershipId = await verify.CondominiumMemberships
            .Where(item => item.UserId == manager.Id && item.CondominiumId == firstId)
            .Select(item => item.Id)
            .SingleAsync();
        var roles = await verify.CondominiumMembershipRoles
            .Where(item => item.CondominiumMembershipId == membershipId)
            .ToListAsync();
        var remaining = await _admin.GetFromJsonAsync<List<CondominiumResponse>>(
            $"/overwatch/managers/{manager.Id}/condominiums");

        Assert.Null(savedUser.ActiveManagementCondominiumId);
        Assert.Contains(roles, role =>
            role.Role == CondominiumRole.Manager && !role.IsActive);
        Assert.Contains(roles, role =>
            role.Role == CondominiumRole.Resident && role.IsActive);
        Assert.Equal(secondId, Assert.Single(remaining!).CondominiumId);
    }

    private async Task<CreatedManagerResponse> CreateManagerAsync(
        string fullName, string email)
    {
        var response = await _admin.PostAsJsonAsync(
            "/overwatch/managers", new { fullName, email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedManagerResponse>())!;
    }

    private Task<HttpResponseMessage> LinkAsync(Guid managerId, Guid condominiumId)
        => _admin.PostAsJsonAsync(
            "/overwatch/management-memberships",
            new { managerId, condominiumId });

    private async Task<(Guid FirstId, Guid SecondId)> CreateCondominiumsAsync()
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = new Condominium($"Alpha {Guid.NewGuid()}", null, null);
        var second = new Condominium($"Beta {Guid.NewGuid()}", null, null);
        db.Condominiums.AddRange(first, second);
        await db.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    private sealed record CreatedManagerResponse(
        Guid Id, string FullName, string Email, string TemporaryPassword);
    private sealed record ManagerResponse(Guid Id, int CondominiumCount);
    private sealed record StatusResponse(bool IsActive);
    private sealed record CondominiumResponse(Guid CondominiumId);
    private sealed record CondominiumManagerResponse(Guid UserId);
}
