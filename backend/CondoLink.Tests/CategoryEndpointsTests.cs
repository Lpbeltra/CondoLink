using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Categories;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// Endpoint-level guarantees for condominium request categories: creating and
/// editing them is manager-only, names are unique per condominium after
/// normalisation, and listing them is open to every active member but scoped to
/// the active categories of that one condominium.
/// </summary>
public sealed class CategoryEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;
    private Guid _outsiderId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapCreateCategory();
            application.MapManageCategory();
            application.MapListCondominiumCategories();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");
            var outsider = CoreTestSeed.User("Estranho", "estranho@example.com");

            db.AddRange(
                condominium, otherCondominium, manager, otherManager,
                resident, outsider);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _otherCondominiumId = otherCondominium.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
            _outsiderId = outsider.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_create_a_category_and_receives_201()
    {
        var response = await CreateAsync(
            _managerId, "  Manutenção  ", "  Reparos gerais  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateCategory.Response>();
        Assert.Equal("Manutenção", body!.Name);
        Assert.Equal("Reparos gerais", body.Description);
        Assert.True(body.IsActive);
        Assert.Equal(_condominiumId, body.CondominiumId);
    }

    [Fact]
    public async Task Resident_cannot_create_a_category()
    {
        var response = await CreateAsync(_residentId, "Manutenção");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CategoryCountAsync());
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_create_a_category()
    {
        var response = await CreateAsync(_otherManagerId, "Manutenção");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CategoryCountAsync());
    }

    [Fact]
    public async Task Anonymous_caller_cannot_create_a_category()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories",
            new { name = "Manutenção" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_normalized_name_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "Manutenção")).StatusCode);

        var response = await CreateAsync(_managerId, "  manutenção  ");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CategoryCountAsync());
    }

    [Fact]
    public async Task The_same_name_may_be_reused_in_a_different_condominium()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "Manutenção")).StatusCode);

        var response = await _host.ClientFor(_otherManagerId).PostAsJsonAsync(
            $"/condominiums/{_otherCondominiumId}/categories",
            new { name = "Manutenção" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_name_returns_400(string? name)
    {
        var response = await CreateAsync(_managerId, name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Name_longer_than_100_characters_returns_400()
    {
        var response = await CreateAsync(_managerId, new string('a', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Description_longer_than_500_characters_returns_400()
    {
        var response = await CreateAsync(
            _managerId, "Manutenção", new string('a', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_category_in_a_missing_condominium_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{Guid.NewGuid()}/categories",
            new { name = "Manutenção" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_condominium_cannot_receive_new_categories()
    {
        await SetCondominiumActiveAsync(false);

        var response = await CreateAsync(_managerId, "Manutenção");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_create_a_category()
    {
        await _host.WithDbAsync(async db =>
        {
            var manager = await db.Set<ApplicationUser>()
                .SingleAsync(user => user.Id == _managerId);
            manager.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await CreateAsync(_managerId, "Manutenção");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_rename_a_category()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}",
            new { name = "  Portaria  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ManageCategory.Response>();
        Assert.Equal("Portaria", body!.Name);
        Assert.Equal(0, body.RequestCount);
        Assert.Equal("PORTARIA", await _host.WithDbAsync(db => db.Categories
            .AsNoTracking().Where(item => item.Id == categoryId)
            .Select(item => item.NormalizedName).SingleAsync()));
    }

    [Fact]
    public async Task Renaming_a_category_onto_an_existing_normalized_name_returns_409()
    {
        var firstId = await CreatedCategoryIdAsync("Manutenção");
        await CreatedCategoryIdAsync("Portaria");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories/{firstId}",
            new { name = "portaria" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Manutenção", await _host.WithDbAsync(db => db.Categories
            .AsNoTracking().Where(item => item.Id == firstId)
            .Select(item => item.Name).SingleAsync()));
    }

    [Fact]
    public async Task Renaming_a_category_to_a_different_casing_of_its_own_name_is_accepted()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}",
            new { name = "MANUTENÇÃO" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_rename_or_delete_a_category()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");
        var resident = _host.ClientFor(_residentId);

        var update = await resident.PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}",
            new { name = "Outro" });
        var delete = await resident.DeleteAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(1, await CategoryCountAsync());
    }

    [Fact]
    public async Task Renaming_a_category_of_another_condominium_returns_404()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");

        var response = await _host.ClientFor(_otherManagerId).PutAsJsonAsync(
            $"/condominiums/{_otherCondominiumId}/categories/{categoryId}",
            new { name = "Roubada" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_delete_an_unused_category()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CategoryCountAsync());
    }

    [Fact]
    public async Task Deleting_a_category_already_used_by_a_request_returns_409()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");
        await _host.WithDbAsync(async db =>
        {
            db.Requests.Add(new DomainRequest(
                _condominiumId, _residentId, null, categoryId,
                "Vazamento", "Descrição"));
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CategoryCountAsync());
    }

    [Fact]
    public async Task Deleting_a_missing_category_returns_404()
    {
        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/categories/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Active_member_lists_the_condominium_categories_ordered_by_name()
    {
        await CreatedCategoryIdAsync("Portaria");
        await CreatedCategoryIdAsync("Manutenção");
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_otherCondominiumId, "Alheia", null));
            await db.SaveChangesAsync();
        });

        var categories = await _host.ClientFor(_residentId)
            .GetFromJsonAsync<List<ListCondominiumCategories.Response>>(
                $"/condominiums/{_condominiumId}/categories") ?? [];

        Assert.Equal(["Manutenção", "Portaria"],
            categories.Select(category => category.Name));
    }

    [Fact]
    public async Task Category_list_reports_the_number_of_requests_per_category()
    {
        var categoryId = await CreatedCategoryIdAsync("Manutenção");
        await _host.WithDbAsync(async db =>
        {
            db.Requests.AddRange(
                new DomainRequest(_condominiumId, _residentId, null,
                    categoryId, "Primeira", "Descrição"),
                new DomainRequest(_condominiumId, _residentId, null,
                    categoryId, "Segunda", "Descrição"));
            await db.SaveChangesAsync();
        });

        var categories = await _host.ClientFor(_residentId)
            .GetFromJsonAsync<List<ListCondominiumCategories.Response>>(
                $"/condominiums/{_condominiumId}/categories") ?? [];

        Assert.Equal(2, Assert.Single(categories).RequestCount);
    }

    [Fact]
    public async Task Non_member_cannot_list_condominium_categories()
    {
        var response = await _host.ClientFor(_outsiderId)
            .GetAsync($"/condominiums/{_condominiumId}/categories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_categories_of_an_inactive_condominium_returns_409()
    {
        await CreatedCategoryIdAsync("Manutenção");
        await SetCondominiumActiveAsync(false);

        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/condominiums/{_condominiumId}/categories");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Listing_categories_of_a_missing_condominium_returns_404()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/condominiums/{Guid.NewGuid()}/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> CreateAsync(
        Guid callerId,
        string? name,
        string? description = null) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/categories",
            new { name, description });

    private async Task<Guid> CreatedCategoryIdAsync(string name)
    {
        var response = await CreateAsync(_managerId, name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateCategory.Response>();
        return body!.Id;
    }

    private Task SetCondominiumActiveAsync(bool isActive) =>
        _host.WithDbAsync(async db =>
        {
            var condominium = await db.Condominiums
                .SingleAsync(item => item.Id == _condominiumId);
            condominium.SetActiveStatus(isActive);
            await db.SaveChangesAsync();
        });

    private Task<int> CategoryCountAsync() =>
        _host.WithDbAsync(db => db.Categories
            .CountAsync(category => category.CondominiumId == _condominiumId));
}
