using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class NotificationEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _userId;
    private Guid _otherUserId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application => application.MapNotifications());
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var user = CoreTestSeed.User("Morador", "notificacao@example.com");
            var other = CoreTestSeed.User("Vizinho", "vizinho-notificacao@example.com");
            db.AddRange(condominium, user, other);
            db.Notifications.AddRange(
                new Notification(user.Id, condominium.Id,
                    NotificationType.RequestCreated, "A", "A"),
                new Notification(user.Id, condominium.Id,
                    NotificationType.RequestMessageReceived, "B", "B"),
                new Notification(other.Id, condominium.Id,
                    NotificationType.RequestCreated, "C", "C"));
            await db.SaveChangesAsync();
            _userId = user.Id;
            _otherUserId = other.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Mark_all_is_recipient_scoped_and_idempotent()
    {
        var client = _host.ClientFor(_userId);

        var first = await client.PatchAsync("/notifications/read-all", null);
        var second = await client.PatchAsync("/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, (await first.Content
            .ReadFromJsonAsync<NotificationEndpoints.MarkAllReadResponse>())!.Updated);
        Assert.Equal(0, (await second.Content
            .ReadFromJsonAsync<NotificationEndpoints.MarkAllReadResponse>())!.Updated);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Notifications.CountAsync(
            item => item.RecipientUserId == _userId && item.ReadAt == null)));
        Assert.Equal(1, await _host.WithDbAsync(db => db.Notifications.CountAsync(
            item => item.RecipientUserId == _otherUserId && item.ReadAt == null)));
    }
}
