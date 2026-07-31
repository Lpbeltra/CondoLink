using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class WhatsAppLoginEndpointsTests : IAsyncLifetime
{
    private const string Code = "123456";
    private const string Phone = "+5544999999999";
    private const string GenericMessage =
        "Se o telefone estiver apto para login, enviaremos um código pelo WhatsApp.";
    private const string Issuer = "whatsapp-login-tests";
    private const string Audience = "whatsapp-login-tests-audience";
    private const string SigningKey =
        "whatsapp-login-test-key-with-at-least-32-bytes";

    private CoreEndpointTestHost _host = null!;
    private HttpClient _client = null!;
    private MutableTimeProvider _time = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        _host = await CoreEndpointTestHost.StartAsync(
            app =>
            {
                app.MapLogin();
                app.MapWhatsAppLogin();
            },
            builder =>
            {
                builder.Configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:Issuer"] = Issuer,
                        ["Jwt:Audience"] = Audience,
                        ["Jwt:Key"] = SigningKey,
                        ["Jwt:ExpirationMinutes"] = "60"
                    });
                builder.Services.Configure<WhatsAppOptions>(settings =>
                {
                    settings.Enabled = true;
                    settings.OutboundWorkerEnabled = true;
                    settings.PhoneNumberId = "test-phone-id";
                    settings.AccessToken = "test-access-token";
                });
                builder.Services.AddSingleton<TimeProvider>(_time);
                builder.Services.AddSingleton<IPhoneVerificationCodeGenerator>(
                    new FixedCodeGenerator());
                builder.Services.AddSingleton<IPhoneVerificationMessageProtector>(
                    new TestMessageProtector());
                builder.Services.AddScoped<WhatsAppLoginService>();
            });
        _client = _host.AnonymousClient();

        await _host.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<
                UserManager<ApplicationUser>>();
            var roles = services.GetRequiredService<
                RoleManager<IdentityRole<Guid>>>();
            await roles.CreateAsync(new IdentityRole<Guid>(
                DependencyInjection.PlatformAdminRole));

            var user = new ApplicationUser(
                "Login WhatsApp", "whatsapp-login@example.com", Phone);
            user.ConfirmPhoneNumber();
            Assert.True((await users.CreateAsync(user, "Passw0rd1")).Succeeded);
            await users.AddToRoleAsync(
                user, DependencyInjection.PlatformAdminRole);
            _userId = user.Id;

            var unverified = new ApplicationUser(
                "Não Verificado", "unverified@example.com",
                "+5544888888888");
            Assert.True((await users.CreateAsync(
                unverified, "Passw0rd1")).Succeeded);

            var inactive = new ApplicationUser(
                "Inativo", "inactive-whatsapp@example.com",
                "+5544777777777");
            inactive.ConfirmPhoneNumber();
            inactive.SetActiveStatus(false);
            Assert.True((await users.CreateAsync(
                inactive, "Passw0rd1")).Succeeded);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Verified_active_phone_queues_hashed_login_challenge()
    {
        var response = await RequestCodeAsync("(44) 99999-9999");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(GenericMessage, await response.Content.ReadAsStringAsync());
        await _host.WithDbAsync(async db =>
        {
            var challenge = await db.WhatsAppPhoneVerifications.SingleAsync();
            Assert.Equal(WhatsAppChallengePurpose.Login, challenge.Purpose);
            Assert.False(Encoding.UTF8.GetBytes(Code)
                .SequenceEqual(challenge.CodeHash));
            Assert.Equal(
                TimeSpan.FromMinutes(10),
                challenge.ExpiresAt - challenge.CreatedAt);
            Assert.Equal(5, challenge.MaximumAttempts);
            var outbound = await db.WhatsAppOutboundMessages.SingleAsync();
            Assert.Equal(
                WhatsAppNotificationType.LoginCode,
                outbound.NotificationType);
            Assert.DoesNotContain(Code, outbound.Content);
        });
    }

    [Theory]
    [InlineData("+5544666666666")]
    [InlineData("+5544888888888")]
    [InlineData("+5544777777777")]
    [InlineData("telefone inválido")]
    public async Task Ineligible_phones_return_same_generic_response(
        string phone)
    {
        var response = await RequestCodeAsync(phone);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(GenericMessage, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await _host.WithDbAsync(db =>
            db.WhatsAppPhoneVerifications.CountAsync()));
    }

    [Fact]
    public async Task Disabled_WhatsApp_returns_functional_unavailable_error()
    {
        await _host.WithServicesAsync(services =>
        {
            services.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>()
                .Value.Enabled = false;
            return Task.CompletedTask;
        });

        var response = await RequestCodeAsync(Phone);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("whatsapp_unavailable",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cooldown_and_hourly_limit_do_not_queue_extra_challenges()
    {
        await RequestCodeAsync(Phone);
        await RequestCodeAsync(Phone);
        Assert.Equal(1, await ChallengeCountAsync());

        _time.Advance(TimeSpan.FromSeconds(61));
        await RequestCodeAsync(Phone);
        _time.Advance(TimeSpan.FromSeconds(61));
        await RequestCodeAsync(Phone);
        _time.Advance(TimeSpan.FromSeconds(61));
        await RequestCodeAsync(Phone);

        Assert.Equal(3, await ChallengeCountAsync());
        await _host.WithDbAsync(async db =>
        {
            var challenges = await db.WhatsAppPhoneVerifications
                .OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.All(challenges[..^1],
                challenge => Assert.NotNull(challenge.InvalidatedAt));
            Assert.Null(challenges[^1].InvalidatedAt);
        });
    }

    [Fact]
    public async Task Correct_code_returns_same_session_shape_and_claims_as_password()
    {
        await RequestCodeAsync("44999999999");
        var whatsapp = await ConfirmAsync("(44) 99999-9999", Code);
        var password = await _client.PostAsJsonAsync(
            "/auth/login",
            new { email = "whatsapp-login@example.com", password = "Passw0rd1" });

        Assert.Equal(HttpStatusCode.OK, whatsapp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, password.StatusCode);
        var whatsappSession = await whatsapp.Content
            .ReadFromJsonAsync<Login.Response>();
        var passwordSession = await password.Content
            .ReadFromJsonAsync<Login.Response>();
        Assert.Equal(passwordSession!.TokenType, whatsappSession!.TokenType);
        Assert.Equal(passwordSession.ExpiresIn, whatsappSession.ExpiresIn);
        Assert.Equal(passwordSession.User.Id, whatsappSession.User.Id);
        Assert.Equal(passwordSession.User.FullName, whatsappSession.User.FullName);
        Assert.Equal(passwordSession.User.Email, whatsappSession.User.Email);
        Assert.Equal(passwordSession.User.IsActive, whatsappSession.User.IsActive);
        Assert.Equal(passwordSession.User.Roles, whatsappSession.User.Roles);

        var whatsappToken = new JwtSecurityTokenHandler()
            .ReadJwtToken(whatsappSession.AccessToken);
        var passwordToken = new JwtSecurityTokenHandler()
            .ReadJwtToken(passwordSession.AccessToken);
        Assert.Equal(passwordToken.Issuer, whatsappToken.Issuer);
        Assert.Equal(passwordToken.Audiences, whatsappToken.Audiences);
        Assert.Equal(passwordToken.Subject, whatsappToken.Subject);
        Assert.Equal(
            passwordToken.Claims
                .Where(x => x.Type is "email"
                    or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                .Select(x => (x.Type, x.Value))
                .OrderBy(x => x.Type),
            whatsappToken.Claims
                .Where(x => x.Type is "email"
                    or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                .Select(x => (x.Type, x.Value))
                .OrderBy(x => x.Type));
    }

    [Fact]
    public async Task Incorrect_code_counts_attempts_and_exhausts_challenge()
    {
        await RequestCodeAsync(Phone);
        for (var attempt = 1; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await ConfirmAsync(Phone, "000000")).StatusCode);

        var exhausted = await ConfirmAsync(Phone, "000000");

        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            var challenge = await db.WhatsAppPhoneVerifications.SingleAsync();
            Assert.Equal(5, challenge.AttemptCount);
            Assert.NotNull(challenge.InvalidatedAt);
        });
    }

    [Fact]
    public async Task Expired_and_consumed_codes_cannot_issue_sessions()
    {
        await RequestCodeAsync(Phone);
        _time.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(HttpStatusCode.Gone,
            (await ConfirmAsync(Phone, Code)).StatusCode);

        _time.Advance(TimeSpan.FromSeconds(61));
        await RequestCodeAsync(Phone);
        Assert.Equal(HttpStatusCode.OK,
            (await ConfirmAsync(Phone, Code)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await ConfirmAsync(Phone, Code)).StatusCode);
    }

    [Fact]
    public async Task Phone_verification_challenge_cannot_be_used_for_login()
    {
        var (hash, salt) = PhoneVerificationCodeHasher.Hash(Code);
        await _host.WithDbAsync(async db =>
        {
            db.WhatsAppPhoneVerifications.Add(
                new WhatsAppPhoneVerification(
                    _userId,
                    Phone,
                    hash,
                    salt,
                    _time.GetUtcNow().UtcDateTime,
                    _time.GetUtcNow().UtcDateTime.AddMinutes(10),
                    5,
                    WhatsAppChallengePurpose.PhoneVerification));
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ConfirmAsync(Phone, Code)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_confirmation_consumes_challenge_once()
    {
        await RequestCodeAsync(Phone);

        var responses = await Task.WhenAll(
            ConfirmAsync(Phone, Code),
            ConfirmAsync(Phone, Code));

        Assert.Equal(1, responses.Count(
            response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(
            response => response.StatusCode == HttpStatusCode.Conflict));
    }

    private Task<HttpResponseMessage> RequestCodeAsync(string phoneNumber) =>
        _client.PostAsJsonAsync(
            "/auth/whatsapp/request-code",
            new { phoneNumber });

    private Task<HttpResponseMessage> ConfirmAsync(
        string phoneNumber,
        string code) =>
        _client.PostAsJsonAsync(
            "/auth/whatsapp/confirm",
            new { phoneNumber, code });

    private Task<int> ChallengeCountAsync() =>
        _host.WithDbAsync(db => db.WhatsAppPhoneVerifications
            .CountAsync(x => x.Purpose == WhatsAppChallengePurpose.Login));

    private sealed class FixedCodeGenerator
        : IPhoneVerificationCodeGenerator
    {
        public string Generate() => Code;
    }

    private sealed class TestMessageProtector
        : IPhoneVerificationMessageProtector
    {
        public string Protect(string message) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(message));

        public string Unprotect(string protectedMessage) =>
            Encoding.UTF8.GetString(
                Convert.FromBase64String(protectedMessage));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }
}
