using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class ManagementCompanyEmployeeModulePermissionEndpointTests
{
    [Fact]
    public async Task Grant_is_scoped_and_requires_current_link_entitlement_delegation_and_active_employee()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapManagementCompanyEmployeeModulePermissionEndpoints());
        var company = new ManagementCompany("Administradora", null, null, null, null);
        var otherCompany = new ManagementCompany("Outra", null, null, null, null);
        var a = new Condominium("A", null, null);
        var b = new Condominium("B", null, null);
        var c = new Condominium("C", null, null);
        var user = CoreTestSeed.User("Departamento Pessoal", "dp@test.local");
        ManagementCompanyEmployee employee = null!;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(company, otherCompany, a, b, c, user);
            foreach (var condominium in new[] { a, b, c }) CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
            a.SetManagementCompany(company.Id); b.SetManagementCompany(company.Id); c.SetManagementCompany(otherCompany.Id);
            employee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento Pessoal");
            db.Add(employee);
            await db.SaveChangesAsync();
        });
        var path = $"/overwatch/management-companies/employees/{employee.Id}/module-permissions";
        var denied = host.ClientFor(Guid.NewGuid());
        denied.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);

        var admin = host.ClientFor(Guid.NewGuid());
        admin.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await admin.PutAsJsonAsync(path, new { permissions = new[] { new { module = "EmployeeManagement", allowed = true } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"{path}/{a.Id}", new { allowed = true })).StatusCode);
        await host.WithDbAsync(async db =>
        {
            var module = await db.CondominiumModules.SingleAsync(x => x.CondominiumId == a.Id && x.Module == CondominiumModuleType.EmployeeManagement);
            module.Set(true, false, DateTime.UtcNow); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"{path}/{a.Id}", new { allowed = true })).StatusCode);
        await host.WithDbAsync(async db =>
        {
            var module = await db.CondominiumModules.SingleAsync(x => x.CondominiumId == a.Id && x.Module == CondominiumModuleType.EmployeeManagement);
            module.Set(true, true, DateTime.UtcNow); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"{path}/{c.Id}", new { allowed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"{path}/{a.Id}", new { allowed = true })).StatusCode);
        var rows = await admin.GetFromJsonAsync<PermissionRow[]>(path);
        Assert.Equal(2, rows!.Length);
        Assert.True(rows.Single(x => x.CondominiumId == a.Id).Allowed);
        Assert.False(rows.Single(x => x.CondominiumId == b.Id).Eligible);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"{path}/{a.Id}", new { allowed = false })).StatusCode);
        rows = await admin.GetFromJsonAsync<PermissionRow[]>(path);
        Assert.False(rows!.Single(x => x.CondominiumId == a.Id).Allowed);

        await host.WithDbAsync(async db =>
        {
            var access = await db.ManagementCompanyEmployees.SingleAsync(x => x.Id == employee.Id);
            access.Deactivate(); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"{path}/{a.Id}", new { allowed = true })).StatusCode);
    }

    private sealed record PermissionRow(Guid CondominiumId, string CondominiumName, bool Eligible, bool Allowed);
}
