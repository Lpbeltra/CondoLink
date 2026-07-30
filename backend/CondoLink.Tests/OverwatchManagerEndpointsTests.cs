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
        _application.MapUpdateOverwatchManager();
        _application.MapUpdateOverwatchManagerStatus();
        _application.MapCreateOverwatchManagementMembership();
        _application.MapListManagerCondominiums();
        _application.MapRemoveManagerCondominium();
        _application.MapListOverwatchCondominiumManagers();
        _application.MapGetOverwatchCondominiumManager();
        _application.MapReplaceOverwatchCondominiumManager();
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
    public async Task Creates_and_updates_global_manager_profile()
    {
        var response = await _admin.PostAsJsonAsync(
            "/overwatch/managers",
            new {
                fullName = "Profile Manager", email = "profile@example.com",
                phoneNumber = "  (11) 99999-0001  ", cpf = "529.982.247-25",
                cnpj = "04.252.011/0001-10", address = "  Rua A  ",
                city = "  São Paulo  ", state = "sp"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedManagerResponse>();
        var details = await _admin.GetFromJsonAsync<ProfileResponse>(
            $"/overwatch/managers/{created!.Id}");
        Assert.Equal("52998224725", details!.Cpf);
        Assert.Equal("04252011000110", details.Cnpj);
        Assert.Equal("SP", details.State);

        var update = await _admin.PutAsJsonAsync(
            $"/overwatch/managers/{created.Id}",
            new {
                fullName = "Updated Manager", email = created.Email,
                phoneNumber = (string?)null, cpf = (string?)null,
                cnpj = (string?)null, address = (string?)null,
                city = (string?)null, state = (string?)null
            });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }

    [Fact]
    public async Task Rejects_invalid_or_duplicate_manager_documents()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync("/overwatch/managers",
                new { fullName = "Invalid", email = "invalid-doc@example.com",
                    cpf = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await _admin.PostAsJsonAsync("/overwatch/managers",
                new { fullName = "First CPF", email = "first-cpf@example.com",
                    cpf = "529.982.247-25" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _admin.PostAsJsonAsync("/overwatch/managers",
                new { fullName = "Second CPF", email = "second-cpf@example.com",
                    cpf = "52998224725" })).StatusCode);
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
    public async Task Links_one_manager_to_many_condominiums_and_rejects_second_manager()
    {
        var manager = await CreateManagerAsync("Linked", "linked@example.com");
        var other = await CreateManagerAsync("Other", "other@example.com");
        var (firstId, secondId) = await CreateCondominiumsAsync();

        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, secondId)).StatusCode);
        var occupied = await LinkAsync(other.Id, firstId);
        Assert.Equal(HttpStatusCode.Conflict,
            occupied.StatusCode);
        Assert.Contains(
            "já possui um síndico",
            await occupied.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict,
            (await LinkAsync(other.Id, firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await LinkAsync(manager.Id, firstId)).StatusCode);

        var details = await _admin.GetFromJsonAsync<ManagerResponse>(
            $"/overwatch/managers/{manager.Id}");
        var condominiums = await _admin.GetFromJsonAsync<List<CondominiumResponse>>(
            $"/overwatch/managers/{manager.Id}/condominiums");
        var linkedManager = await _admin.GetFromJsonAsync<CondominiumManagerResponse>(
            $"/overwatch/condominiums/{firstId}/manager");

        Assert.Equal(2, details!.CondominiumCount);
        Assert.Equal(2, condominiums!.Count);
        Assert.Equal(manager.Id, linkedManager!.UserId);
    }

    [Fact]
    public async Task Rejects_inactive_manager_and_reactivates_previous_link()
    {
        var manager = await CreateManagerAsync("Reactivation", "reactivation@example.com");
        var other = await CreateManagerAsync("Replacement", "replacement@example.com");
        var (condominiumId, _) = await CreateCondominiumsAsync();

        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, condominiumId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _admin.DeleteAsync(
                $"/overwatch/managers/{manager.Id}/condominiums/{condominiumId}"))
            .StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(manager.Id, condominiumId)).StatusCode);

        await _admin.DeleteAsync(
            $"/overwatch/managers/{manager.Id}/condominiums/{condominiumId}");
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(other.Id, condominiumId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await LinkAsync(manager.Id, condominiumId)).StatusCode);

        await _admin.PatchAsJsonAsync(
            $"/overwatch/managers/{manager.Id}/status",
            new { isActive = false });
        var inactive = await LinkAsync(manager.Id, condominiumId);
        Assert.Equal(HttpStatusCode.Conflict, inactive.StatusCode);
        Assert.Contains("inativo", await inactive.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reactivation_is_blocked_when_preserved_link_would_restore_conflict()
    {
        var previous = await CreateManagerAsync("Previous", "previous@example.com");
        var replacement = await CreateManagerAsync("New Active", "new-active@example.com");
        var (condominiumId, _) = await CreateCondominiumsAsync();
        await LinkAsync(previous.Id, condominiumId);

        Assert.Equal(HttpStatusCode.OK,
            (await _admin.PatchAsJsonAsync(
                $"/overwatch/managers/{previous.Id}/status",
                new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(replacement.Id, condominiumId)).StatusCode);

        var reactivate = await _admin.PatchAsJsonAsync(
            $"/overwatch/managers/{previous.Id}/status",
            new { isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, reactivate.StatusCode);

        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.Users.SingleAsync(
            item => item.Id == previous.Id)).IsActive);
    }

    [Fact]
    public async Task Transactionally_replaces_manager_and_preserves_other_relationships()
    {
        var current = await CreateManagerAsync("Current", "current@example.com");
        var next = await CreateManagerAsync("Next", "next@example.com");
        var (firstId, secondId) = await CreateCondominiumsAsync();
        await LinkAsync(next.Id, firstId);
        await _admin.DeleteAsync(
            $"/overwatch/managers/{next.Id}/condominiums/{firstId}");
        await LinkAsync(current.Id, firstId);
        await LinkAsync(current.Id, secondId);

        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == current.Id);
            user.SetActiveManagementCondominium(firstId);
            var membership = await db.CondominiumMemberships.SingleAsync(item =>
                item.UserId == current.Id && item.CondominiumId == firstId);
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(
                membership.Id, CondominiumRole.Resident));
            await db.SaveChangesAsync();
        }

        var response = await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{firstId}/manager",
            new { managerId = next.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyScope = _application!.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = await verify.Users.SingleAsync(item => item.Id == current.Id);
        var currentMembership = await verify.CondominiumMemberships.SingleAsync(item =>
            item.UserId == current.Id && item.CondominiumId == firstId);
        var currentRoles = await verify.CondominiumMembershipRoles
            .Where(item => item.CondominiumMembershipId == currentMembership.Id)
            .ToListAsync();
        var remaining = await _admin.GetFromJsonAsync<List<CondominiumResponse>>(
            $"/overwatch/managers/{current.Id}/condominiums");

        Assert.Equal(secondId, currentUser.ActiveManagementCondominiumId);
        Assert.Contains(currentRoles, item =>
            item.Role == CondominiumRole.Manager && !item.IsActive);
        Assert.Contains(currentRoles, item =>
            item.Role == CondominiumRole.Resident && item.IsActive);
        Assert.Equal(secondId, Assert.Single(remaining!).CondominiumId);
        Assert.Equal(next.Id,
            (await response.Content.ReadFromJsonAsync<CondominiumManagerResponse>())!.UserId);

        var idempotent = await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{firstId}/manager",
            new { managerId = next.Id });
        Assert.Equal(HttpStatusCode.OK, idempotent.StatusCode);

        var invalid = await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{firstId}/manager",
            new { managerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        Assert.Equal(next.Id,
            (await _admin.GetFromJsonAsync<CondominiumManagerResponse>(
                $"/overwatch/condominiums/{firstId}/manager"))!.UserId);
    }

    [Fact]
    public async Task Concurrent_links_finish_with_exactly_one_manager()
    {
        var first = await CreateManagerAsync("Concurrent One", "concurrent-one@example.com");
        var second = await CreateManagerAsync("Concurrent Two", "concurrent-two@example.com");
        var (condominiumId, _) = await CreateCondominiumsAsync();

        var responses = await Task.WhenAll(
            LinkAsync(first.Id, condominiumId),
            LinkAsync(second.Id, condominiumId));

        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);

        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await (
                from membership in db.CondominiumMemberships
                join role in db.CondominiumMembershipRoles
                    on membership.Id equals role.CondominiumMembershipId
                join user in db.Users on membership.UserId equals user.Id
                where membership.CondominiumId == condominiumId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                    && user.IsActive
                select membership.Id)
            .CountAsync();
        Assert.Equal(1, count);
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

        Assert.Equal(secondId, savedUser.ActiveManagementCondominiumId);
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
    private sealed record ProfileResponse(string? Cpf, string? Cnpj, string? State);
}
