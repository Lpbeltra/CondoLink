using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ResetMemberTemporaryPasswordTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _managerId;
    private Guid _residentId;
    private Guid _targetId;
    private Guid _otherUserId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application => application.MapResetMemberTemporaryPassword());

        await _host.WithServicesAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            await _host.WithDbAsync(async db =>
            {
                var condominium = new Condominium("Alfa", null, null);
                var otherCondominium = new Condominium("Beta", null, null);
                var manager = CoreTestSeed.User("Síndico", "reset-manager@example.com");
                var resident = CoreTestSeed.User("Morador", "reset-resident@example.com");
                var target = CoreTestSeed.User("Alvo", "reset-target@example.com");
                var other = CoreTestSeed.User("Outro", "reset-other@example.com");
                db.AddRange(condominium, otherCondominium);
                await db.SaveChangesAsync();

                Assert.True((await userManager.CreateAsync(manager, "Manager123")).Succeeded);
                Assert.True((await userManager.CreateAsync(resident, "Resident123")).Succeeded);
                Assert.True((await userManager.CreateAsync(target, "Original123")).Succeeded);
                Assert.True((await userManager.CreateAsync(other, "OtherPass123")).Succeeded);

                CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
                CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
                CoreTestSeed.AddMember(db, target.Id, condominium.Id, CondominiumRole.Resident);
                CoreTestSeed.AddMember(db, other.Id, otherCondominium.Id, CondominiumRole.Resident);
                await db.SaveChangesAsync();

                _condominiumId = condominium.Id;
                _otherCondominiumId = otherCondominium.Id;
                _managerId = manager.Id;
                _residentId = resident.Id;
                _targetId = target.Id;
                _otherUserId = other.Id;
            });
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_reset_a_member_password_and_old_password_is_invalidated()
    {
        var response = await ResetAsync(_managerId, _condominiumId, _targetId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ResetMemberTemporaryPassword.Response>();
        Assert.False(string.IsNullOrWhiteSpace(body!.TemporaryPassword));

        await _host.WithServicesAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(_targetId.ToString());
            Assert.NotNull(user);
            Assert.False(await userManager.CheckPasswordAsync(user!, "Original123"));
            Assert.True(await userManager.CheckPasswordAsync(user!, body.TemporaryPassword));
            Assert.True(user!.MustChangePassword);
            Assert.True(user.ReceiveWhatsAppUpdates);
        });
    }

    [Fact]
    public async Task Resident_cannot_reset_passwords()
    {
        var response = await ResetAsync(_residentId, _condominiumId, _targetId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_reset_a_user_from_another_condominium()
    {
        var response = await ResetAsync(_managerId, _condominiumId, _otherUserId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_reset_a_member_without_a_manager_membership()
    {
        var client = _host.ClientFor(_residentId);
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            DependencyInjection.PlatformAdminRole);

        var response = await client.PostAsync(
            $"/condominiums/{_otherCondominiumId}/members/{_otherUserId}/reset-temporary-password",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> ResetAsync(
        Guid callerId,
        Guid condominiumId,
        Guid userId) =>
        _host.ClientFor(callerId).PostAsync(
            $"/condominiums/{condominiumId}/members/{userId}/reset-temporary-password",
            null);
}
