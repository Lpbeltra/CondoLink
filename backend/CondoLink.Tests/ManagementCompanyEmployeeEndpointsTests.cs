using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Domain.Entities;
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

public sealed class ManagementCompanyEmployeeEndpointsTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite(_connection));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
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
        _application.MapCreateManagementCompanyEmployee();
        _application.MapListManagementCompanyEmployees();
        _application.MapUpdateManagementCompanyEmployeeStatus();
        _application.MapDeleteManagementCompanyEmployee();
        _application.MapListManagementCompanies();
        _application.MapGetManagementCompany();
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
    public async Task Can_create_employee_and_receive_temporary_credentials()
    {
        var companyId = await CreateCompanyAsync();

        var response = await CreateEmployeeAsync(
            companyId,
            "  Employee One  ",
            "  EMPLOYEE@EXAMPLE.COM  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var employee = await response.Content
            .ReadFromJsonAsync<CreatedEmployeeResponse>();
        Assert.Equal(companyId, employee!.ManagementCompanyId);
        Assert.Equal("Employee One", employee.FullName);
        Assert.Equal("employee@example.com", employee.Email);
        Assert.Equal("WhatsApp", employee.Contact);
        Assert.Equal("Atendimento", employee.JobTitle);
        Assert.True(employee.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(employee.TemporaryPassword));
    }

    [Fact]
    public async Task Job_title_is_required_and_field_limits_are_validated()
    {
        var companyId = await CreateCompanyAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync(
                $"/overwatch/management-companies/{companyId}/employees",
                new { fullName = "Employee", email = "missing-job@example.com",
                    contact = "WhatsApp", jobTitle = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync(
                $"/overwatch/management-companies/{companyId}/employees",
                new { fullName = "Employee", email = "long-job@example.com",
                    contact = new string('1', 31), jobTitle = new string('A', 101) })).StatusCode);
    }

    [Fact]
    public async Task Can_list_employees_ordered_by_full_name()
    {
        var companyId = await CreateCompanyAsync();
        await CreateEmployeeAsync(companyId, "Zulu", "zulu@example.com");
        await CreateEmployeeAsync(companyId, "Alpha", "alpha@example.com");

        var employees = await _admin.GetFromJsonAsync<List<EmployeeResponse>>(
            $"/overwatch/management-companies/{companyId}/employees");

        Assert.Equal(
            ["Alpha", "Zulu"],
            employees!.Select(employee => employee.FullName));
        Assert.All(employees!, employee => {
            Assert.Equal("WhatsApp", employee.Contact);
            Assert.Equal("Atendimento", employee.JobTitle);
        });
    }

    [Fact]
    public async Task Can_deactivate_employee()
    {
        var employee = await CreatePersistedEmployeeAsync();

        var response = await SetStatusAsync(employee.Id, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated =
            await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task Can_activate_employee()
    {
        var employee = await CreatePersistedEmployeeAsync();
        await SetStatusAsync(employee.Id, false);

        var response = await SetStatusAsync(employee.Id, true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated =
            await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task Delete_removes_only_employee_link()
    {
        var employee = await CreatePersistedEmployeeAsync();

        var response = await _admin.DeleteAsync($"/employees/{employee.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = _application!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await dbContext.ManagementCompanyEmployees
            .AnyAsync(current => current.Id == employee.Id));
        Assert.True(await dbContext.Users
            .AnyAsync(user => user.Id == employee.UserId));
    }

    [Fact]
    public async Task Existing_email_returns_409()
    {
        var companyId = await CreateCompanyAsync();
        await CreateUserAsync("Existing", "existing@example.com");

        var response = await CreateEmployeeAsync(
            companyId,
            "Another",
            "existing@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task User_already_linked_returns_409()
    {
        var firstCompanyId = await CreateCompanyAsync("First");
        var secondCompanyId = await CreateCompanyAsync("Second");
        var user = await CreateUserAsync("Employee", "linked@example.com");
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.ManagementCompanyEmployees.Add(
                new ManagementCompanyEmployee(firstCompanyId, user.Id));
            await dbContext.SaveChangesAsync();
        }

        var response = await CreateEmployeeAsync(
            secondCompanyId,
            "Employee",
            "linked@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "already belongs",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_management_company_returns_404()
    {
        var response = await CreateEmployeeAsync(
            Guid.NewGuid(),
            "Employee",
            "missing-company@example.com");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Common_user_receives_403()
    {
        var companyId = await CreateCompanyAsync();
        using var client = _application!.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        var response = await client.PostAsJsonAsync(
            $"/overwatch/management-companies/{companyId}/employees",
            new { fullName = "Employee", email = "employee@example.com",
                contact = (string?)null, jobTitle = "Atendimento" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Management_company_without_employees_returns_zero_count()
    {
        var companyId = await CreateCompanyAsync();

        var companies = await _admin.GetFromJsonAsync<List<CompanyCountResponse>>(
            "/overwatch/management-companies") ?? [];
        var company = await _admin.GetFromJsonAsync<CompanyCountResponse>(
            $"/overwatch/management-companies/{companyId}");

        Assert.Equal(0, companies!.Single().EmployeeCount);
        Assert.Equal(0, company!.EmployeeCount);
    }

    [Fact]
    public async Task Employee_count_tracks_all_existing_links_per_company()
    {
        var firstCompanyId = await CreateCompanyAsync("Alpha");
        var secondCompanyId = await CreateCompanyAsync("Beta");
        var firstEmployee = await CreateEmployeeAndReadAsync(
            firstCompanyId, "First", "first@example.com");
        var inactiveEmployee = await CreateEmployeeAndReadAsync(
            firstCompanyId, "Inactive", "inactive@example.com");
        await CreateEmployeeAndReadAsync(
            secondCompanyId, "Other", "other@example.com");

        var companies = await _admin.GetFromJsonAsync<List<CompanyCountResponse>>(
            "/overwatch/management-companies") ?? [];
        var firstCompany = await _admin.GetFromJsonAsync<CompanyCountResponse>(
            $"/overwatch/management-companies/{firstCompanyId}");

        Assert.Equal(["Alpha", "Beta"], companies!.Select(item => item.Name));
        Assert.Equal(2, companies.Single(
            item => item.Id == firstCompanyId).EmployeeCount);
        Assert.Equal(1, companies.Single(
            item => item.Id == secondCompanyId).EmployeeCount);
        Assert.Equal(2, firstCompany!.EmployeeCount);

        Assert.Equal(
            HttpStatusCode.OK,
            (await SetStatusAsync(inactiveEmployee.Id, false)).StatusCode);
        firstCompany = await _admin.GetFromJsonAsync<CompanyCountResponse>(
            $"/overwatch/management-companies/{firstCompanyId}");
        Assert.Equal(2, firstCompany!.EmployeeCount);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _admin.DeleteAsync($"/employees/{firstEmployee.Id}")).StatusCode);
        companies = await _admin.GetFromJsonAsync<List<CompanyCountResponse>>(
            "/overwatch/management-companies") ?? [];

        Assert.Equal(1, companies!.Single(
            item => item.Id == firstCompanyId).EmployeeCount);
        Assert.Equal(1, companies.Single(
            item => item.Id == secondCompanyId).EmployeeCount);
    }

    private async Task<Guid> CreateCompanyAsync(string name = "Company")
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = new ManagementCompany(name, null, null, null, null);
        dbContext.ManagementCompanies.Add(company);
        await dbContext.SaveChangesAsync();
        return company.Id;
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string fullName,
        string email)
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(fullName, email, null);
        var result = await userManager.CreateAsync(user, "Temporary1!");
        Assert.True(result.Succeeded);
        return user;
    }

    private async Task<CreatedEmployeeResponse> CreatePersistedEmployeeAsync()
    {
        var companyId = await CreateCompanyAsync();
        var response = await CreateEmployeeAsync(
            companyId,
            "Employee",
            $"{Guid.NewGuid()}@example.com");
        return (await response.Content
            .ReadFromJsonAsync<CreatedEmployeeResponse>())!;
    }

    private async Task<CreatedEmployeeResponse> CreateEmployeeAndReadAsync(
        Guid companyId,
        string fullName,
        string email)
    {
        var response = await CreateEmployeeAsync(companyId, fullName, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<CreatedEmployeeResponse>())!;
    }

    private Task<HttpResponseMessage> CreateEmployeeAsync(
        Guid companyId,
        string fullName,
        string email) =>
        _admin.PostAsJsonAsync(
            $"/overwatch/management-companies/{companyId}/employees",
            new { fullName, email, contact = "  WhatsApp  ", jobTitle = "  Atendimento  " });

    private Task<HttpResponseMessage> SetStatusAsync(
        Guid employeeId,
        bool isActive) =>
        _admin.PatchAsJsonAsync(
            $"/employees/{employeeId}/status",
            new { isActive });

    private sealed record CreatedEmployeeResponse(
        Guid Id,
        Guid ManagementCompanyId,
        Guid UserId,
        string FullName,
        string Email,
        string? Contact,
        string JobTitle,
        bool IsActive,
        string TemporaryPassword);

    private sealed record EmployeeResponse(
        Guid Id,
        string FullName,
        string? Contact,
        string JobTitle);

    private sealed record StatusResponse(bool IsActive);
    private sealed record CompanyCountResponse(
        Guid Id,
        string Name,
        int CondominiumCount,
        int EmployeeCount);
}
