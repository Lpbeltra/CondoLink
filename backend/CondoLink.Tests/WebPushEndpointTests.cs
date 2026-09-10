using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.WebPush;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class WebPushEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private ApplicationUser _userA = null!;
    private ApplicationUser _userB = null!;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapWebPush(),
            builder =>
            {
                builder.Configuration["WebPush:Enabled"] = "true";
                builder.Configuration["WebPush:VapidPublicKey"] = "public";
                builder.Configuration["WebPush:VapidPrivateKey"] = "private";
                builder.Configuration["WebPush:Subject"] = "mailto:ops@comvy.test";
                builder.Services.Configure<WebPushOptions>(
                    builder.Configuration.GetSection(WebPushOptions.SectionName));
                builder.Services.AddSingleton(TimeProvider.System);
            });
        _userA = CoreTestSeed.User("Usuário A", "a.push@example.com");
        _userB = CoreTestSeed.User("Usuário B", "b.push@example.com");
        await _host.WithDbAsync(async db =>
        {
            db.AddRange(_userA, _userB);
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Register_is_idempotent_and_supports_multiple_devices()
    {
        var client = _host.ClientFor(_userA.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/users/me/push/subscriptions", Body("https://push.test/a", "one"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/users/me/push/subscriptions", Body("https://push.test/a", "updated"))).StatusCode);
        await client.PostAsJsonAsync("/users/me/push/subscriptions", Body("https://push.test/b", "two"));

        await _host.WithDbAsync(async db =>
        {
            var rows = await db.WebPushSubscriptions.Where(x => x.UserId == _userA.Id).ToArrayAsync();
            Assert.Equal(2, rows.Length);
            Assert.Equal(Key("updated", 65), rows.Single(x => x.Endpoint.EndsWith("/a")).P256dh);
            Assert.All(rows, row => Assert.True(row.IsActive));
        });
    }

    [Fact]
    public async Task Delete_only_deactivates_current_users_device()
    {
        var endpoint = "https://push.test/private";
        await _host.ClientFor(_userA.Id).PostAsJsonAsync(
            "/users/me/push/subscriptions", Body(endpoint, "a"));

        var response = await _host.ClientFor(_userB.Id).SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/users/me/push/subscriptions")
        { Content = JsonContent.Create(new { endpoint }) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await _host.WithDbAsync(db => db.WebPushSubscriptions
            .Where(x => x.UserId == _userA.Id).Select(x => x.IsActive).SingleAsync()));

        await _host.ClientFor(_userA.Id).SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/users/me/push/subscriptions")
        { Content = JsonContent.Create(new { endpoint }) });
        Assert.False(await _host.WithDbAsync(db => db.WebPushSubscriptions
            .Where(x => x.UserId == _userA.Id).Select(x => x.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Shared_browser_endpoint_is_safely_reassigned_after_user_switch()
    {
        var endpoint = "https://push.test/shared-browser";
        var userAClient = _host.ClientFor(_userA.Id);
        await userAClient.PostAsJsonAsync("/users/me/push/subscriptions", Body(endpoint, "a"));
        await userAClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            "/users/me/push/subscriptions") { Content = JsonContent.Create(new { endpoint }) });

        await _host.ClientFor(_userB.Id).PostAsJsonAsync(
            "/users/me/push/subscriptions", Body(endpoint, "b"));

        await _host.WithDbAsync(async db =>
        {
            var row = await db.WebPushSubscriptions.SingleAsync(x => x.Endpoint == endpoint);
            Assert.Equal(_userB.Id, row.UserId);
            Assert.True(row.IsActive);
            Assert.False(await db.WebPushSubscriptions.AnyAsync(
                x => x.UserId == _userA.Id && x.Endpoint == endpoint));
        });
    }

    [Fact]
    public async Task Invalid_endpoint_is_rejected()
    {
        var response = await _host.ClientFor(_userA.Id).PostAsJsonAsync(
            "/users/me/push/subscriptions", Body("javascript:alert(1)", "a"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _host.AnonymousClient().GetAsync("/users/me/push/config")).StatusCode);
    }

    [Fact]
    public async Task Disabled_config_exposes_no_public_key()
    {
        await using var disabled = await CoreEndpointTestHost.StartAsync(
            app => app.MapWebPush(), builder =>
            {
                builder.Services.Configure<WebPushOptions>(_ => { });
                builder.Services.AddSingleton(TimeProvider.System);
            });
        var user = CoreTestSeed.User("Disabled", "disabled@example.com");
        await disabled.WithDbAsync(async db => { db.Add(user); await db.SaveChangesAsync(); });
        var payload = await disabled.ClientFor(user.Id).GetFromJsonAsync<ConfigResponse>(
            "/users/me/push/config");
        Assert.False(payload!.Enabled);
        Assert.Null(payload.VapidPublicKey);
    }

    private static object Body(string endpoint, string key) =>
        new { endpoint, keys = new {
            p256dh = Key(key, 65),
            auth = Key(key, 16)
        } };
    private static string Key(string value, int length) => Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(value.PadRight(length, 'p')))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed record ConfigResponse(bool Enabled, string? VapidPublicKey);
}
