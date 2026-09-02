using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.SubManagers;
using CondoLink.Api.Features.Auth;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class SubManagerEditEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _platformId, _s1Id, _s2Id, _condominiumId, _s1MembershipId, _s2MembershipId;

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
            var platform = CoreTestSeed.User("Platform", "platform-edit@test.local");
            var s1 = CoreTestSeed.User("S1 original", "s1-original@test.local");
            var s2 = CoreTestSeed.User("S2 original", "s2-original@test.local");
            db.AddRange(condominium, platform, s1, s2);
            var m1 = CoreTestSeed.AddMember(db, s1.Id, condominium.Id, CondominiumRole.SubManager);
            var m2 = CoreTestSeed.AddMember(db, s2.Id, condominium.Id, CondominiumRole.SubManager);
            db.SubManagerModulePermissions.Add(new(m1.Id, SubManagerModule.Requests, platform.Id));
            db.SubManagerModulePermissions.Add(new(m2.Id, SubManagerModule.Agenda, platform.Id));
            await db.SaveChangesAsync();
            (_platformId, _s1Id, _s2Id, _condominiumId, _s1MembershipId, _s2MembershipId) = (platform.Id, s1.Id, s2.Id, condominium.Id, m1.Id, m2.Id);
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

    private Task<S2Snapshot> Snapshot(Guid userId) => _host.WithDbAsync(db => Snapshot(userId, db));
    private static async Task<S2Snapshot> Snapshot(Guid userId, AppDbContext db)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
        var membership = await db.CondominiumMemberships.AsNoTracking().SingleAsync(x => x.UserId == userId);
        return new(user.FullName, user.Email, user.PhoneNumber, user.PixKeyType, user.PixKey, membership.Id, membership.CondominiumId,
            await db.SubManagerModulePermissions.AsNoTracking().Where(x => x.CondominiumMembershipId == membership.Id).Select(x => new PermissionSnapshot(x.Module, x.IsAllowed)).ToArrayAsync());
    }

    private sealed record PermissionSnapshot(SubManagerModule Module, bool Allowed);
    private sealed record S2Snapshot(string FullName, string? Email, string? Phone, PixKeyType? PixType, string? PixKey, Guid MembershipId, Guid CondominiumId, PermissionSnapshot[] Permissions);
}
