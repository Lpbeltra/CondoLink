using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class ManagementCompanyAccessHardDeleteEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _platformId, _companyId, _condominiumId, _categoryId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapManagementCompanyAccessLifecycleEndpoints());
        await _host.WithDbAsync(async db =>
        {
            var company = new ManagementCompany("Administradora teste", null, null, null, null, null, null);
            var condominium = new Condominium("Condomínio teste", null, null);
            var category = new ManagementCompanyRequestCategory(
                company.Id, "Solicitação", null, ManagementCompanyRequestFormType.Generic);
            var platform = CoreTestSeed.User("Platform", "platform-company-delete@test.local");
            db.AddRange(company, condominium, category, platform);
            await db.SaveChangesAsync();
            (_platformId, _companyId, _condominiumId, _categoryId) =
                (platform.Id, company.Id, condominium.Id, category.Id);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Exclusive_access_is_deleted_with_user_after_eligibility()
    {
        var (userId, accessId) = await CreateAccessAsync("Exclusivo");
        using var client = PlatformClient();

        var eligibility = await EligibilityAsync(client, accessId);
        Assert.True(eligibility.GetProperty("canHardDelete").GetBoolean());

        var response = await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _host.WithDbAsync(async db =>
        {
            Assert.False(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId));
            Assert.False(await db.ManagementCompanyRequestCategoryResponsibles.AnyAsync(x => x.ManagementCompanyEmployeeId == accessId));
            Assert.False(await db.Users.AnyAsync(x => x.Id == userId));
        });
    }

    [Fact]
    public async Task Historical_access_is_not_deleted()
    {
        var (userId, accessId) = await CreateAccessAsync("Com histórico");
        var requestId = await AddHistoryAsync(userId);
        using var client = PlatformClient();

        var eligibility = await EligibilityAsync(client, accessId);
        Assert.False(eligibility.GetProperty("canHardDelete").GetBoolean());
        var response = await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _host.WithDbAsync(async db =>
        {
            Assert.True(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId));
            Assert.True(await db.Users.AnyAsync(x => x.Id == userId));
            Assert.True(await db.ManagementCompanyRequests.AnyAsync(x => x.Id == requestId));
        });
    }

    [Fact]
    public async Task Confirmation_is_required_before_deletion()
    {
        var (userId, accessId) = await CreateAccessAsync("Confirmação");
        using var client = PlatformClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await DeleteAsync(client, accessId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeleteAsync(client, accessId, "EXCLUIR")).StatusCode);
        await _host.WithDbAsync(async db => Assert.True(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId)));

        Assert.Equal(HttpStatusCode.NoContent,
            (await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE")).StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.False(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId));
            Assert.False(await db.Users.AnyAsync(x => x.Id == userId));
        });
    }

    [Fact]
    public async Task Shared_user_is_preserved_when_access_is_deleted()
    {
        var (userId, accessId) = await CreateAccessAsync("Compartilhado", withResidence: true);
        using var client = PlatformClient();

        var eligibility = await EligibilityAsync(client, accessId);
        Assert.False(eligibility.GetProperty("canHardDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,
            (await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE")).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            Assert.True(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId));
            Assert.True(await db.CondominiumMemberships.AnyAsync(x => x.UserId == userId));
            Assert.True(await db.Users.AnyAsync(x => x.Id == userId));
        });
    }

    [Fact]
    public async Task Delete_revalidates_dependency_added_after_eligibility()
    {
        var (userId, accessId) = await CreateAccessAsync("TOCTOU");
        using var client = PlatformClient();
        var eligibility = await EligibilityAsync(client, accessId);
        Assert.True(eligibility.GetProperty("canHardDelete").GetBoolean());
        var requestId = await AddHistoryAsync(userId);

        Assert.Equal(HttpStatusCode.Conflict,
            (await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE")).StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.True(await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == accessId));
            Assert.True(await db.Users.AnyAsync(x => x.Id == userId));
            Assert.True(await db.ManagementCompanyRequests.AnyAsync(x => x.Id == requestId));
        });
    }

    [Fact]
    public async Task Delete_requires_platform_admin()
    {
        var user = await _host.WithDbAsync(async db =>
        {
            var outsider = CoreTestSeed.User("Outsider", $"outsider-{Guid.NewGuid():N}@test.local");
            db.Add(outsider);
            await db.SaveChangesAsync();
            return outsider.Id;
        });
        var (_, accessId) = await CreateAccessAsync("Protegido");
        using var client = _host.ClientFor(user);

        var response = await DeleteAsync(client, accessId, "EXCLUIR PERMANENTEMENTE");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient PlatformClient()
    {
        var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        return client;
    }

    private static async Task<JsonElement> EligibilityAsync(HttpClient client, Guid accessId) =>
        await client.GetFromJsonAsync<JsonElement>(
            $"/overwatch/management-company-accesses/{accessId}/hard-delete-eligibility");

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid accessId, string? confirmation) =>
        client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/overwatch/management-company-accesses/{accessId}/hard-delete")
        {
            Content = confirmation is null ? null : JsonContent.Create(new { confirmation })
        });

    private async Task<(Guid UserId, Guid AccessId)> CreateAccessAsync(
        string name, bool withResidence = false)
    {
        return await _host.WithDbAsync(async db =>
        {
            var user = CoreTestSeed.User(name, $"{Guid.NewGuid():N}@test.local");
            var access = new ManagementCompanyEmployee(_companyId, user.Id, "Teste");
            db.AddRange(user, access);
            db.Add(new ManagementCompanyRequestCategoryResponsible(_categoryId, access.Id));
            if (withResidence)
                db.Add(new CondominiumMembership(user.Id, _condominiumId));
            await db.SaveChangesAsync();
            return (user.Id, access.Id);
        });
    }

    private Task<Guid> AddHistoryAsync(Guid userId) => _host.WithDbAsync(async db =>
    {
        var request = new ManagementCompanyRequest(
            _condominiumId, _companyId, _categoryId, userId,
            ManagementCompanyRequestType.GeneralQuestion);
        db.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    });
}
