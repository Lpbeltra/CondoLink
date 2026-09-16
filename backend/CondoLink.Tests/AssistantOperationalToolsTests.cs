using System.Text.Json;
using CondoLink.Api.Features.Agenda;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class AssistantOperationalToolsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private Guid condominiumId;
    private Guid managerId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var manager = new ApplicationUser("Manager", $"manager-{Guid.NewGuid():N}@test.local", null);
        managerId = manager.Id;
        manager.NormalizedUserName = manager.UserName!.ToUpperInvariant();
        manager.NormalizedEmail = manager.Email!.ToUpperInvariant();
        db.Add(manager);
        var condominium = new Condominium("Condominium", null, null);
        condominiumId = condominium.Id;
        db.Add(condominium);
        var membership = new CondominiumMembership(managerId, condominiumId);
        db.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Manager));
        db.Add(membership);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Tool_rejects_other_condominium_without_leaking_data()
    {
        var tools = CreateTools();

        var result = await tools.ExecuteAsync("get_management_company", "{}", managerId, Guid.NewGuid(), default);

        Assert.Contains("autorizada", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Personal_provider_is_visible_only_to_its_owner()
    {
        var otherUserId = Guid.NewGuid();
        var other = new ApplicationUser("Other", $"other-{Guid.NewGuid():N}@test.local", null);
        other.NormalizedUserName = other.UserName!.ToUpperInvariant();
        other.NormalizedEmail = other.Email!.ToUpperInvariant();
        db.Add(other);
        otherUserId = other.Id;
        var own = new ServiceProvider("Eletricista próprio", null, "elétrica", null, "111", null, null, null, null, DateTime.UtcNow);
        var otherProvider = new ServiceProvider("Eletricista de outro usuário", null, "elétrica", null, "222", null, null, null, null, DateTime.UtcNow);
        db.AddRange(own, otherProvider);
        db.Add(new ServiceProviderUserLink(own.Id, managerId));
        db.Add(new ServiceProviderUserLink(otherProvider.Id, otherUserId));
        await db.SaveChangesAsync();

        var result = await CreateTools().ExecuteAsync("search_service_providers", "{\"query\":\"Eletricista\"}", managerId, condominiumId, default);
        using var json = JsonDocument.Parse(result.Json);
        var names = json.RootElement.GetProperty("rows").EnumerateArray()
            .Select(x => x.GetProperty("Name").GetString()).ToArray();

        Assert.Contains("Eletricista próprio", names);
        Assert.DoesNotContain("Eletricista de outro usuário", names);
    }

    [Theory]
    [InlineData("search_residents", "{}")]
    [InlineData("get_unit_residents", "{\"unit\":\"999\"}")]
    [InlineData("search_requests", "{}")]
    [InlineData("get_request", "{\"id\":\"999\"}")]
    [InlineData("list_reminders", "{\"view\":\"today\"}")]
    [InlineData("search_service_providers", "{}")]
    [InlineData("get_management_company", "{}")]
    [InlineData("search_management_company_requests", "{}")]
    public async Task Every_operational_domain_rejects_another_condominium_server_side(string tool, string arguments)
    {
        var result = await CreateTools().ExecuteAsync(tool, arguments, managerId, Guid.NewGuid(), default);
        Assert.Contains("autorizada", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.References);
    }

    [Fact]
    public async Task Attendance_tool_rejects_submanager_without_module_permission()
    {
        var user = new ApplicationUser("No Attendance", $"no-attendance-{Guid.NewGuid():N}@test.local", null);
        user.NormalizedUserName = user.UserName!.ToUpperInvariant();
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        var membership = new CondominiumMembership(user.Id, condominiumId);
        db.AddRange(user, membership, new CondominiumMembershipRole(membership.Id, CondominiumRole.SubManager));
        var denied = new SubManagerModulePermission(membership.Id, SubManagerModule.Attendance, managerId);
        denied.SetAllowed(false, managerId);
        db.SubManagerModulePermissions.Add(denied);
        await db.SaveChangesAsync();

        var result = await CreateTools().ExecuteAsync("search_requests", "{}", user.Id, condominiumId, default);
        Assert.Contains("autorizada", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.References);
    }

    [Fact]
    public async Task Authorized_domains_return_safe_empty_results_for_missing_entities()
    {
        var tools = CreateTools();
        foreach (var (name, args) in new[]
        {
            ("search_residents", "{\"query\":\"nome-inexistente\"}"),
            ("search_requests", "{\"query\":\"protocolo-inexistente\"}"),
            ("list_reminders", "{\"view\":\"today\"}"),
            ("search_service_providers", "{\"query\":\"especialidade-inexistente\"}"),
            ("search_management_company_requests", "{\"query\":\"solicitacao-inexistente\"}")
        })
        {
            var result = await tools.ExecuteAsync(name, args, managerId, condominiumId, default);
            Assert.DoesNotContain("Consulta nÃ£o autorizada", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.References);
        }
    }

    private AssistantOperationalTools CreateTools() => new(db,
        NullLogger<AssistantOperationalTools>.Instance,
        Options.Create(new AgendaOptions { OperationalTimeZone = "America/Sao_Paulo" }));
}
