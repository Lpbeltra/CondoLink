using System.Net;
using System.Net.Http.Json;
using System.Text;
using CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ManagementCompanyRequestCategoryEndpointsTests
    : IAsyncLifetime
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
        _application.MapListManagementCompanyRequestCategories();
        _application.MapGetManagementCompanyRequestCategory();
        _application.MapCreateManagementCompanyRequestCategory();
        _application.MapUpdateManagementCompanyRequestCategory();
        _application.MapUpdateManagementCompanyRequestCategoryStatus();
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
    public async Task PlatformAdmin_can_create_valid_category_and_receives_201()
    {
        var companyId = await CreateCompanyAsync();

        var response = await CreateCategoryAsync(
            companyId,
            "  Supplier payment  ",
            "  Invoices and payments.  ",
            ManagementCompanyRequestFormType.SupplierPayment);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content
            .ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal("Supplier payment", category!.Name);
        Assert.Equal("Invoices and payments.", category.Description);
        Assert.Equal(
            ManagementCompanyRequestFormType.SupplierPayment,
            category.FormType);
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task Common_user_receives_403()
    {
        var companyId = await CreateCompanyAsync();
        using var client = _application!.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");

        var response = await client.PostAsJsonAsync(
            CategoriesUrl(companyId),
            Request("Category"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_management_company_returns_404()
    {
        var response = await CreateCategoryAsync(
            Guid.NewGuid(),
            "Category");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Empty_name_returns_400()
    {
        var companyId = await CreateCompanyAsync();

        var response = await CreateCategoryAsync(companyId, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_form_type_returns_400()
    {
        var companyId = await CreateCompanyAsync();
        using var content = new StringContent(
            """{"name":"Category","description":null,"formType":"Invalid"}""",
            Encoding.UTF8,
            "application/json");

        var response = await _admin.PostAsync(
            CategoriesUrl(companyId),
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_in_same_company_returns_409()
    {
        var companyId = await CreateCompanyAsync();
        await CreateCategoryAsync(companyId, "Payments");

        var response = await CreateCategoryAsync(companyId, "Payments");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Case_only_duplicate_returns_409()
    {
        var companyId = await CreateCompanyAsync();
        await CreateCategoryAsync(companyId, "Payments");

        var response = await CreateCategoryAsync(companyId, "pAyMeNtS");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Same_name_is_allowed_in_different_companies()
    {
        var firstCompanyId = await CreateCompanyAsync("First");
        var secondCompanyId = await CreateCompanyAsync("Second");

        var first = await CreateCategoryAsync(firstCompanyId, "Payments");
        var second = await CreateCategoryAsync(secondCompanyId, "payments");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task List_is_ordered_by_name()
    {
        var companyId = await CreateCompanyAsync();
        await CreateCategoryAsync(companyId, "Zulu");
        await CreateCategoryAsync(companyId, "Alpha");
        await CreateCategoryAsync(companyId, "Middle");

        var categories = await _admin
            .GetFromJsonAsync<List<CategoryResponse>>(
                CategoriesUrl(companyId));

        Assert.Equal(
            ["Alpha", "Middle", "Zulu"],
            categories!.Select(category => category.Name));
    }

    [Fact]
    public async Task Get_by_id_works()
    {
        var companyId = await CreateCompanyAsync();
        var category = await CreatePersistedCategoryAsync(
            companyId,
            "Payments");

        var result = await _admin.GetFromJsonAsync<CategoryResponse>(
            CategoryUrl(companyId, category.Id));

        Assert.Equal(category.Id, result!.Id);
        Assert.Equal(companyId, result.ManagementCompanyId);
    }

    [Fact]
    public async Task Update_works()
    {
        var companyId = await CreateCompanyAsync();
        var category = await CreatePersistedCategoryAsync(
            companyId,
            "Before");

        var response = await _admin.PutAsJsonAsync(
            CategoryUrl(companyId, category.Id),
            Request(
                "  After  ",
                "  Updated  ",
                ManagementCompanyRequestFormType.Reimbursement));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content
            .ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal("After", updated!.Name);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(
            ManagementCompanyRequestFormType.Reimbursement,
            updated.FormType);
    }

    [Fact]
    public async Task Status_change_works()
    {
        var companyId = await CreateCompanyAsync();
        var category = await CreatePersistedCategoryAsync(
            companyId,
            "Category");

        var response = await _admin.PatchAsJsonAsync(
            $"{CategoryUrl(companyId, category.Id)}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content
            .ReadFromJsonAsync<CategoryResponse>();
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task Category_from_another_company_returns_404()
    {
        var firstCompanyId = await CreateCompanyAsync("First");
        var secondCompanyId = await CreateCompanyAsync("Second");
        var category = await CreatePersistedCategoryAsync(
            firstCompanyId,
            "Category");

        var response = await _admin.GetAsync(
            CategoryUrl(secondCompanyId, category.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_category_returns_404()
    {
        var companyId = await CreateCompanyAsync();

        var response = await _admin.GetAsync(
            CategoryUrl(companyId, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private async Task<CategoryResponse> CreatePersistedCategoryAsync(
        Guid companyId,
        string name)
    {
        var response = await CreateCategoryAsync(companyId, name);
        return (await response.Content
            .ReadFromJsonAsync<CategoryResponse>())!;
    }

    private Task<HttpResponseMessage> CreateCategoryAsync(
        Guid companyId,
        string name,
        string? description = null,
        ManagementCompanyRequestFormType formType =
            ManagementCompanyRequestFormType.Generic) =>
        _admin.PostAsJsonAsync(
            CategoriesUrl(companyId),
            Request(name, description, formType));

    private static object Request(
        string name,
        string? description = null,
        ManagementCompanyRequestFormType formType =
            ManagementCompanyRequestFormType.Generic) =>
        new { name, description, formType };

    private static string CategoriesUrl(Guid companyId) =>
        $"/overwatch/management-companies/{companyId}/request-categories";

    private static string CategoryUrl(Guid companyId, Guid categoryId) =>
        $"{CategoriesUrl(companyId)}/{categoryId}";

    private sealed record CategoryResponse(
        Guid Id,
        Guid ManagementCompanyId,
        string Name,
        string? Description,
        ManagementCompanyRequestFormType FormType,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
