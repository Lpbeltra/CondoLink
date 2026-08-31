using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>
/// POST /auth/login is the single anonymous entry point of the API. It must
/// reject malformed input with 400, bad credentials with 401 (without leaking
/// whether the account exists), deactivated accounts with 403, and otherwise
/// hand back a signed JWT that carries the caller's identity.
///
/// Real Jwt:* configuration is wired into the test host, so the issued token is
/// parsed and asserted rather than merely checked for presence.
/// </summary>
public sealed class LoginEndpointTests : IAsyncLifetime
{
    private const string Issuer = "condolink-tests";
    private const string Audience = "condolink-tests-audience";
    private const string SigningKey =
        "condolink-test-signing-key-with-at-least-32-bytes";

    private CoreEndpointTestHost _host = null!;
    private HttpClient _client = null!;
    private Guid _activeUserId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application =>
            {
                application.MapLogin();
                application.MapChangeTemporaryPassword();
                application.MapRefreshEndpoints();
            },
            builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:Key"] = SigningKey,
                    ["Jwt:ExpirationMinutes"] = "60"
                }));

        _client = _host.AnonymousClient();

        await _host.WithServicesAsync(async services =>
        {
            var userManager = services
                .GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await roleManager.CreateAsync(
                new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole));

            var active = new ApplicationUser(
                "Morador Ativo", "ativo@example.com", null);
            Assert.True((await userManager.CreateAsync(active, "Passw0rd1"))
                .Succeeded);
            await userManager.AddToRoleAsync(
                active, DependencyInjection.PlatformAdminRole);
            _activeUserId = active.Id;

            var inactive = new ApplicationUser(
                "Morador Inativo", "inativo@example.com", null);
            inactive.SetActiveStatus(false);
            Assert.True((await userManager.CreateAsync(inactive, "Passw0rd1"))
                .Succeeded);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_email_returns_400(string? email)
    {
        var response = await LoginAsync(email, "Passw0rd1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("nao-e-email")]
    [InlineData("sem-arroba.example.com")]
    [InlineData("@example.com")]
    public async Task Invalid_email_format_returns_400(string email)
    {
        var response = await LoginAsync(email, "Passw0rd1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_password_returns_400(string? password)
    {
        var response = await LoginAsync("ativo@example.com", password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await LoginAsync("ativo@example.com", "SenhaErrada1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_returns_401_just_like_a_wrong_password()
    {
        var response = await LoginAsync("ninguem@example.com", "Passw0rd1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_with_the_right_password_returns_403()
    {
        var response = await LoginAsync("inativo@example.com", "Passw0rd1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_with_a_wrong_password_still_returns_401()
    {
        var response = await LoginAsync("inativo@example.com", "SenhaErrada1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Successful_login_returns_a_bearer_token_and_the_user_profile()
    {
        var response = await LoginAsync("ativo@example.com", "Passw0rd1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Login.Response>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(3600, body.ExpiresIn);
        Assert.Equal(_activeUserId, body.User.Id);
        Assert.Equal("Morador Ativo", body.User.FullName);
        Assert.Equal("ativo@example.com", body.User.Email);
        Assert.True(body.User.IsActive);
        Assert.Equal([DependencyInjection.PlatformAdminRole], body.User.Roles);
    }

    [Fact]
    public async Task Refresh_rotates_cookie_and_rejects_reuse_then_logout_revokes_current_session()
    {
        var login = await LoginAsync("ativo@example.com", "Passw0rd1");
        var first = Cookie(login);
        Assert.Contains("httponly", string.Join(";", login.Headers.GetValues("Set-Cookie")).ToLowerInvariant());
        Assert.DoesNotContain(first, await login.Content.ReadAsStringAsync());

        var refreshed = await PostWithCookieAsync("/auth/refresh", first);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = Cookie(refreshed);
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookieAsync("/auth/refresh", first)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/auth/logout", second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookieAsync("/auth/refresh", second)).StatusCode);
    }

    [Fact]
    public async Task Refresh_rejects_random_token_inactive_user_and_changed_security_stamp()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await PostWithCookieAsync("/auth/refresh", "random-or-tampered")).StatusCode);
        var inactiveLogin = await LoginAsync("ativo@example.com", "Passw0rd1");
        var inactiveCookie = Cookie(inactiveLogin);
        await _host.WithDbAsync(async db => { var user=await db.Users.SingleAsync(x=>x.Id==_activeUserId);user.SetActiveStatus(false);await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized,(await PostWithCookieAsync("/auth/refresh",inactiveCookie)).StatusCode);
        await _host.WithServicesAsync(async services => { var manager=services.GetRequiredService<UserManager<ApplicationUser>>();var user=await manager.FindByIdAsync(_activeUserId.ToString());user!.SetActiveStatus(true);await manager.UpdateAsync(user); });
        var stampLogin=await LoginAsync("ativo@example.com","Passw0rd1");var stampCookie=Cookie(stampLogin);
        await _host.WithServicesAsync(async services => { var manager=services.GetRequiredService<UserManager<ApplicationUser>>();var user=await manager.FindByIdAsync(_activeUserId.ToString());await manager.UpdateSecurityStampAsync(user!); });
        Assert.Equal(HttpStatusCode.Unauthorized,(await PostWithCookieAsync("/auth/refresh",stampCookie)).StatusCode);
    }

    [Fact]
    public async Task Issued_token_carries_the_configured_issuer_audience_and_subject()
    {
        var response = await LoginAsync("ativo@example.com", "Passw0rd1");
        var body = await response.Content.ReadFromJsonAsync<Login.Response>();

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(body!.AccessToken);

        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal(Audience, Assert.Single(token.Audiences));
        Assert.Equal(_activeUserId.ToString(), token.Subject);
        Assert.Equal("ativo@example.com",
            token.Claims.Single(claim => claim.Type == "email").Value);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public async Task Email_is_trimmed_and_matched_case_insensitively()
    {
        var response = await LoginAsync("  ATIVO@EXAMPLE.COM  ", "Passw0rd1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_does_not_require_a_token_of_its_own()
    {
        // The handler is mapped without RequireAuthorization; an anonymous
        // client must reach the handler rather than being challenged.
        var response = await LoginAsync("ativo@example.com", "Passw0rd1");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_user_deactivated_after_registration_can_no_longer_log_in()
    {
        Assert.Equal(HttpStatusCode.OK,
            (await LoginAsync("ativo@example.com", "Passw0rd1")).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var user = await db.Set<ApplicationUser>()
                .SingleAsync(item => item.Id == _activeUserId);
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await LoginAsync("ativo@example.com", "Passw0rd1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task First_login_requires_password_change_and_returns_no_token()
    {
        await CreateTemporaryUserAsync("primeiro-login@example.com");
        var response = await LoginAsync(
            "primeiro-login@example.com",
            "Temporaria1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<Login.PasswordChangeRequiredResponse>();
        Assert.True(body!.RequiresPasswordChange);
        Assert.Equal("primeiro-login@example.com", body.Email);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Temporary_password_can_be_changed_then_normal_login_succeeds()
    {
        var temporaryUserId = await CreateTemporaryUserAsync(
            "primeiro-change@example.com");
        var change = await _client.PostAsJsonAsync(
            "/auth/change-temporary-password",
            new
            {
                email = "primeiro-change@example.com",
                temporaryPassword = "Temporaria1",
                newPassword = "NovaSenha2",
                confirmation = "NovaSenha2"
            });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync("primeiro-change@example.com", "Temporaria1")).StatusCode);

        var login = await LoginAsync("primeiro-change@example.com", "NovaSenha2");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<Login.Response>();
        Assert.False(body!.RequiresPasswordChange);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        await _host.WithDbAsync(async db =>
        {
            var user = await db.Set<ApplicationUser>()
                .SingleAsync(item => item.Id == temporaryUserId);
            Assert.False(user.MustChangePassword);
            Assert.NotNull(user.PasswordChangedAt);
            Assert.NotNull(user.LastLoginAt);
        });
    }

    [Fact]
    public async Task Invalid_temporary_password_is_rejected()
    {
        await CreateTemporaryUserAsync("primeiro-invalid@example.com");
        var response = await _client.PostAsJsonAsync(
            "/auth/change-temporary-password",
            new
            {
                email = "primeiro-invalid@example.com",
                temporaryPassword = "Errada123",
                newPassword = "NovaSenha2",
                confirmation = "NovaSenha2"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_change_a_temporary_password()
    {
        await CreateTemporaryUserAsync(
            "primeiro-inactive@example.com",
            isActive: false);
        var response = await _client.PostAsJsonAsync(
            "/auth/change-temporary-password",
            new
            {
                email = "primeiro-inactive@example.com",
                temporaryPassword = "Temporaria1",
                newPassword = "NovaSenha2",
                confirmation = "NovaSenha2"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> LoginAsync(
        string? email,
        string? password) =>
        _client.PostAsJsonAsync("/auth/login", new { email, password });

    private async Task<HttpResponseMessage> PostWithCookieAsync(string path,string cookie)
    { using var request=new HttpRequestMessage(HttpMethod.Post,path);request.Headers.Add("Cookie",$"{AuthenticationSessionService.CookieName}={cookie}");return await _client.SendAsync(request); }
    private static string Cookie(HttpResponseMessage response)
    { var header=response.Headers.GetValues("Set-Cookie").Single();return header.Split(';')[0].Split('=',2)[1]; }

    private async Task<Guid> CreateTemporaryUserAsync(
        string email,
        bool isActive = true)
    {
        var id = Guid.Empty;
        await _host.WithServicesAsync(async services =>
        {
            var userManager = services
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser("Primeiro Acesso", email, null);
            user.RequirePasswordChange();
            if (!isActive) user.SetActiveStatus(false);
            Assert.True((await userManager.CreateAsync(
                user,
                "Temporaria1")).Succeeded);
            id = user.Id;
        });
        return id;
    }
}
