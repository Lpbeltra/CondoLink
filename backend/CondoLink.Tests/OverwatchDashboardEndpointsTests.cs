using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch;
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
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class OverwatchDashboardEndpointsTests : IAsyncLifetime
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
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        builder.Services.AddIdentityCore<ApplicationUser>()
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
        builder.Services.AddAuthorization(options => options.AddPolicy(
            DependencyInjection.PlatformAdminPolicy,
            policy => policy.RequireRole(DependencyInjection.PlatformAdminRole)));

        _application = builder.Build();
        _application.UseAuthentication();
        _application.UseAuthorization();
        _application.MapGetOverwatchDashboard();
        await _application.StartAsync();
        _admin = _application.GetTestClient();
        _admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");

        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var managerRole = new IdentityRole<Guid>("Manager") { NormalizedName = "MANAGER" };
        var manager = new ApplicationUser("Manager", "manager@example.com", null);
        manager.NormalizedUserName = "MANAGER@EXAMPLE.COM";
        manager.NormalizedEmail = "MANAGER@EXAMPLE.COM";
        var firstCompany = new ManagementCompany("First", null, null, null, null);
        var secondCompany = new ManagementCompany("Second", null, null, null, null);
        var firstCondominium = new Condominium("First condominium", null, null);
        var secondCondominium = new Condominium("Second condominium", null, null);
        var firstEmployeeUser = new ApplicationUser(
            "First employee", "first-employee@example.com", null);
        firstEmployeeUser.NormalizedUserName = "FIRST-EMPLOYEE@EXAMPLE.COM";
        firstEmployeeUser.NormalizedEmail = "FIRST-EMPLOYEE@EXAMPLE.COM";
        var secondEmployeeUser = new ApplicationUser(
            "Second employee", "second-employee@example.com", null);
        secondEmployeeUser.NormalizedUserName = "SECOND-EMPLOYEE@EXAMPLE.COM";
        secondEmployeeUser.NormalizedEmail = "SECOND-EMPLOYEE@EXAMPLE.COM";
        var activeEmployee = new ManagementCompanyEmployee(
            firstCompany.Id, firstEmployeeUser.Id);
        var inactiveEmployee = new ManagementCompanyEmployee(
            secondCompany.Id, secondEmployeeUser.Id);
        inactiveEmployee.Deactivate();
        db.AddRange(managerRole, manager, firstCompany, secondCompany,
            firstCondominium, secondCondominium, firstEmployeeUser,
            secondEmployeeUser, activeEmployee, inactiveEmployee);
        db.UserRoles.Add(new IdentityUserRole<Guid> {
            UserId = manager.Id, RoleId = managerRole.Id
        });
        await db.SaveChangesAsync();
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
    public async Task Returns_all_real_counts_including_inactive_employees()
    {
        var result = await _admin.GetFromJsonAsync<OverwatchDashboardResponse>(
            "/overwatch/dashboard");
        Assert.Equal(2, result!.ManagementCompanyCount);
        Assert.Equal(2, result.CondominiumCount);
        Assert.Equal(1, result.ManagerCount);
        Assert.Equal(2, result.EmployeeCount);
    }

    [Fact]
    public async Task Requires_platform_admin()
    {
        using var common = _application!.GetTestClient();
        common.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await common.GetAsync("/overwatch/dashboard")).StatusCode);
    }
}
