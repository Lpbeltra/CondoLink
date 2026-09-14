using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.EmployeeManagement;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>
/// Contract tests for the administrator-scoped Employee CRUD/status endpoints —
/// in particular the "toggling status must never blank out the rest of the
/// card" regression (a partial response used to replace the full cached
/// employee on the frontend) and the CPF masking/reveal boundary between the
/// list, the bulk reveal and the per-employee detail/edit endpoints.
/// </summary>
public sealed class AdministratorEmployeeEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _operatorId;
    private Guid _companyId;
    private Guid _condominiumId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapAdministratorEmployeeEndpoints(),
            builder => builder.Services.AddScoped<EmployeeManagementAccessService>());

        await _host.WithDbAsync(async db =>
        {
            var company = new ManagementCompany("Administradora de Funcionários", null, null, null, null);
            var user = CoreTestSeed.User("Operador", $"employee-admin-{Guid.NewGuid():N}@test.local");
            var companyEmployee = new ManagementCompanyEmployee(company.Id, user.Id, "Departamento pessoal");
            var condominium = new Condominium("Monticello", null, "11222333000181", null, null, null, false, false, null);
            condominium.SetManagementCompany(company.Id);
            db.AddRange(company, user, companyEmployee, condominium,
                new CondominiumManagementCompanyLink(condominium.Id, company.Id),
                new ManagementCompanyModule(company.Id, ManagementCompanyModuleType.EmployeeManagement, true, DateTime.UtcNow),
                new ManagementCompanyEmployeeModuleGrant(companyEmployee.Id, ManagementCompanyModuleType.EmployeeManagement, user.Id, DateTime.UtcNow));
            await db.SaveChangesAsync();
            _operatorId = user.Id;
            _companyId = company.Id;
            _condominiumId = condominium.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Toggling_status_returns_the_full_employee_not_only_the_changed_fields()
    {
        using var client = _host.ClientFor(_operatorId);
        var created = await client.PostAsJsonAsync("/administrator/employees", new
        {
            condominiumId = _condominiumId, fullName = "Ana Empregada", cpf = "529.982.247-25",
            jobTitle = "Zeladora", phoneNumber = "11999990000", email = "ana@example.com", registrationNumber = "MAT-1",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var employeeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.PatchAsJsonAsync($"/administrator/employees/{employeeId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Regression guard: this used to return only { id, isActive, updatedAt },
        // which the frontend used to overwrite its cached item — blanking the
        // card's name/job title/tags the instant status was toggled.
        Assert.False(body.GetProperty("isActive").GetBoolean());
        Assert.Equal("Ana Empregada", body.GetProperty("fullName").GetString());
        Assert.Equal("Zeladora", body.GetProperty("jobTitle").GetString());
        Assert.Equal("11999990000", body.GetProperty("phoneNumber").GetString());
        Assert.Equal("MAT-1", body.GetProperty("registrationNumber").GetString());
        Assert.Equal(_condominiumId, body.GetProperty("condominiumId").GetGuid());
        Assert.Equal("***.***.***-25", body.GetProperty("cpf").GetString());
    }

    [Fact]
    public async Task List_masks_cpf_by_default_and_reveals_it_only_when_asked()
    {
        using var client = _host.ClientFor(_operatorId);
        await client.PostAsJsonAsync("/administrator/employees", new
        {
            condominiumId = _condominiumId, fullName = "Bruno Empregado", cpf = "529.982.247-25",
            jobTitle = "Porteiro", phoneNumber = "11999990001", email = (string?)null, registrationNumber = (string?)null,
        });

        var masked = await client.GetFromJsonAsync<JsonElement[]>("/administrator/employees");
        Assert.Equal("***.***.***-25", masked![0].GetProperty("cpf").GetString());

        var revealed = await client.GetFromJsonAsync<JsonElement[]>("/administrator/employees?revealCpf=true");
        Assert.Equal("529.982.247-25", revealed![0].GetProperty("cpf").GetString());
    }

    [Fact]
    public async Task Detail_endpoint_returns_full_cpf_for_the_edit_form()
    {
        using var client = _host.ClientFor(_operatorId);
        var created = await client.PostAsJsonAsync("/administrator/employees", new
        {
            condominiumId = _condominiumId, fullName = "Carla Empregada", cpf = "529.982.247-25",
            jobTitle = null as string, phoneNumber = null as string, email = null as string, registrationNumber = null as string,
        });
        var employeeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/administrator/employees/{employeeId}");
        Assert.Equal("529.982.247-25", detail.GetProperty("cpf").GetString());
    }

    [Fact]
    public async Task Detail_endpoint_is_scoped_to_the_administrators_own_management_company()
    {
        var (otherOperatorId, otherEmployeeId) = await _host.WithDbAsync(async db =>
        {
            var otherCompany = new ManagementCompany("Outra Administradora", null, null, null, null);
            var otherUser = CoreTestSeed.User("Outro Operador", $"other-{Guid.NewGuid():N}@test.local");
            var otherCompanyEmployee = new ManagementCompanyEmployee(otherCompany.Id, otherUser.Id, "Departamento pessoal");
            var otherCondominium = new Condominium("Outro Condomínio", null, null, null, null, null, false, false, null);
            otherCondominium.SetManagementCompany(otherCompany.Id);
            var otherEmployee = new Employee(otherCondominium.Id, "Funcionário de Outra Empresa", null, null, null, null, null);
            db.AddRange(otherCompany, otherUser, otherCompanyEmployee, otherCondominium, otherEmployee,
                new CondominiumManagementCompanyLink(otherCondominium.Id, otherCompany.Id),
                new ManagementCompanyModule(otherCompany.Id, ManagementCompanyModuleType.EmployeeManagement, true, DateTime.UtcNow),
                new ManagementCompanyEmployeeModuleGrant(otherCompanyEmployee.Id, ManagementCompanyModuleType.EmployeeManagement, otherUser.Id, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return (otherUser.Id, otherEmployee.Id);
        });

        using var client = _host.ClientFor(_operatorId);
        var response = await client.GetAsync($"/administrator/employees/{otherEmployeeId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _ = otherOperatorId;
    }

    [Fact]
    public async Task Editing_an_employee_never_changes_isactive_even_if_the_request_carries_it()
    {
        using var client = _host.ClientFor(_operatorId);
        var created = await client.PostAsJsonAsync("/administrator/employees", new
        {
            condominiumId = _condominiumId, fullName = "Diego Empregado", cpf = (string?)null,
            jobTitle = (string?)null, phoneNumber = (string?)null, email = (string?)null, registrationNumber = (string?)null,
        });
        var employeeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Status can only change via PATCH .../status — the update endpoint's
        // request contract has no IsActive field at all, so even a client that
        // tries to smuggle one in has nothing to bind to.
        var updated = await client.PutAsJsonAsync($"/administrator/employees/{employeeId}", new
        {
            condominiumId = _condominiumId, fullName = "Diego Empregado Atualizado", cpf = (string?)null,
            jobTitle = (string?)null, phoneNumber = (string?)null, email = (string?)null, registrationNumber = (string?)null,
            isActive = false,
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }
}
