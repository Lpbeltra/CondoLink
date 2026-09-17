using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CondoLink.Api.Features.Auth;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class PasswordResetEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private HttpClient _client = null!;
    private RecordingEmailSender _email = null!;
    private Guid _eligibleUserId;

    public async Task InitializeAsync()
    {
        _email = new RecordingEmailSender();
        _host = await CoreEndpointTestHost.StartAsync(
            app =>
            {
                app.MapLogin();
                app.MapRefreshEndpoints();
                app.MapPasswordReset();
            },
            builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "tests",
                    ["Jwt:Audience"] = "tests",
                    ["Jwt:Key"] = "password-reset-test-key-with-at-least-32-bytes",
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["FirstAccess:FrontendBaseUrl"] = "https://app.example.test"
                });
                builder.Services.AddMemoryCache();
                builder.Services.Configure<FirstAccessOptions>(builder.Configuration.GetSection(FirstAccessOptions.SectionName));
                builder.Services.Configure<AuthenticationSessionOptions>(builder.Configuration.GetSection(AuthenticationSessionOptions.SectionName));
                builder.Services.AddSingleton<IEmailSender>(_email);
                builder.Services.AddSingleton<PasswordResetRequestLimiter>();
                builder.Services.AddScoped<PasswordResetService>();
            });
        _client = _host.AnonymousClient();

        _eligibleUserId = await CreateUserAsync("elegivel@comvy.test", emailDeliveryEnabled: true);
        await CreateUserAsync("inativo@comvy.test", emailDeliveryEnabled: true, isActive: false);
        await CreateUserAsync("sem-envio@comvy.test", emailDeliveryEnabled: false);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Forgot_password_is_neutral_and_only_sends_for_an_eligible_user()
    {
        var valid = await ForgotAsync("elegivel@comvy.test");
        var unknown = await ForgotAsync("desconhecido@comvy.test");
        var inactive = await ForgotAsync("inativo@comvy.test");
        var disabled = await ForgotAsync("sem-envio@comvy.test");
        var fictional = await ForgotAsync("ficticio@example.test");

        Assert.All(new[] { valid, unknown, inactive, disabled, fictional }, response =>
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        });
        var bodies = await Task.WhenAll(valid.Content.ReadAsStringAsync(), unknown.Content.ReadAsStringAsync(), inactive.Content.ReadAsStringAsync(), disabled.Content.ReadAsStringAsync(), fictional.Content.ReadAsStringAsync());
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));
        Assert.Single(_email.Messages);
        Assert.Contains("/reset-password?", _email.Messages[0].Html);
        Assert.DoesNotContain("elegivel@comvy.test", _email.Messages[0].Html);
    }

    [Fact]
    public async Task Forgot_password_keeps_a_neutral_response_when_smtp_fails()
    {
        _email.ThrowOnSend = true;

        var response = await ForgotAsync("elegivel@comvy.test");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(_email.Messages);
    }

    [Fact]
    public async Task Forgot_password_limits_delivery_without_changing_the_public_response()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await ForgotAsync("elegivel@comvy.test");
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(5, _email.Messages.Count);
    }

    [Fact]
    public async Task Reset_password_clears_temporary_state_revokes_refresh_and_invalidates_old_jwt()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new { email = "elegivel@comvy.test", password = "Passw0rd1" });
        var before = await login.Content.ReadFromJsonAsync<Login.Response>();
        var cookie = Cookie(login);
        Assert.True(StampMatches(before!.AccessToken, await SecurityStampAsync(_eligibleUserId)));
        await _host.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(_eligibleUserId.ToString());
            user!.RequirePasswordChange();
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        });

        await ForgotAsync("elegivel@comvy.test");
        var link = LinkFrom(_email.Messages.Single());
        var response = await ResetAsync(link, "NovaSenha2", "NovaSenha2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(StampMatches(before.AccessToken, await SecurityStampAsync(_eligibleUserId)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookieAsync(cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/auth/login", new { email = "elegivel@comvy.test", password = "Passw0rd1" })).StatusCode);

        var fresh = await _client.PostAsJsonAsync("/auth/login", new { email = "elegivel@comvy.test", password = "NovaSenha2" });
        var after = await fresh.Content.ReadFromJsonAsync<Login.Response>();
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.True(StampMatches(after!.AccessToken, await SecurityStampAsync(_eligibleUserId)));
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(_eligibleUserId);
            Assert.False(user!.MustChangePassword);
            Assert.NotNull(user.PasswordChangedAt);
        });
    }

    [Fact]
    public async Task Reset_rejects_invalid_weak_and_reused_tokens()
    {
        var mismatch = await _client.PostAsJsonAsync("/auth/reset-password", new { userId = _eligibleUserId, token = "ignored", password = "NovaSenha2", confirmation = "OutraSenha3" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        var invalid = await _client.PostAsJsonAsync("/auth/reset-password", new { userId = _eligibleUserId, token = "invalid", password = "NovaSenha2", confirmation = "NovaSenha2" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await ForgotAsync("elegivel@comvy.test");
        var link = LinkFrom(_email.Messages.Single());
        var weak = await ResetAsync(link, "Fraca1", "Fraca1");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var completed = await ResetAsync(link, "NovaSenha2", "NovaSenha2");
        var reused = await ResetAsync(link, "OutraSenha3", "OutraSenha3");
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task Multiple_links_are_valid_until_one_is_used_then_security_stamp_invalidates_the_other()
    {
        await ForgotAsync("elegivel@comvy.test");
        await ForgotAsync("elegivel@comvy.test");
        var links = _email.Messages.Select(LinkFrom).ToArray();

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(links[0], "NovaSenha2", "NovaSenha2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(links[1], "OutraSenha3", "OutraSenha3")).StatusCode);
    }

    [Fact]
    public async Task Reset_rejects_a_link_when_the_account_becomes_inactive()
    {
        await ForgotAsync("elegivel@comvy.test");
        var link = LinkFrom(_email.Messages.Single());
        await _host.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(_eligibleUserId.ToString());
            user!.SetActiveStatus(false);
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        });

        Assert.Equal(HttpStatusCode.BadRequest,
            (await ResetAsync(link, "NovaSenha2", "NovaSenha2")).StatusCode);
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) => _client.PostAsJsonAsync("/auth/forgot-password", new { email });

    private async Task<HttpResponseMessage> ResetAsync(Uri link, string password, string confirmation)
    {
        var values = QueryHelpers.ParseQuery(link.Query);
        return await _client.PostAsJsonAsync("/auth/reset-password", new
        {
            userId = Guid.Parse(values["userId"].ToString()),
            token = values["token"].ToString(),
            password,
            confirmation
        });
    }

    private static Uri LinkFrom(EmailMessage message)
    {
        var href = Regex.Match(message.Html, "href=\\\"([^\\\"]+)\\\"").Groups[1].Value;
        return new Uri(WebUtility.HtmlDecode(href));
    }

    private async Task<Guid> CreateUserAsync(string email, bool emailDeliveryEnabled, bool isActive = true)
    {
        return await _host.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser("Usuário de Teste", email, null);
            user.SetEmailDeliveryEnabled(emailDeliveryEnabled);
            if (!isActive) user.SetActiveStatus(false);
            Assert.True((await users.CreateAsync(user, "Passw0rd1")).Succeeded);
            return user.Id;
        });
    }

    private Task<string?> SecurityStampAsync(Guid userId) => _host.WithDbAsync(async db => (await db.Users.FindAsync(userId))!.SecurityStamp);
    private static bool StampMatches(string token, string? stamp)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return SecurityStampJwtValidator.Matches(new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims)), stamp);
    }
    private async Task<HttpResponseMessage> PostWithCookieAsync(string cookie)
    { using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh"); request.Headers.Add("Cookie", $"{AuthenticationSessionService.CookieName}={cookie}"); return await _client.SendAsync(request); }
    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1];

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public bool ThrowOnSend { get; set; }
        public Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken)
        {
            Messages.Add(new EmailMessage(subject, html));
            if (ThrowOnSend) throw new InvalidOperationException("SMTP unavailable");
            return Task.CompletedTask;
        }
    }
    private sealed record EmailMessage(string Subject, string Html);
}
