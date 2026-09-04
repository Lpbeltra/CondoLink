using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.Overwatch.SubManagers;
using CondoLink.Api.Features.Auth;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class SubManagerEditEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _platformId, _s1Id, _s2Id, _residentId, _condominiumId, _otherCondominiumId, _s1MembershipId, _s2MembershipId, _unitMembershipId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app => app.MapSubManagerEndpoints(), builder =>
        {
            builder.Services.AddScoped<FirstAccessService>();
            builder.Services.AddSingleton<IEmailSender>(new NoOpEmailSender());
        });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Monticello", null, null);
            var otherCondominium = new Condominium("Outro", null, null);
            var platform = CoreTestSeed.User("Platform", "platform-edit@test.local");
            var s1 = CoreTestSeed.User("S1 original", "s1-original@test.local");
            var s2 = CoreTestSeed.User("S2 original", "s2-original@test.local");
            var resident = new ApplicationUser("Aline Souza", "aline@test.local", "+5511999990010");
            resident.RequirePasswordChange();
            resident.PasswordHash = "existing-password-hash";
            resident.NormalizedUserName = "ALINE@TEST.LOCAL";
            resident.NormalizedEmail = "ALINE@TEST.LOCAL";
            resident.SetPix(PixKeyType.Email, "aline-pix@test.local");
            db.AddRange(condominium, otherCondominium, platform, s1, s2, resident);
            var unit = new Unit(condominium.Id, "304", null, null, null);
            db.Add(unit);
            var unitMembership = new UnitMembership(resident.Id, unit.Id, UnitRelationshipType.Owner, true, true);
            db.AddRange(unitMembership, new CondominiumMembership(resident.Id, condominium.Id));
            var m1 = CoreTestSeed.AddMember(db, s1.Id, condominium.Id, CondominiumRole.SubManager);
            var m2 = CoreTestSeed.AddMember(db, s2.Id, condominium.Id, CondominiumRole.SubManager);
            db.SubManagerModulePermissions.Add(new(m1.Id, SubManagerModule.Requests, platform.Id));
            db.SubManagerModulePermissions.Add(new(m2.Id, SubManagerModule.Agenda, platform.Id));
            await db.SaveChangesAsync();
            (_platformId, _s1Id, _s2Id, _residentId, _condominiumId, _otherCondominiumId, _s1MembershipId, _s2MembershipId, _unitMembershipId) = (platform.Id, s1.Id, s2.Id, resident.Id, condominium.Id, otherCondominium.Id, m1.Id, m2.Id, unitMembership.Id);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Editing_s1_preserves_membership_permissions_and_s2()
    {
        var before = await Snapshot(_s2Id);
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.PutAsJsonAsync($"/overwatch/submanagers/{_s1Id}", new
        {
            fullName = "S1 updated", email = "s1-updated@test.local", phoneNumber = "+5511999990001",
            condominiumId = _condominiumId, pixKeyType = "Email", pixKey = "s1-pix@test.local"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            var s1 = await db.Users.SingleAsync(x => x.Id == _s1Id);
            Assert.Equal("S1 updated", s1.FullName); Assert.Equal("s1-updated@test.local", s1.Email);
            Assert.Equal("+5511999990001", s1.PhoneNumber); Assert.Equal(PixKeyType.Email, s1.PixKeyType); Assert.Equal("s1-pix@test.local", s1.PixKey);
            Assert.Equal(_s1MembershipId, await db.CondominiumMemberships.Where(x => x.UserId == _s1Id).Select(x => x.Id).SingleAsync());
            Assert.Equal(_condominiumId, await db.CondominiumMemberships.Where(x => x.UserId == _s1Id).Select(x => x.CondominiumId).SingleAsync());
            Assert.True(await db.SubManagerModulePermissions.AnyAsync(x => x.CondominiumMembershipId == _s1MembershipId && x.Module == SubManagerModule.Requests));
            var after = await Snapshot(_s2Id, db);
            Assert.Equal(before with { Permissions = after.Permissions }, after);
            Assert.Equal(before.Permissions, after.Permissions);
        });
    }

    [Fact]
    public async Task Permission_catalog_hides_legacy_requests_without_deleting_it()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");

        var response = await client.GetAsync($"/overwatch/submanagers/{_s1Id}/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<PermissionRow>>();
        Assert.Equal(6, rows!.Count);
        Assert.DoesNotContain(rows, row => row.Module == "Requests");
        Assert.Contains(rows, row => row.Module == "Attendance");

        await _host.WithDbAsync(async db =>
            Assert.True(await db.SubManagerModulePermissions.AnyAsync(x =>
                x.CondominiumMembershipId == _s1MembershipId && x.Module == SubManagerModule.Requests)));
    }

    [Fact]
    public async Task Creating_submanager_accepts_current_frontend_payload()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            fullName = "Novo Subsíndico", email = "novo-submanager@test.local",
            phoneNumber = "+5511999990003", condominiumId = _condominiumId,
            pixKeyType = (string?)null, pixKey = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Existing_resident_is_promoted_without_changing_user_or_unit_link()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            existingUserId = _residentId, condominiumId = _condominiumId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SubManagerEndpoints.CreatedResponse>();
        Assert.Equal(_residentId, created!.Id);
        Assert.Null(created.TemporaryPassword);
        await _host.WithDbAsync(async db =>
        {
            var resident = await db.Users.SingleAsync(x => x.Id == _residentId);
            Assert.Equal("existing-password-hash", resident.PasswordHash);
            Assert.True(resident.MustChangePassword);
            Assert.Null(resident.LastLoginAt);
            Assert.Null(resident.PasswordChangedAt);
            Assert.True(await db.UnitMemberships.AnyAsync(x => x.Id == _unitMembershipId && x.IsActive));
            Assert.Equal(1, await db.CondominiumMemberships.CountAsync(x => x.UserId == _residentId && x.CondominiumId == _condominiumId));
            Assert.True(await (from m in db.CondominiumMemberships
                join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
                where m.UserId == _residentId && m.CondominiumId == _condominiumId
                    && r.Role == CondominiumRole.SubManager && m.IsActive && r.IsActive && r.RevokedAt == null
                select r).AnyAsync());
        });

        var second = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            existingUserId = _residentId, condominiumId = _condominiumId
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            var membershipId = await db.CondominiumMemberships
                .Where(x => x.UserId == _residentId && x.CondominiumId == _condominiumId)
                .Select(x => x.Id).SingleAsync();
            Assert.Equal(1, await db.CondominiumMemberships.CountAsync(x => x.UserId == _residentId && x.CondominiumId == _condominiumId));
            Assert.Equal(1, await db.CondominiumMembershipRoles.CountAsync(x => x.CondominiumMembershipId == membershipId && x.Role == CondominiumRole.SubManager));
            Assert.Equal(6, await db.SubManagerModulePermissions.CountAsync(x => x.CondominiumMembershipId == membershipId));
        });
        var listed = await client.GetFromJsonAsync<List<SubManagerEndpoints.Response>>("/overwatch/submanagers");
        Assert.Contains(listed!, item => item.Id == _s1Id && item.CondominiumId == _condominiumId && item.HasActiveLink);
        Assert.Contains(listed!, item => item.Id == _s2Id && item.CondominiumId == _condominiumId && item.HasActiveLink);
        Assert.Contains(listed!, item => item.Id == _residentId && item.CondominiumId == _condominiumId && item.HasActiveLink);
        Assert.Equal(3, listed!.Count(item => item.CondominiumId == _condominiumId && item.HasActiveLink));
    }

    [Fact]
    public async Task Existing_submanager_in_other_condominium_returns_conflict()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            existingUserId = _s1Id, condominiumId = _otherCondominiumId
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Existing_inactive_submanager_is_reactivated_in_same_condominium()
    {
        await _host.WithDbAsync(async db =>
        {
            var role = await db.CondominiumMembershipRoles.SingleAsync(x =>
                x.CondominiumMembershipId == _s2MembershipId && x.Role == CondominiumRole.SubManager);
            role.Deactivate();
            await db.SaveChangesAsync();
        });

        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            existingUserId = _s2Id, condominiumId = _condominiumId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.True(await (from m in db.CondominiumMemberships
                join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
                where m.Id == _s2MembershipId && m.IsActive && m.EndedAt == null
                    && r.Role == CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null
                select r).AnyAsync());
        });
    }

    [Fact]
    public async Task Search_returns_existing_resident_without_sensitive_identity_data()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var response = await client.GetAsync($"/overwatch/submanagers/search?query=Aline&condominiumId={_condominiumId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(_residentId.ToString(), body);
        Assert.Contains("304", body);
        Assert.DoesNotContain("PasswordHash", body);
    }

    [Fact]
    public async Task Creating_two_submanagers_in_same_condominium_persists_user_link_permissions_pix_and_list()
    {
        using var client = _host.ClientFor(_platformId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var first = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            fullName = "Subsíndico PIX", email = "submanager-pix@test.local",
            phoneNumber = "+5511999990004", condominiumId = _condominiumId,
            pixKeyType = "Email", pixKey = "submanager-pix@test.local"
        });
        var second = await client.PostAsJsonAsync("/overwatch/submanagers", new
        {
            fullName = "Subsíndico sem PIX", email = "submanager-no-pix@test.local",
            phoneNumber = (string?)null, condominiumId = _condominiumId,
            pixKeyType = (string?)null, pixKey = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstCreated = await first.Content.ReadFromJsonAsync<SubManagerEndpoints.CreatedResponse>();
        var secondCreated = await second.Content.ReadFromJsonAsync<SubManagerEndpoints.CreatedResponse>();
        Assert.NotEqual(firstCreated!.Id, secondCreated!.Id);

        await _host.WithDbAsync(async db =>
        {
            var users = await db.Users.Where(x => x.Id == firstCreated.Id || x.Id == secondCreated.Id).ToListAsync();
            Assert.Equal(2, users.Count);
            Assert.All(users, user => Assert.True(user.MustChangePassword));
            Assert.Equal(PixKeyType.Email, users.Single(x => x.Id == firstCreated.Id).PixKeyType);
            Assert.Equal("submanager-pix@test.local", users.Single(x => x.Id == firstCreated.Id).PixKey);
            Assert.Null(users.Single(x => x.Id == secondCreated.Id).PixKeyType);
            Assert.Equal(6, await db.SubManagerModulePermissions.CountAsync(x => x.CondominiumMembershipId ==
                db.CondominiumMemberships.Where(m => m.UserId == firstCreated.Id).Select(m => m.Id).Single()));
            Assert.Equal(4, await (from m in db.CondominiumMemberships
                join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
                where m.CondominiumId == _condominiumId && r.Role == CondominiumRole.SubManager
                    && m.IsActive && r.IsActive && r.RevokedAt == null
                select r.Id).CountAsync());
        });

        var listed = await client.GetFromJsonAsync<List<SubManagerEndpoints.Response>>("/overwatch/submanagers");
        Assert.Contains(listed!, item => item.Id == firstCreated.Id);
        Assert.Contains(listed!, item => item.Id == secondCreated.Id);
    }

    [Fact]
    public async Task Exclusive_submanager_can_be_hard_deleted_and_confirmation_is_required()
    {
        using var client = _host.ClientFor(_platformId); client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var eligibility = await client.GetFromJsonAsync<JsonElement>($"/overwatch/submanagers/{_s2Id}/hard-delete-eligibility");
        Assert.True(eligibility.GetProperty("canHardDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/overwatch/submanagers/{_s2Id}/hard-delete")).StatusCode);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/overwatch/submanagers/{_s2Id}/hard-delete") { Content = JsonContent.Create(new { confirmation = "EXCLUIR PERMANENTEMENTE" }) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _host.WithDbAsync(async db => { Assert.False(await db.Users.AnyAsync(x => x.Id == _s2Id)); Assert.False(await db.CondominiumMemberships.AnyAsync(x => x.Id == _s2MembershipId)); Assert.False(await db.SubManagerModulePermissions.AnyAsync(x => x.CondominiumMembershipId == _s2MembershipId)); });
    }

    [Fact]
    public async Task Promoted_resident_hard_delete_removes_only_submanager_role()
    {
        using var client = _host.ClientFor(_platformId); client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        await client.PostAsJsonAsync("/overwatch/submanagers", new { existingUserId = _residentId, condominiumId = _condominiumId });
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/overwatch/submanagers/{_residentId}/hard-delete") { Content = JsonContent.Create(new { confirmation = "EXCLUIR PERMANENTEMENTE" }) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _host.WithDbAsync(async db => { Assert.True(await db.Users.AnyAsync(x => x.Id == _residentId)); Assert.True(await db.UnitMemberships.AnyAsync(x => x.Id == _unitMembershipId)); Assert.True(await db.CondominiumMemberships.AnyAsync(x => x.UserId == _residentId)); Assert.False(await (from r in db.CondominiumMembershipRoles join m in db.CondominiumMemberships on r.CondominiumMembershipId equals m.Id where m.UserId == _residentId && r.Role == CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null select r).AnyAsync()); });
    }

    [Fact]
    public async Task Hard_delete_revalidates_history_and_returns_conflict_without_partial_delete()
    {
        using var client = _host.ClientFor(_platformId); client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        var eligibility = await client.GetFromJsonAsync<JsonElement>($"/overwatch/submanagers/{_s2Id}/hard-delete-eligibility"); Assert.True(eligibility.GetProperty("canHardDelete").GetBoolean());
        await _host.WithDbAsync(async db => { var category = new Category(_condominiumId, "Histórico", null); db.Add(category); await db.SaveChangesAsync(); db.Requests.Add(new Request(_condominiumId, _s2Id, null, category.Id, "Histórico", "Histórico")); await db.SaveChangesAsync(); });
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/overwatch/submanagers/{_s2Id}/hard-delete") { Content = JsonContent.Create(new { confirmation = "EXCLUIR PERMANENTEMENTE" }) });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _host.WithDbAsync(async db => Assert.True(await db.Users.AnyAsync(x => x.Id == _s2Id)));
    }

    private Task<S2Snapshot> Snapshot(Guid userId) => _host.WithDbAsync(db => Snapshot(userId, db));
    private static async Task<S2Snapshot> Snapshot(Guid userId, AppDbContext db)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
        var membership = await db.CondominiumMemberships.AsNoTracking().SingleAsync(x => x.UserId == userId);
        return new(user.FullName, user.Email, user.PhoneNumber, user.PixKeyType, user.PixKey, membership.Id, membership.CondominiumId,
            await db.SubManagerModulePermissions.AsNoTracking().Where(x => x.CondominiumMembershipId == membership.Id).Select(x => new PermissionSnapshot(x.Module, x.IsAllowed)).ToArrayAsync());
    }

    private sealed record PermissionSnapshot(SubManagerModule Module, bool Allowed);
    private sealed record PermissionRow(string Module, bool Allowed);
    private sealed record S2Snapshot(string FullName, string? Email, string? Phone, PixKeyType? PixType, string? PixKey, Guid MembershipId, Guid CondominiumId, PermissionSnapshot[] Permissions);
}
