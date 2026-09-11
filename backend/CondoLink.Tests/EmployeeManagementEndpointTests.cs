using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class EmployeeManagementEndpointTests
{
    private static async Task<CoreEndpointTestHost> StartHostAsync(RecordingLoggerProvider? logs = null) =>
        await CoreEndpointTestHost.StartAsync(
            app => app.MapEmployeeManagementEndpoints(),
            builder =>
            {
                builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>();
                builder.Services.AddScoped<EmployeeManagementAccessService>();
                if (logs is not null) builder.Logging.AddProvider(logs);
            });

    private static async Task EnableModuleAsync(CoreEndpointTestHost host, Guid condominiumId, bool enabled, bool managementCompanyAccessEnabled) =>
        await host.WithDbAsync(async db =>
        {
            var row = await db.CondominiumModules.SingleAsync(x => x.CondominiumId == condominiumId && x.Module == CondominiumModuleType.EmployeeManagement);
            row.Set(enabled, managementCompanyAccessEnabled, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

    // Condominium sem Manager ativo + administradora vinculada + EmployeeManagement
    // habilitado + delegação habilitada + funcionário com permissão = acesso permitido.
    [Fact]
    public async Task Management_company_employee_can_operate_condominium_with_no_manager()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Sem síndico", null, null);
        var company = new ManagementCompany("Administradora X", null, null, null, null);
        var operatorUser = CoreTestSeed.User("Operador DP", "dp@test.local");
        ManagementCompanyEmployee employee = null!;

        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, company, operatorUser);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            condominium.SetManagementCompany(company.Id);
            employee = new ManagementCompanyEmployee(company.Id, operatorUser.Id, "Departamento Pessoal");
            db.Add(employee);
            db.Add(new ManagementCompanyEmployeeModulePermission(employee.Id, CondominiumModuleType.EmployeeManagement, operatorUser.Id));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, true);

        // No CondominiumMembership/Manager row exists for this condominium at all.
        var hasAnyMembership = await host.WithDbAsync(db => Task.FromResult(db.CondominiumMemberships.Any(x => x.CondominiumId == condominium.Id)));
        Assert.False(hasAnyMembership);

        var client = host.ClientFor(operatorUser.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);

        var create = await client.PostAsJsonAsync($"/condominiums/{condominium.Id}/employees",
            new EmployeeRequest("João Porteiro", "Porteiro", "11987654321", null, "MAT-001", null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<EmployeeResponse>();

        var update = await client.PutAsJsonAsync($"/condominiums/{condominium.Id}/employees/{created!.Id}",
            new EmployeeRequest("João Porteiro Silva", "Porteiro", "11987654321", null, "MAT-001", null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task Manager_can_operate_when_module_enabled_and_is_denied_when_disabled()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo A", null, null);
        var manager = CoreTestSeed.User("Síndico", "manager@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        var client = host.ClientFor(manager.Id);

        // Disabled by default (EmployeeManagement defaults off).
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);

        await EnableModuleAsync(host, condominium.Id, true, false);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task SubManager_is_denied_by_default_and_allowed_once_explicitly_granted()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo B", null, null);
        var subManager = CoreTestSeed.User("Subsíndico", "submanager@test.local");
        Guid membershipId = Guid.Empty;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, subManager);
            var membership = CoreTestSeed.AddMember(db, subManager.Id, condominium.Id, CondominiumRole.SubManager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            membershipId = membership.Id;
            // Simulate an existing SubManager whose permissions were already
            // backfilled for the other (pre-existing) configurable modules.
            await SubManagerAccess.EnsureDefaultsAsync(db, membershipId, subManager.Id, default);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, false);
        var client = host.ClientFor(subManager.Id);

        // Denied by default even though other modules were auto-granted.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
        var permissions = await host.WithDbAsync(db => Task.FromResult(db.SubManagerModulePermissions
            .Where(x => x.CondominiumMembershipId == membershipId).ToArray()));
        Assert.False(permissions.Single(x => x.Module == SubManagerModule.EmployeeManagement).IsAllowed);
        Assert.True(permissions.Where(x => x.Module != SubManagerModule.EmployeeManagement).All(x => x.IsAllowed));

        await host.WithDbAsync(async db =>
        {
            var permission = await db.SubManagerModulePermissions.SingleAsync(
                x => x.CondominiumMembershipId == membershipId && x.Module == SubManagerModule.EmployeeManagement);
            permission.SetAllowed(true, subManager.Id);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    // A SubManager membership that predates the permission system (or whose backfill
    // never ran) has zero SubManagerModulePermissions rows. The legacy fallback treats
    // that as full access for pre-existing modules, but EmployeeManagement must never
    // be granted by absence of rows — only by an explicit allow row.
    [Fact]
    public async Task SubManager_with_no_permission_rows_at_all_is_denied_EmployeeManagement()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo K", null, null);
        var subManager = CoreTestSeed.User("Subsíndico Legado", "legacy-submanager@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, subManager);
            CoreTestSeed.AddMember(db, subManager.Id, condominium.Id, CondominiumRole.SubManager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, false);

        var hasAnyPermissionRow = await host.WithDbAsync(db => Task.FromResult(db.SubManagerModulePermissions.Any()));
        Assert.False(hasAnyPermissionRow);

        var client = host.ClientFor(subManager.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Delegation_disabled_denies_management_company_employee()
    {
        await using var host = await StartHostAsync();
        var (condominium, company, user, employee) = await SeedDelegatedScenarioAsync(host, moduleEnabled: true, delegationEnabled: false, grantPermission: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(user.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Delegation_enabled_but_employee_without_permission_is_denied()
    {
        await using var host = await StartHostAsync();
        var (condominium, company, user, employee) = await SeedDelegatedScenarioAsync(host, moduleEnabled: true, delegationEnabled: true, grantPermission: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(user.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Former_management_company_loses_access_after_condominium_switches_administrator()
    {
        await using var host = await StartHostAsync();
        var (condominium, company, user, employee) = await SeedDelegatedScenarioAsync(host, moduleEnabled: true, delegationEnabled: true, grantPermission: true);
        Assert.Equal(HttpStatusCode.OK, (await host.ClientFor(user.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);

        var otherCompany = new ManagementCompany("Administradora Y", null, null, null, null);
        await host.WithDbAsync(async db =>
        {
            db.Add(otherCompany);
            await db.SaveChangesAsync();
            var tracked = await db.Condominiums.SingleAsync(x => x.Id == condominium.Id);
            tracked.SetManagementCompany(otherCompany.Id);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(user.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Employee_of_a_different_management_company_is_denied()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo D", null, null);
        var companyA = new ManagementCompany("Administradora A", null, null, null, null);
        var companyB = new ManagementCompany("Administradora B", null, null, null, null);
        var outsider = CoreTestSeed.User("Funcionário de outra administradora", "outsider@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, companyA, companyB, outsider);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            condominium.SetManagementCompany(companyA.Id);
            var employee = new ManagementCompanyEmployee(companyB.Id, outsider.Id, "Departamento Pessoal");
            db.Add(employee);
            db.Add(new ManagementCompanyEmployeeModulePermission(employee.Id, CondominiumModuleType.EmployeeManagement, outsider.Id));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, true);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(outsider.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Resident_is_denied()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo E", null, null);
        var resident = CoreTestSeed.User("Morador", "resident@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, resident);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, true);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(resident.Id).GetAsync($"/condominiums/{condominium.Id}/employees")).StatusCode);
    }

    [Fact]
    public async Task Employee_from_another_condominium_is_not_found_without_leaking_existence()
    {
        await using var host = await StartHostAsync();
        var condominiumA = new Condominium("Condo F", null, null);
        var condominiumB = new Condominium("Condo G", null, null);
        var manager = CoreTestSeed.User("Síndico Multi", "multi@test.local");
        Employee employeeInA = null!;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominiumA, condominiumB, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominiumA.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, manager.Id, condominiumB.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominiumA.Id, DateTime.UtcNow);
            CondominiumModuleService.AddDefaults(db, condominiumB.Id, DateTime.UtcNow);
            employeeInA = new Employee(condominiumA.Id, "Zelador A", null, null, null, null, null);
            db.Add(employeeInA);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominiumA.Id, true, false);
        await EnableModuleAsync(host, condominiumB.Id, true, false);

        var client = host.ClientFor(manager.Id);

        var get = await client.GetAsync($"/condominiums/{condominiumB.Id}/employees/{employeeInA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var put = await client.PutAsJsonAsync($"/condominiums/{condominiumB.Id}/employees/{employeeInA.Id}",
            new EmployeeRequest("Zelador A Renomeado", null, null, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        var patch = await client.PatchAsJsonAsync($"/condominiums/{condominiumB.Id}/employees/{employeeInA.Id}/status",
            new EmployeeStatusRequest(false));
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);

        // The employee itself must be untouched by the failed cross-condominium attempts.
        var untouched = await host.WithDbAsync(db => db.Employees.AsNoTracking().SingleAsync(x => x.Id == employeeInA.Id));
        Assert.Equal("Zelador A", untouched.FullName);
        Assert.True(untouched.IsActive);
    }

    [Fact]
    public async Task Registration_number_is_unique_per_condominium_and_phone_is_validated()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo H", null, null);
        var manager = CoreTestSeed.User("Síndico H", "sindicoh@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, false);
        var client = host.ClientFor(manager.Id);

        var first = await client.PostAsJsonAsync($"/condominiums/{condominium.Id}/employees",
            new EmployeeRequest("Primeiro", null, null, null, "mat-01", null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync($"/condominiums/{condominium.Id}/employees",
            new EmployeeRequest("Segundo", null, null, null, "MAT-01", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalidPhone = await client.PostAsJsonAsync($"/condominiums/{condominium.Id}/employees",
            new EmployeeRequest("Terceiro", null, "not-a-phone", null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPhone.StatusCode);
    }

    [Fact]
    public async Task List_supports_search_and_active_status_filter()
    {
        await using var host = await StartHostAsync();
        var condominium = new Condominium("Condo I", null, null);
        var manager = CoreTestSeed.User("Síndico I", "sindicoi@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            var active = new Employee(condominium.Id, "Ana Ativa", "Zeladora", null, null, null, null);
            var inactive = new Employee(condominium.Id, "Bruno Inativo", "Porteiro", null, null, null, null);
            inactive.Deactivate();
            db.AddRange(active, inactive);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, false);
        var client = host.ClientFor(manager.Id);

        var all = await client.GetFromJsonAsync<EmployeeResponse[]>($"/condominiums/{condominium.Id}/employees");
        Assert.Equal(2, all!.Length);

        var activeOnly = await client.GetFromJsonAsync<EmployeeResponse[]>($"/condominiums/{condominium.Id}/employees?status=active");
        Assert.Single(activeOnly!);
        Assert.Equal("Ana Ativa", activeOnly![0].FullName);

        var searched = await client.GetFromJsonAsync<EmployeeResponse[]>($"/condominiums/{condominium.Id}/employees?search=zeladora");
        Assert.Single(searched!);
    }

    [Fact]
    public async Task Status_change_activates_and_deactivates_and_writes_audit_log()
    {
        var logs = new RecordingLoggerProvider();
        await using var host = await StartHostAsync(logs);
        var condominium = new Condominium("Condo J", null, null);
        var manager = CoreTestSeed.User("Síndico J", "sindicoj@test.local");
        Employee employee = null!;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            employee = new Employee(condominium.Id, "Carla", null, null, null, null, null);
            db.Add(employee);
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, true, false);
        var client = host.ClientFor(manager.Id);

        var deactivate = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/{employee.Id}/status", new EmployeeStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<EmployeeResponse>();
        Assert.False(deactivated!.IsActive);

        var reactivate = await client.PatchAsJsonAsync($"/condominiums/{condominium.Id}/employees/{employee.Id}/status", new EmployeeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var auditActions = logs.Entries.Where(x => x.Category == "EmployeeAudit")
            .Select(x => x.Values["Action"]?.ToString()).ToArray();
        Assert.Contains("Deactivated", auditActions);
        Assert.Contains("Activated", auditActions);
        Assert.All(logs.Entries.Where(x => x.Category == "EmployeeAudit"), entry =>
        {
            Assert.False(entry.Values.ContainsKey("PhoneNumber"));
            Assert.False(entry.Values.ContainsKey("Email"));
        });
    }

    [Fact]
    public async Task Administrator_condominium_listing_only_returns_delegated_and_permitted_condominiums()
    {
        await using var host = await StartHostAsync();
        var delegated = new Condominium("Delegado", null, null);
        var notDelegated = new Condominium("Não delegado", null, null);
        var company = new ManagementCompany("Administradora Z", null, null, null, null);
        var user = CoreTestSeed.User("Operador Z", "operadorz@test.local");
        await host.WithDbAsync(async db =>
        {
            db.AddRange(delegated, notDelegated, company, user);
            CondominiumModuleService.AddDefaults(db, delegated.Id, DateTime.UtcNow);
            CondominiumModuleService.AddDefaults(db, notDelegated.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            delegated.SetManagementCompany(company.Id);
            notDelegated.SetManagementCompany(company.Id);
            var employee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento Pessoal");
            db.Add(employee);
            db.Add(new ManagementCompanyEmployeeModulePermission(employee.Id, CondominiumModuleType.EmployeeManagement, user.Id));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, delegated.Id, true, true);
        await EnableModuleAsync(host, notDelegated.Id, true, false);

        var options = await host.ClientFor(user.Id)
            .GetFromJsonAsync<ListAdministratorEmployeeManagementCondominiums.CondominiumOption[]>("/administrator/employee-management/condominiums");
        Assert.Single(options!);
        Assert.Equal(delegated.Id, options![0].CondominiumId);
    }

    private static async Task<(Condominium Condominium, ManagementCompany Company, ApplicationUser User, ManagementCompanyEmployee Employee)>
        SeedDelegatedScenarioAsync(CoreEndpointTestHost host, bool moduleEnabled, bool delegationEnabled, bool grantPermission)
    {
        var condominium = new Condominium("Condo Delegado", null, null);
        var company = new ManagementCompany("Administradora", null, null, null, null);
        var user = CoreTestSeed.User("Operador", $"operador-{Guid.NewGuid():N}@test.local");
        ManagementCompanyEmployee employee = null!;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(condominium, company, user);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            condominium.SetManagementCompany(company.Id);
            employee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento Pessoal");
            db.Add(employee);
            if (grantPermission)
                db.Add(new ManagementCompanyEmployeeModulePermission(employee.Id, CondominiumModuleType.EmployeeManagement, user.Id));
            await db.SaveChangesAsync();
        });
        await EnableModuleAsync(host, condominium.Id, moduleEnabled, delegationEnabled);
        return (condominium, company, user, employee);
    }

    internal sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<Entry> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);
        public void Dispose() { }
    }

    internal sealed class RecordingLogger(string category, List<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            entries.Add(new Entry(category, values.ToDictionary(x => x.Key, x => x.Value)));
        }
    }

    internal sealed record Entry(string Category, IReadOnlyDictionary<string, object?> Values);
}
