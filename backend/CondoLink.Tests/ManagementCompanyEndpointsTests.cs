using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class ManagementCompanyEndpointsTests : IAsyncLifetime
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
        _application.MapListManagementCompanies();
        _application.MapGetManagementCompany();
        _application.MapCreateManagementCompany();
        _application.MapUpdateManagementCompany();
        _application.MapUpdateManagementCompanyStatus();
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
    public async Task PlatformAdmin_can_create_and_valid_creation_returns_201()
    {
        var response = await CreateAsync(
            "  Alpha Admin  ", "  Alpha Legal  ", "  123  ",
            "  ADMIN@EXAMPLE.COM  ", "  555  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        Assert.NotNull(body);
        Assert.Equal("Alpha Admin", body.Name);
        Assert.Equal("Rua Teste, 1", body.Address);
        Assert.Equal("São Paulo", body.City);
        Assert.Equal("SP", body.State);
        Assert.Equal("admin@example.com", body.Email);
        Assert.Equal("555", body.PhoneNumber);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Common_user_receives_403()
    {
        using var client = _application!.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        var response = await client.PostAsJsonAsync(
            "/overwatch/management-companies",
            Request("Admin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Empty_name_returns_400()
    {
        var response = await CreateAsync("   ");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_document_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync("First", document: "DOC-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await CreateAsync("Second", document: " DOC-1 ")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_returns_409_case_insensitively()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync("First", email: "admin@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await CreateAsync("Second", email: " ADMIN@EXAMPLE.COM ")).StatusCode);
    }

    [Fact]
    public async Task Invalid_cnpj_or_state_returns_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync("/overwatch/management-companies",
                new { name = "Invalid CNPJ", cnpj = "123", address = "Rua A",
                    city = "São Paulo", state = "SP" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.PostAsJsonAsync("/overwatch/management-companies",
                new { name = "Invalid state", cnpj = "04.252.011/0001-10",
                    address = "Rua A", city = "São Paulo", state = "XX" })).StatusCode);
    }

    [Fact]
    public async Task List_is_ordered_by_name()
    {
        await CreateAsync("Zulu");
        await CreateAsync("Alpha");
        await CreateAsync("Middle");

        var companies = await _admin.GetFromJsonAsync<List<CompanyResponse>>(
            "/overwatch/management-companies");

        Assert.Equal(["Alpha", "Middle", "Zulu"],
            companies!.Select(company => company.Name));
    }

    [Fact]
    public async Task Update_works()
    {
        var created = await CreateAsync("Before");
        var company = await created.Content.ReadFromJsonAsync<CompanyResponse>();

        var response = await _admin.PutAsJsonAsync(
            $"/overwatch/management-companies/{company!.Id}",
            Request("  After  ", "  Legal  ", " DOC ", "NEW@EXAMPLE.COM", " 123 "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        Assert.Equal("After", updated!.Name);
        Assert.Equal("new@example.com", updated.Email);
    }

    [Fact]
    public async Task Status_change_works()
    {
        var created = await CreateAsync("Admin");
        var company = await created.Content.ReadFromJsonAsync<CompanyResponse>();

        var response = await _admin.PatchAsJsonAsync(
            $"/overwatch/management-companies/{company!.Id}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.False(updated!.IsActive);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Missing_entity_returns_404(string method)
    {
        var id = Guid.NewGuid();
        HttpResponseMessage response = method switch
        {
            "GET" => await _admin.GetAsync(
                $"/overwatch/management-companies/{id}"),
            "PUT" => await _admin.PutAsJsonAsync(
                $"/overwatch/management-companies/{id}", Request("Missing")),
            _ => await _admin.PatchAsJsonAsync(
                $"/overwatch/management-companies/{id}/status",
                new { isActive = false })
        };

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Can_link_management_company_to_condominium()
    {
        var (condominiumId, firstCompanyId, _) = await SeedRelationshipAsync();

        var response = await SetCompanyAsync(condominiumId, firstCompanyId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var condominium =
            await response.Content.ReadFromJsonAsync<CondominiumResponse>();
        Assert.Equal(firstCompanyId, condominium!.ManagementCompanyId);
    }

    [Fact]
    public async Task Can_change_management_company()
    {
        var (condominiumId, firstCompanyId, secondCompanyId) =
            await SeedRelationshipAsync();
        await SetCompanyAsync(condominiumId, firstCompanyId);

        var response = await SetCompanyAsync(condominiumId, secondCompanyId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var condominium =
            await response.Content.ReadFromJsonAsync<CondominiumResponse>();
        Assert.Equal(secondCompanyId, condominium!.ManagementCompanyId);
    }

    [Fact]
    public async Task Can_remove_management_company_link()
    {
        var (condominiumId, firstCompanyId, _) = await SeedRelationshipAsync();
        await SetCompanyAsync(condominiumId, firstCompanyId);

        var response = await SetCompanyAsync(condominiumId, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var condominium =
            await response.Content.ReadFromJsonAsync<CondominiumResponse>();
        Assert.Null(condominium!.ManagementCompanyId);
    }

    [Fact]
    public async Task Missing_management_company_returns_404()
    {
        var (condominiumId, _, _) = await SeedRelationshipAsync();

        var response = await SetCompanyAsync(condominiumId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_condominium_returns_404_when_setting_company()
    {
        var (_, companyId, _) = await SeedRelationshipAsync();

        var response = await SetCompanyAsync(Guid.NewGuid(), companyId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Common_user_cannot_set_management_company()
    {
        var (condominiumId, companyId, _) = await SeedRelationshipAsync();
        using var client = _application!.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        var response = await client.PutAsJsonAsync(
            $"/overwatch/condominiums/{condominiumId}/management-company",
            new { managementCompanyId = companyId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_management_company_returns_correct_condominium_count()
    {
        var (condominiumId, companyId, _) = await SeedRelationshipAsync();
        await SetCompanyAsync(condominiumId, companyId);

        var company = await _admin.GetFromJsonAsync<CompanyResponse>(
            $"/overwatch/management-companies/{companyId}");

        Assert.Equal(1, company!.CondominiumCount);
    }

    [Fact]
    public async Task List_returns_condominium_count_without_loading_collection()
    {
        var (condominiumId, companyId, secondCompanyId) =
            await SeedRelationshipAsync();
        await SetCompanyAsync(condominiumId, companyId);

        var companies =
            await _admin.GetFromJsonAsync<List<CompanyResponse>>(
                "/overwatch/management-companies") ?? [];

        Assert.Equal(1, companies.Single(
            company => company.Id == companyId).CondominiumCount);
        Assert.Equal(0, companies.Single(
            company => company.Id == secondCompanyId).CondominiumCount);
        Assert.All(companies, company => Assert.Equal(0, company.EmployeeCount));
    }

    private Task<HttpResponseMessage> SetCompanyAsync(
        Guid condominiumId,
        Guid? managementCompanyId) =>
        _admin.PutAsJsonAsync(
            $"/overwatch/condominiums/{condominiumId}/management-company",
            new { managementCompanyId });

    private async Task<(Guid CondominiumId, Guid FirstCompanyId,
        Guid SecondCompanyId)> SeedRelationshipAsync()
    {
        await using var scope = _application!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var condominium = new Condominium("Condominium", null, null);
        var firstCompany =
            new ManagementCompany("First Company", null, null, null, null);
        var secondCompany =
            new ManagementCompany("Second Company", null, null, null, null);
        dbContext.AddRange(condominium, firstCompany, secondCompany);
        await dbContext.SaveChangesAsync();
        return (condominium.Id, firstCompany.Id, secondCompany.Id);
    }

    private Task<HttpResponseMessage> CreateAsync(
        string name,
        string? legalName = null,
        string? document = null,
        string? email = null,
        string? phoneNumber = null) =>
        _admin.PostAsJsonAsync(
            "/overwatch/management-companies",
            Request(name, legalName, document, email, phoneNumber));

    private static object Request(
        string name,
        string? legalName = null,
        string? document = null,
        string? email = null,
        string? phoneNumber = null) =>
        new
        {
            name,
            cnpj = TestCnpj(document ?? name),
            address = "Rua Teste, 1",
            city = "São Paulo",
            state = "SP",
            email,
            phoneNumber
        };

    private static string TestCnpj(string seed)
    {
        var root = Math.Abs(seed.Trim().ToUpperInvariant().GetHashCode())
            .ToString().PadLeft(8, '0')[..8] + "0001";
        static int Digit(string value, int[] weights)
        {
            var remainder = value.Select((current, index) =>
                (current - '0') * weights[index]).Sum() % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }
        var first = Digit(root, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = Digit(root + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return root + first + second;
    }

    private sealed record CompanyResponse(
        Guid Id,
        string Name,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        int CondominiumCount,
        int EmployeeCount);

    private sealed record StatusResponse(bool IsActive);
    private sealed record CondominiumResponse(
        Guid Id,
        Guid? ManagementCompanyId);
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string TestScheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var value)
            && Guid.TryParse(value.ToString(), out var parsedUserId)
                ? parsedUserId
                : Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            TestScheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity), TestScheme)));
    }
}
