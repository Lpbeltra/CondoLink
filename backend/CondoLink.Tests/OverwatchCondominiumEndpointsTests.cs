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
        _application.MapCreateOverwatchCondominium();
        _application.MapUpdateOverwatchCondominium();
        _application.MapUpdateOverwatchCondominiumStatus();
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
    public async Task Inactivating_condominium_reconciles_manager_context()
    {
        var (condominiumId, _, _) = await SeedRelationshipsAsync();
        Guid managerId;
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            managerId = await db.CondominiumMemberships
                .Where(item => item.CondominiumId == condominiumId)
                .Select(item => item.UserId)
                .SingleAsync();
            var manager = await db.Users.SingleAsync(item => item.Id == managerId);
            manager.SetActiveManagementCondominium(condominiumId);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK,
            (await _admin.PatchAsJsonAsync(
                $"/overwatch/condominiums/{condominiumId}/status",
                new { isActive = false })).StatusCode);

        await using var verifyScope = _application!.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await verify.Users.SingleAsync(
            item => item.Id == managerId)).ActiveManagementCondominiumId);
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

    [Fact]
    public async Task Creates_and_updates_complete_registration()
    {
        var response = await _admin.PostAsJsonAsync(
            "/overwatch/condominiums",
            Registration("Registered", "04.252.011/0001-10", true, true, "Central"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>();

        var update = await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{created!.Id}",
            Registration("Updated", "04.252.011/0001-10", false, true, "Ignored"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var details = await _admin.GetFromJsonAsync<RegistrationResponse>(
            $"/overwatch/condominiums/{created.Id}");
        Assert.False(details!.HasDoorman);
        Assert.False(details.IsRemoteDoorman);
        Assert.Null(details.DoormanContact);
        Assert.Equal("04252011000110", details.Cnpj);
        Assert.True(details.WhatsAppUpdatesEnabled);
    }

    [Fact]
    public async Task WhatsApp_updates_default_to_true_and_explicit_values_can_be_toggled()
    {
        var defaultResponse = await _admin.PostAsJsonAsync(
            "/overwatch/condominiums",
            Registration("Default WhatsApp", "04.252.011/0001-10", false, false, null));
        var defaultCreated = await defaultResponse.Content.ReadFromJsonAsync<CreatedResponse>();
        Assert.True((await _admin.GetFromJsonAsync<RegistrationResponse>(
            $"/overwatch/condominiums/{defaultCreated!.Id}"))!.WhatsAppUpdatesEnabled);
        var disableDefault = new {
            name = "Default WhatsApp", email = (string?)null,
            cnpj = "04.252.011/0001-10", address = "Rua A", city = "São Paulo",
            state = "SP", hasDoorman = false, isRemoteDoorman = false,
            doormanContact = (string?)null, whatsAppUpdatesEnabled = false
        };
        Assert.Equal(HttpStatusCode.OK, (await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{defaultCreated.Id}", disableDefault)).StatusCode);
        Assert.False((await _admin.GetFromJsonAsync<RegistrationResponse>(
            $"/overwatch/condominiums/{defaultCreated.Id}"))!.WhatsAppUpdatesEnabled);

        var explicitFalse = new {
            name = "Disabled WhatsApp", email = (string?)null,
            cnpj = "45.723.174/0001-10", address = "Rua B", city = "Curitiba",
            state = "PR", hasDoorman = false, isRemoteDoorman = false,
            doormanContact = (string?)null, whatsAppUpdatesEnabled = false
        };
        var falseResponse = await _admin.PostAsJsonAsync(
            "/overwatch/condominiums", explicitFalse);
        var falseCreated = await falseResponse.Content.ReadFromJsonAsync<CreatedResponse>();
        var falseDetails = await _admin.GetFromJsonAsync<RegistrationResponse>(
            $"/overwatch/condominiums/{falseCreated!.Id}");
        Assert.False(falseDetails!.WhatsAppUpdatesEnabled);

        var enabledUpdate = new {
            explicitFalse.name, explicitFalse.email, explicitFalse.cnpj,
            explicitFalse.address, explicitFalse.city, explicitFalse.state,
            explicitFalse.hasDoorman, explicitFalse.isRemoteDoorman,
            explicitFalse.doormanContact, whatsAppUpdatesEnabled = true
        };
        Assert.Equal(HttpStatusCode.OK, (await _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{falseCreated.Id}", enabledUpdate)).StatusCode);
        Assert.True((await _admin.GetFromJsonAsync<RegistrationResponse>(
            $"/overwatch/condominiums/{falseCreated.Id}"))!.WhatsAppUpdatesEnabled);
    }

    [Fact]
    public void Domain_and_ef_model_default_whatsapp_updates_to_true()
    {
        Assert.True(new Condominium("Default", null, null).WhatsAppUpdatesEnabled);
        using var scope = _application!.Services.CreateScope();
        var property = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model
            .FindEntityType(typeof(Condominium))!
            .FindProperty(nameof(Condominium.WhatsAppUpdatesEnabled))!;
        Assert.Equal(true, property.GetDefaultValue());
    }

    [Fact]
    public async Task Rejects_invalid_and_duplicate_cnpj_or_state()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync("/overwatch/condominiums",
                Registration("Invalid", "123", false, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync("/overwatch/condominiums",
                new {
                    name = "State", email = (string?)null, cnpj = "04.252.011/0001-10",
                    address = "Rua A", city = "São Paulo", state = "XX",
                    hasDoorman = false, isRemoteDoorman = false,
                    doormanContact = (string?)null
                })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await _admin.PostAsJsonAsync("/overwatch/condominiums",
                Registration("First CNPJ", "04.252.011/0001-10", false, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _admin.PostAsJsonAsync("/overwatch/condominiums",
                Registration("Second CNPJ", "04252011000110", false, false, null))).StatusCode);
    }

    private static object Registration(
        string name, string cnpj, bool hasDoorman, bool isRemote, string? contact) =>
        new {
            name, email = (string?)null, cnpj, address = "Rua A",
            city = "São Paulo", state = "SP", hasDoorman,
            isRemoteDoorman = isRemote, doormanContact = contact
        };

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
    private sealed record CreatedResponse(Guid Id);
    private sealed record RegistrationResponse(
        string? Cnpj, bool HasDoorman, bool IsRemoteDoorman, string? DoormanContact,
        bool WhatsAppUpdatesEnabled);
}
