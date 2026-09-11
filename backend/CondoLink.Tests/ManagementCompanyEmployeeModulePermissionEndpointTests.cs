using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using Microsoft.AspNetCore.Builder;

namespace CondoLink.Tests;

public sealed class ManagementCompanyEmployeeModulePermissionEndpointTests
{
    [Fact]
    public async Task Only_platform_admin_can_grant_and_revoke_employee_management_permission()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapManagementCompanyEmployeeModulePermissionEndpoints());
        var company = new ManagementCompany("Administradora", null, null, null, null);
        var user = CoreTestSeed.User("Departamento Pessoal", "dp@test.local");
        ManagementCompanyEmployee employee = null!;
        await host.WithDbAsync(async db =>
        {
            db.AddRange(company, user);
            await db.SaveChangesAsync();
            employee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento Pessoal");
            db.Add(employee);
            await db.SaveChangesAsync();
        });

        var denied = host.ClientFor(Guid.NewGuid());
        denied.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"/overwatch/management-companies/employees/{employee.Id}/module-permissions")).StatusCode);

        var admin = host.ClientFor(Guid.NewGuid());
        admin.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);

        var initial = await admin.GetFromJsonAsync<PermissionRow[]>($"/overwatch/management-companies/employees/{employee.Id}/module-permissions");
        var row = Assert.Single(initial!);
        Assert.Equal("EmployeeManagement", row.Module);
        Assert.False(row.Allowed);

        var grant = await admin.PutAsJsonAsync($"/overwatch/management-companies/employees/{employee.Id}/module-permissions",
            new ManagementCompanyEmployeeModulePermissionEndpoints.Request([
                new ManagementCompanyEmployeeModulePermissionEndpoints.PermissionItem("EmployeeManagement", true)
            ]));
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var afterGrant = await admin.GetFromJsonAsync<PermissionRow[]>($"/overwatch/management-companies/employees/{employee.Id}/module-permissions");
        Assert.True(Assert.Single(afterGrant!).Allowed);

        var revoke = await admin.PutAsJsonAsync($"/overwatch/management-companies/employees/{employee.Id}/module-permissions",
            new ManagementCompanyEmployeeModulePermissionEndpoints.Request([
                new ManagementCompanyEmployeeModulePermissionEndpoints.PermissionItem("EmployeeManagement", false)
            ]));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var afterRevoke = await admin.GetFromJsonAsync<PermissionRow[]>($"/overwatch/management-companies/employees/{employee.Id}/module-permissions");
        Assert.False(Assert.Single(afterRevoke!).Allowed);
    }

    private sealed record PermissionRow(string Module, bool Allowed);
}
