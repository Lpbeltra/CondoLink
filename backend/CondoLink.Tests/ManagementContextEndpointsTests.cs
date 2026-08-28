using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class ManagementContextEndpointsTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite(_connection));
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                options.DefaultForbidScheme = TestAuthHandler.TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.TestScheme, _ => { });
        builder.Services.AddAuthorization();

        _application = builder.Build();
        _application.UseAuthentication();
        _application.UseAuthorization();
        _application.MapManagementContext();
        _application.MapListCondominiumRequests();
        await _application.StartAsync();

        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = User("Manager", "context@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;

        _client = _application.GetTestClient();
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", _userId.ToString());
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task No_condominiums_clears_stale_context()
    {
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == _userId);
            user.SetActiveManagementCondominium(Guid.NewGuid());
            await db.SaveChangesAsync();
        }

        var context = await GetContextAsync();

        Assert.Equal(0, context.CondominiumCount);
        Assert.Null(context.ActiveManagementCondominiumId);
        Assert.False(context.UsesConsolidatedManagementScope);
        Assert.Empty(context.AvailableCondominiums);
        await using var verifyScope = _application!.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await verify.Users.SingleAsync(
            item => item.Id == _userId)).ActiveManagementCondominiumId);
    }

    [Fact]
    public async Task One_condominium_is_selected_and_persisted_automatically()
    {
        var condominium = await AddManagedCondominiumAsync("Único");

        var context = await GetContextAsync();

        Assert.Equal(condominium.Id, context.ActiveManagementCondominiumId);
        Assert.Equal(condominium.Id, context.ActiveCondominium!.Id);
        Assert.False(context.UsesConsolidatedManagementScope);
        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(condominium.Id, (await db.Users.SingleAsync(
            item => item.Id == _userId)).ActiveManagementCondominiumId);
    }

    [Fact]
    public async Task Multiple_condominiums_default_to_consolidated_and_preserve_valid_selection()
    {
        var first = await AddManagedCondominiumAsync("Alfa");
        await AddManagedCondominiumAsync("Beta");

        var consolidated = await GetContextAsync();
        Assert.Null(consolidated.ActiveManagementCondominiumId);
        Assert.True(consolidated.UsesConsolidatedManagementScope);
        Assert.Equal(2, consolidated.CondominiumCount);

        var selectedResponse = await _client.PutAsJsonAsync(
            "/management/context", new { condominiumId = first.Id });
        var selected = await selectedResponse.Content
            .ReadFromJsonAsync<ContextResponse>();
        Assert.Equal(HttpStatusCode.OK, selectedResponse.StatusCode);
        Assert.Equal(first.Id, selected!.ActiveManagementCondominiumId);
        Assert.False(selected.UsesConsolidatedManagementScope);
        Assert.Equal(first.Id, (await GetContextAsync()).ActiveManagementCondominiumId);

        var allResponse = await _client.PutAsJsonAsync(
            "/management/context", new { condominiumId = (Guid?)null });
        var all = await allResponse.Content.ReadFromJsonAsync<ContextResponse>();
        Assert.Null(all!.ActiveManagementCondominiumId);
        Assert.True(all.UsesConsolidatedManagementScope);
    }

    [Fact]
    public async Task Invalid_or_inactive_condominium_is_rejected_or_reconciled()
    {
        var condominium = await AddManagedCondominiumAsync("Ativo");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.PutAsJsonAsync(
                "/management/context",
                new { condominiumId = Guid.NewGuid() })).StatusCode);

        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Condominiums.SingleAsync(
                item => item.Id == condominium.Id);
            saved.SetActiveStatus(false);
            await db.SaveChangesAsync();
        }

        var context = await GetContextAsync();
        Assert.Empty(context.AvailableCondominiums);
        Assert.Null(context.ActiveManagementCondominiumId);
    }

    [Fact]
    public async Task Requests_support_consolidated_and_specific_contexts()
    {
        var first = await AddManagedCondominiumAsync("Alfa");
        var second = await AddManagedCondominiumAsync("Beta");
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var author = User("Resident", "requests-author@example.com");
            var firstCategory = new Category(first.Id, "Primeira", null);
            var secondCategory = new Category(second.Id, "Segunda", null);
            db.AddRange(
                author,
                firstCategory,
                secondCategory,
                new DomainRequest(
                    first.Id, author.Id, null, firstCategory.Id,
                    "Solicitação Alfa", "Descrição"),
                new DomainRequest(
                    second.Id, author.Id, null, secondCategory.Id,
                    "Solicitação Beta", "Descrição"));
            await db.SaveChangesAsync();
        }

        var consolidated = await _client.GetFromJsonAsync<RequestsResponse>(
            "/management/requests");
        var specific = await _client.GetFromJsonAsync<RequestsResponse>(
            $"/management/requests?condominiumId={first.Id}");

        Assert.Equal(2, consolidated!.Total);
        Assert.Contains(consolidated.Items, item =>
            item.CondominiumId == first.Id && item.CondominiumName == "Alfa");
        Assert.Contains(consolidated.Items, item =>
            item.CondominiumId == second.Id && item.CondominiumName == "Beta");
        Assert.Equal(first.Id, Assert.Single(specific!.Items).CondominiumId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.GetAsync(
                $"/management/requests?condominiumId={Guid.NewGuid()}"))
            .StatusCode);
    }

    [Fact]
    public async Task Administrator_eligibility_follows_specific_and_consolidated_context()
    {
        var eligible = await AddManagedCondominiumAsync("Com administradora");
        var ineligible = await AddManagedCondominiumAsync("Sem administradora");
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = new ManagementCompany("Administradora", null, null, null, null);
            db.AddRange(company, new CondominiumManagementCompanyLink(eligible.Id, company.Id));
            await db.SaveChangesAsync();
        }

        Assert.True((await GetContextAsync()).HasEligibleManagementCompany);
        var selected = await (await _client.PutAsJsonAsync(
            "/management/context", new { condominiumId = ineligible.Id }))
            .Content.ReadFromJsonAsync<ContextResponse>();
        Assert.False(selected!.HasEligibleManagementCompany);
        var eligibleSelected = await (await _client.PutAsJsonAsync(
            "/management/context", new { condominiumId = eligible.Id }))
            .Content.ReadFromJsonAsync<ContextResponse>();
        Assert.True(eligibleSelected!.HasEligibleManagementCompany);
    }

    [Fact]
    public async Task Submanager_receives_management_scope_only_in_its_condominium()
    {
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var allowed = new Condominium("Permitido", null, null);
            var denied = new Condominium("Negado", null, null);
            var allowedMembership = new CondominiumMembership(_userId, allowed.Id);
            var deniedMembership = new CondominiumMembership(_userId, denied.Id);
            db.AddRange(allowed, denied, allowedMembership, deniedMembership,
                new CondominiumMembershipRole(allowedMembership.Id, CondominiumRole.SubManager),
                new CondominiumMembershipRole(deniedMembership.Id, CondominiumRole.Resident));
            await db.SaveChangesAsync();
        }

        var context = await GetContextAsync();

        var condominium = Assert.Single(context.AvailableCondominiums);
        Assert.Equal("Permitido", condominium.Name);
    }

    private async Task<ContextResponse> GetContextAsync()
        => (await _client.GetFromJsonAsync<ContextResponse>(
            "/management/context"))!;

    private async Task<Condominium> AddManagedCondominiumAsync(string name)
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var condominium = new Condominium(name, null, null);
        var membership = new CondominiumMembership(_userId, condominium.Id);
        db.AddRange(
            condominium,
            membership,
            new CondominiumMembershipRole(
                membership.Id, CondominiumRole.Manager));
        await db.SaveChangesAsync();
        return condominium;
    }

    private static ApplicationUser User(string name, string email)
    {
        var user = new ApplicationUser(name, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        return user;
    }

    private sealed record CondominiumResponse(Guid Id, string Name, bool IsActive);
    private sealed record ContextResponse(
        Guid? ActiveManagementCondominiumId,
        bool UsesConsolidatedManagementScope,
        int CondominiumCount,
        CondominiumResponse? ActiveCondominium,
        IReadOnlyList<CondominiumResponse> AvailableCondominiums,
        bool HasEligibleManagementCompany);
    private sealed record RequestItemResponse(
        Guid Id,
        Guid CondominiumId,
        string CondominiumName);
    private sealed record RequestsResponse(
        int Total,
        IReadOnlyList<RequestItemResponse> Items);
}
