using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class OverwatchCondominiumEndpointsTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
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
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(
                DependencyInjection.PlatformAdminPolicy,
                policy => policy.RequireRole(
                    DependencyInjection.PlatformAdminRole)));

        _application = builder.Build();
        _application.UseAuthentication();
        _application.UseAuthorization();
        _application.MapListOverwatchCondominiums();
        _application.MapGetOverwatchCondominium();
        _application.MapListOverwatchCondominiumManagers();
        _application.MapSetCondominiumManagementCompany();
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
    public async Task List_projects_company_name_and_isolated_active_manager_count()
    {
        var (firstId, secondId, companyName) = await SeedRelationshipsAsync();

        var items = await _admin.GetFromJsonAsync<List<Response>>(
            "/overwatch/condominiums") ?? [];

        var first = items.Single(item => item.Id == firstId);
        var second = items.Single(item => item.Id == secondId);
        Assert.Equal(companyName, first.ManagementCompanyName);
        Assert.Equal(1, first.ManagerCount);
        Assert.Null(second.ManagementCompanyName);
        Assert.Equal(1, second.ManagerCount);
    }

    [Fact]
    public async Task Details_and_manager_list_return_real_relationship_data()
    {
        var (firstId, _, companyName) = await SeedRelationshipsAsync();

        var details = await _admin.GetFromJsonAsync<Response>(
            $"/overwatch/condominiums/{firstId}");
        var managers = await _admin.GetFromJsonAsync<List<ManagerResponse>>(
            $"/overwatch/condominiums/{firstId}/managers");

        Assert.Equal(companyName, details!.ManagementCompanyName);
        Assert.Equal(1, details.ManagerCount);
        var manager = Assert.Single(managers!);
        Assert.Equal("First Manager", manager.FullName);
    }

    [Fact]
    public async Task Missing_details_and_managers_return_404()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _admin.GetAsync($"/overwatch/condominiums/{id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _admin.GetAsync(
                $"/overwatch/condominiums/{id}/managers")).StatusCode);
    }

    [Fact]
    public async Task Invalid_company_returns_404_and_null_unlinks()
    {
        var (condominiumId, _, _) = await SeedRelationshipsAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _admin.PutAsJsonAsync(
                $"/overwatch/condominiums/{condominiumId}/management-company",
                new { managementCompanyId = Guid.NewGuid() })).StatusCode);

        var response = await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{condominiumId}/management-company",
            new { managementCompanyId = (Guid?)null });
        var unlinked = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(unlinked!.ManagementCompanyId);
    }

    [Fact]
    public async Task Common_user_cannot_access_list()
    {
        using var client = _application!.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/overwatch/condominiums")).StatusCode);
    }

    private async Task<(Guid FirstId, Guid SecondId, string CompanyName)>
        SeedRelationshipsAsync()
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = new ManagementCompany(
            "Management Company", null, null, null, null);
        var first = new Condominium("Alpha", null, null);
        var second = new Condominium("Beta", null, null);
        first.SetManagementCompany(company.Id);
        var firstUser = new ApplicationUser(
            "First Manager", "first-manager@example.com", null);
        var secondUser = new ApplicationUser(
            "Second Manager", "second-manager@example.com", null);
        firstUser.NormalizedUserName = "FIRST-MANAGER@EXAMPLE.COM";
        firstUser.NormalizedEmail = "FIRST-MANAGER@EXAMPLE.COM";
        secondUser.NormalizedUserName = "SECOND-MANAGER@EXAMPLE.COM";
        secondUser.NormalizedEmail = "SECOND-MANAGER@EXAMPLE.COM";
        var firstMembership = new CondominiumMembership(firstUser.Id, first.Id);
        var secondMembership = new CondominiumMembership(secondUser.Id, second.Id);
        var firstRole = new CondominiumMembershipRole(
            firstMembership.Id, CondominiumRole.Manager);
        var secondRole = new CondominiumMembershipRole(
            secondMembership.Id, CondominiumRole.Manager);

        db.AddRange(
            company, first, second, firstUser, secondUser,
            firstMembership, secondMembership, firstRole, secondRole);
        await db.SaveChangesAsync();
        return (first.Id, second.Id, company.Name);
    }

    private sealed record Response(
        Guid Id,
        string? ManagementCompanyName,
        int ManagerCount);

    private sealed record ManagerResponse(string FullName);
    private sealed record LinkResponse(Guid? ManagementCompanyId);
}
