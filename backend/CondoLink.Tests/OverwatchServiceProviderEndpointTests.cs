using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.ServiceProviders;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class OverwatchServiceProviderEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _adminId;
    private Guid _providerId;
    private Guid _requestId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app => app.MapOverwatchServiceProviderEndpoints());
        _adminId = Guid.NewGuid(); var condominium = new Condominium("Residencial Teste", null, null);
        var user = CoreTestSeed.User("Ana", "ana@test.local"); var category = new Category(condominium.Id, "Reparos", null);
        var provider = new ServiceProvider("César", null, "Elétrica", null, "11999999999", null, "pix@cesar", ServiceProviderPixKeyType.Email, null, DateTime.UtcNow);
        var request = new Request(condominium.Id, user.Id, null, category.Id, "Luz", "Sem luz"); request.SetServiceProvider(provider.Id, DateTime.UtcNow);
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(user, condominium, category, provider, request, new ServiceProviderUserLink(provider.Id, user.Id), new ServiceProviderCondominiumLink(provider.Id, condominium.Id), new ServiceProviderSpecialty(provider.Id, "Elétrica"));
            db.RequestServiceProviderHistories.Add(new RequestServiceProviderHistory(request.Id, "Linked", null, null, provider.Name, provider.Specialty, user.Id, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
        _providerId = provider.Id; _requestId = request.Id;
    }

    [Fact]
    public async Task PlatformAdmin_lists_impact_and_hard_deletes_while_preserving_request_history()
    {
        var client = _host.ClientFor(_adminId); client.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);
        var list = await client.GetFromJsonAsync<List<OverwatchServiceProviderEndpoints.ProviderResponse>>("/overwatch/service-providers?search=C%C3%A9sar");
        Assert.Single(list!);
        var impact = await client.GetFromJsonAsync<OverwatchServiceProviderEndpoints.DeletionImpact>($"/overwatch/service-providers/{_providerId}/deletion-impact");
        Assert.Equal(1, impact!.CurrentRequests); Assert.Equal(1, impact.PersonalLinks); Assert.Equal(1, impact.CondominiumLinks);
        var response = await client.DeleteAsync($"/overwatch/service-providers/{_providerId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/overwatch/service-providers/{_providerId}") { Content = JsonContent.Create(new { confirmation = "EXCLUIR PERMANENTEMENTE" }) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.False(await db.ServiceProviders.AnyAsync(x => x.Id == _providerId));
            Assert.Null((await db.Requests.SingleAsync(x => x.Id == _requestId)).ServiceProviderId);
            Assert.Equal(RequestStatus.InProgress, (await db.Requests.SingleAsync(x => x.Id == _requestId)).Status);
            Assert.True(await db.RequestServiceProviderHistories.AnyAsync(x => x.RequestId == _requestId && x.ProviderName == "César"));
        });
    }

    [Fact]
    public async Task Non_platform_admin_is_rejected()
    {
        var client = _host.ClientFor(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/overwatch/service-providers")).StatusCode);
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();
}
