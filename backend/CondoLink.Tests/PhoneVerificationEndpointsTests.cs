using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.Users;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class PhoneVerificationEndpointsTests : IAsyncLifetime
{
    private const string Code = "123456";
    private const string AppSecret = "verification-test-secret";
    private CoreEndpointTestHost _host = null!;
    private MutableTimeProvider _time = null!;
    private FakeWhatsAppClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        _client = new FakeWhatsAppClient();
        _host = await CoreEndpointTestHost.StartAsync(
            app =>
            {
                app.MapPhoneVerification();
                app.MapWhatsAppWebhook();
            },
            builder =>
            {
                builder.Services.Configure<WhatsAppOptions>(settings =>
                {
                    settings.Enabled = true;
                    settings.OutboundWorkerEnabled = true;
                    settings.AppSecret = AppSecret;
                });
                builder.Services.AddSingleton<TimeProvider>(_time);
                builder.Services.AddSingleton<IPhoneVerificationCodeGenerator>(
                    new FixedCodeGenerator());
                builder.Services.AddSingleton<IPhoneVerificationMessageProtector>(
                    new TestMessageProtector());
                builder.Services.AddSingleton<IWhatsAppClient>(_client);
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<WhatsAppPhoneVerificationService>();
                builder.Services.AddScoped<WhatsAppConversationService>();
            });
        await _host.WithDbAsync(async db =>
        {
            var user = CoreTestSeed.User(
                "Pessoa Verificação", "phone-verification@example.com");
            user.Update("Pessoa Verificação", "(11) 99999-0001");
            db.Users.Add(user);
            db.WhatsAppInboundMessages.Add(new WhatsAppInboundMessage(
                "window-seed", "+5511999990001", "text", "Olá",
                _time.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
            _userId = user.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Start_creates_hashed_challenge_and_protected_queue_message()
    {
        var response = await StartAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Code, body);
        await _host.WithDbAsync(async db =>
        {
            var challenge = await db.WhatsAppPhoneVerifications.SingleAsync();
            Assert.False(Encoding.UTF8.GetBytes(Code)
                .SequenceEqual(challenge.CodeHash));
            Assert.NotEmpty(challenge.CodeSalt);
            Assert.Equal(5, challenge.MaximumAttempts);
            Assert.Equal(
                WhatsAppChallengePurpose.PhoneVerification,
                challenge.Purpose);
            Assert.Equal(
                TimeSpan.FromMinutes(10),
                challenge.ExpiresAt - challenge.CreatedAt);
            var outbound = await db.WhatsAppOutboundMessages.SingleAsync();
            Assert.Equal(WhatsAppNotificationType.PhoneVerification,
                outbound.NotificationType);
            Assert.DoesNotContain(Code, outbound.Content);
            Assert.Null(outbound.RequestId);
            Assert.Null(outbound.CondominiumId);
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
        });

        var status = await _host.ClientFor(_userId).GetAsync(
            "/users/me/phone-verification");
        var statusBody = await status.Content.ReadAsStringAsync();
        Assert.Contains("\"activeChallenge\":true", statusBody);
        Assert.DoesNotContain(Code, statusBody);
        Assert.DoesNotContain("codeHash", statusBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_challenge_does_not_block_phone_verification_challenge()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            var (hash, salt) = PhoneVerificationCodeHasher.Hash("654321");
            var now = _time.GetUtcNow().UtcDateTime;
            db.WhatsAppPhoneVerifications.Add(
                new WhatsAppPhoneVerification(
                    user.Id,
                    user.NormalizedPhoneNumber!,
                    hash,
                    salt,
                    now,
                    now.AddMinutes(10),
                    5,
                    WhatsAppChallengePurpose.Login));
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Accepted,
            (await StartAsync()).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var purposes = await db.WhatsAppPhoneVerifications
                .OrderBy(x => x.Purpose)
                .Select(x => x.Purpose)
                .ToArrayAsync();
            Assert.Equal(
                [
                    WhatsAppChallengePurpose.PhoneVerification,
                    WhatsAppChallengePurpose.Login
                ],
                purposes);
        });
    }

    [Fact]
    public async Task Missing_phone_inactive_user_and_confirmed_phone_are_safe()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update(user.FullName, null);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await StartAsync()).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update(user.FullName, "(11) 99999-0001");
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden,
            (await StartAsync()).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.SetActiveStatus(true);
            user.ConfirmPhoneNumber();
            await db.SaveChangesAsync();
        });
        var confirmed = await StartAsync();
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Contains("already_confirmed",
            await confirmed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Resend_interval_is_enforced_then_previous_challenge_is_invalidated()
    {
        Assert.Equal(HttpStatusCode.Accepted,
            (await StartAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await StartAsync()).StatusCode);

        _time.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(HttpStatusCode.Accepted,
            (await StartAsync()).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var challenges = await db.WhatsAppPhoneVerifications
                .OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(2, challenges.Length);
            Assert.NotNull(challenges[0].InvalidatedAt);
            Assert.Null(challenges[1].InvalidatedAt);
        });
    }

    [Fact]
    public async Task Correct_code_confirms_phone_without_entering_conversation()
    {
        await StartAsync();

        var response = await PostWebhookAsync(Code, "verify-correct");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("confirmado com sucesso",
            Assert.Single(_client.Messages).Text);
        await _host.WithDbAsync(async db =>
        {
            Assert.True((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
            Assert.NotNull((await db.WhatsAppPhoneVerifications
                .SingleAsync()).ConfirmedAt);
            Assert.Empty(db.WhatsAppSessions);
            Assert.Empty(db.Requests);
            Assert.Equal("phone_verification_confirmed",
                (await db.WhatsAppInboundMessages.SingleAsync(
                    x => x.ExternalMessageId == "verify-correct"))
                .ProcessingResult);
        });
    }

    [Fact]
    public async Task Correct_code_submitted_by_authenticated_user_confirms_phone()
    {
        await StartAsync();

        var response = await ConfirmAsync(_userId, Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"confirmed\"",
            await response.Content.ReadAsStringAsync());
        await _host.WithDbAsync(async db =>
        {
            Assert.True((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
            Assert.NotNull((await db.WhatsAppPhoneVerifications
                .SingleAsync()).ConfirmedAt);
        });
    }

    [Fact]
    public async Task Incorrect_code_is_rejected_and_counts_attempt()
    {
        await StartAsync();

        var response = await ConfirmAsync(_userId, "000000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_code",
            await response.Content.ReadAsStringAsync());
        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(1, (await db.WhatsAppPhoneVerifications
                .SingleAsync()).AttemptCount);
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
        });
    }

    [Fact]
    public async Task Expired_and_used_codes_are_rejected_by_confirmation_endpoint()
    {
        await StartAsync();
        _time.Advance(TimeSpan.FromMinutes(11));
        var expired = await ConfirmAsync(_userId, Code);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Contains("\"status\":\"expired\"",
            await expired.Content.ReadAsStringAsync());

        _time.Advance(TimeSpan.FromSeconds(61));
        await StartAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await ConfirmAsync(_userId, Code)).StatusCode);
        var used = await ConfirmAsync(_userId, Code);
        Assert.Equal(HttpStatusCode.Conflict, used.StatusCode);
        Assert.Contains("\"status\":\"used\"",
            await used.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Confirmation_endpoint_invalidates_code_at_attempt_limit()
    {
        await StartAsync();

        for (var attempt = 1; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest,
                (await ConfirmAsync(_userId, "000000")).StatusCode);
        var exhausted = await ConfirmAsync(_userId, "000000");

        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        Assert.Contains("attempts_exhausted",
            await exhausted.Content.ReadAsStringAsync());
        await _host.WithDbAsync(async db =>
        {
            var challenge = await db.WhatsAppPhoneVerifications.SingleAsync();
            Assert.Equal(5, challenge.AttemptCount);
            Assert.NotNull(challenge.InvalidatedAt);
        });
    }

    [Fact]
    public async Task User_cannot_confirm_another_users_phone()
    {
        await StartAsync();
        var otherUserId = await _host.WithDbAsync(async db =>
        {
            var other = CoreTestSeed.User(
                "Outra Pessoa", "other-phone@example.com");
            other.Update("Outra Pessoa", "(21) 98888-0002");
            db.Users.Add(other);
            await db.SaveChangesAsync();
            return other.Id;
        });

        var response = await ConfirmAsync(otherUserId, Code);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == otherUserId)).PhoneNumberConfirmed);
        });
    }

    [Fact]
    public async Task Incorrect_code_exhausts_attempts_without_confirmation()
    {
        await StartAsync();
        for (var attempt = 1; attempt <= 5; attempt++)
            await PostWebhookAsync("000000", $"verify-wrong-{attempt}");

        Assert.Contains("limite de tentativas",
            _client.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var challenge = await db.WhatsAppPhoneVerifications.SingleAsync();
            Assert.Equal(5, challenge.AttemptCount);
            Assert.NotNull(challenge.InvalidatedAt);
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
        });
    }

    [Fact]
    public async Task Expired_and_used_codes_cannot_be_reused()
    {
        await StartAsync();
        _time.Advance(TimeSpan.FromMinutes(11));
        await PostWebhookAsync(Code, "verify-expired");
        Assert.Contains("expirou", _client.Messages.Last().Text);

        _time.Advance(TimeSpan.FromSeconds(61));
        await StartAsync();
        await PostWebhookAsync(Code, "verify-used-first");
        await PostWebhookAsync(Code, "verify-used-second");
        Assert.Contains("não está mais disponível",
            _client.Messages.Last().Text);
    }

    [Fact]
    public async Task Request_does_not_require_a_prior_WhatsApp_session()
    {
        _time.Advance(TimeSpan.FromHours(25));

        var response = await StartAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await _host.WithDbAsync(db =>
            db.WhatsAppPhoneVerifications.CountAsync()));
    }

    [Fact]
    public async Task Changing_phone_clears_confirmation_and_invalidates_challenge()
    {
        await StartAsync();
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update(user.FullName, "(21) 98888-0002");
            await db.SaveChangesAsync();
        });

        await _host.WithDbAsync(async db =>
        {
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
            Assert.NotNull((await db.WhatsAppPhoneVerifications
                .SingleAsync()).InvalidatedAt);
        });
    }

    [Fact]
    public async Task Hourly_challenge_limit_is_enforced()
    {
        for (var request = 0; request < 3; request++)
        {
            Assert.Equal(HttpStatusCode.Accepted,
                (await StartAsync()).StatusCode);
            _time.Advance(TimeSpan.FromSeconds(61));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await StartAsync()).StatusCode);
        Assert.Equal(3, await _host.WithDbAsync(db =>
            db.WhatsAppPhoneVerifications.CountAsync()));
    }

    [Fact]
    public async Task Enabled_worker_sends_protected_message_through_fake_client()
    {
        await StartAsync();

        await _host.WithServicesAsync(async services =>
        {
            var worker = new WhatsAppOutboundWorker(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IOptions<WhatsAppOptions>>(),
                services.GetRequiredService<ILogger<WhatsAppOutboundWorker>>());
            await worker.ProcessBatch(
                services.GetRequiredService<IOptions<WhatsAppOptions>>().Value,
                CancellationToken.None);
        });

        Assert.Contains(Code, Assert.Single(_client.Messages).Text);
        await _host.WithDbAsync(async db =>
            Assert.Equal(
                WhatsAppOutboundStatus.Sent,
                (await db.WhatsAppOutboundMessages.SingleAsync()).Status));
    }

    [Fact]
    public async Task Provider_send_failure_does_not_confirm_phone()
    {
        await StartAsync();
        _client.Fail = true;

        await _host.WithServicesAsync(async services =>
        {
            var worker = new WhatsAppOutboundWorker(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IOptions<WhatsAppOptions>>(),
                services.GetRequiredService<ILogger<WhatsAppOutboundWorker>>());
            await worker.ProcessBatch(
                services.GetRequiredService<IOptions<WhatsAppOptions>>().Value,
                CancellationToken.None);
        });

        await _host.WithDbAsync(async db =>
        {
            Assert.False((await db.Users.SingleAsync(
                x => x.Id == _userId)).PhoneNumberConfirmed);
            Assert.Equal(
                WhatsAppOutboundStatus.PermanentlyFailed,
                (await db.WhatsAppOutboundMessages.SingleAsync()).Status);
        });
    }

    [Fact]
    public async Task Disabled_integration_does_not_create_or_queue_challenge()
    {
        await _host.WithServicesAsync(services =>
        {
            services.GetRequiredService<IOptions<WhatsAppOptions>>()
                .Value.Enabled = false;
            return Task.CompletedTask;
        });

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await StartAsync()).StatusCode);
        Assert.Equal(0, await _host.WithDbAsync(db =>
            db.WhatsAppPhoneVerifications.CountAsync()));
        Assert.Equal(0, await _host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.CountAsync()));
    }

    private Task<HttpResponseMessage> StartAsync() =>
        _host.ClientFor(_userId).PostAsync(
            "/users/me/phone-verification", null);

    private Task<HttpResponseMessage> ConfirmAsync(Guid userId, string code) =>
        _host.ClientFor(userId).PostAsJsonAsync(
            "/users/me/phone-verification/confirm", new { code });

    private async Task<HttpResponseMessage> PostWebhookAsync(
        string text, string id)
    {
        var body = JsonSerializer.Serialize(new
        {
            entry = new[]
            {
                new
                {
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messages = new[]
                                {
                                    new
                                    {
                                        from = "5511999990001",
                                        id,
                                        timestamp = "1785236400",
                                        type = "text",
                                        text = new { body = text }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/webhooks/whatsapp")
        {
            Content = new StringContent(
                body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(
            "X-Hub-Signature-256",
            "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(AppSecret),
                Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
        return await _host.AnonymousClient().SendAsync(request);
    }

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
            Encoding.UTF8.GetString(Convert.FromBase64String(protectedMessage));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }

    private sealed class FakeWhatsAppClient : IWhatsAppClient
    {
        public List<(string Phone, string Text)> Messages { get; } = [];
        public bool Fail { get; set; }
        public Task<WhatsAppSendResult> SendTextAsync(
            string phoneNumber, string text, CancellationToken cancellationToken)
        {
            Messages.Add((phoneNumber, text));
            return Task.FromResult(Fail
                ? new WhatsAppSendResult(
                    false, null, "simulated", false, "simulated")
                : new WhatsAppSendResult(
                    true, Guid.NewGuid().ToString(), null));
        }
        public Task<WhatsAppSendResult> SendTemplateAsync(
            string phoneNumber, string templateName, string language,
            CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppSendResult(
                true, Guid.NewGuid().ToString(), null));
        public Task<WhatsAppMediaResult> DownloadMediaAsync(
            string mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppMediaResult(
                false, null, null, "not configured"));
    }
}
