using System.Net.Http.Json;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// <see cref="ListCondominiumRequests"/> now paginates instead of loading every
/// request in memory. These tests pin the default (unpaginated-looking)
/// behaviour that existing callers rely on, and the explicit pagination and
/// search parameters the frontend can opt into later.
/// </summary>
public sealed class ListCondominiumRequestsEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId;
    private Guid _managerId;
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapListCondominiumRequests();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);

            db.AddRange(condominium, manager, category);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();

            for (var index = 0; index < 5; index++)
            {
                db.Requests.Add(new DomainRequest(
                    condominium.Id, manager.Id, null, category.Id,
                    $"Solicitação {index}", "Descrição"));
            }

            db.Requests.Add(new DomainRequest(
                condominium.Id, manager.Id, null, category.Id,
                "Vazamento na garagem", "Descrição"));

            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _managerId = manager.Id;
            _categoryId = category.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Default_call_without_paging_parameters_returns_every_matching_request()
    {
        var response = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<Response>("/management/requests");

        Assert.Equal(6, response!.Total);
        Assert.Equal(6, response.Items.Count);
        Assert.Equal(1, response.Page);
        Assert.Equal(200, response.PageSize);
    }

    [Fact]
    public async Task Explicit_page_size_limits_and_skips_rows_while_total_reflects_the_full_count()
    {
        var firstPage = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<Response>("/management/requests?page=1&pageSize=2");
        var secondPage = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<Response>("/management/requests?page=2&pageSize=2");

        Assert.Equal(6, firstPage!.Total);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(6, secondPage!.Total);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Empty(firstPage.Items.Select(item => item.Id)
            .Intersect(secondPage.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task Search_filters_by_title_and_still_reports_the_filtered_total()
    {
        var response = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<Response>("/management/requests?search=vazamento");

        Assert.Equal(1, response!.Total);
        Assert.Equal("Vazamento na garagem", Assert.Single(response.Items).Title);
    }

    [Fact]
    public async Task PageSize_is_clamped_to_the_maximum_and_page_to_at_least_one()
    {
        var response = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<Response>("/management/requests?page=0&pageSize=99999");

        Assert.Equal(1, response!.Page);
        Assert.Equal(500, response.PageSize);
    }

    private sealed record ItemResponse(Guid Id, string Title);

    private sealed record Response(
        int Total, int Page, int PageSize, IReadOnlyList<ItemResponse> Items);
}
