using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class WhatsAppWebhookEndpointsTests : IAsyncLifetime
{
    private const string AppSecret = "test-app-secret";
    private const string VerifyToken = "test-verify-token";
    private CoreEndpointTestHost _host = null!;
    private FakeWhatsAppClient _fake = null!;
    private Guid _userId;
    private Guid _condominiumId;
    private Guid _unitId;

    public async Task InitializeAsync()
    {
        _fake = new FakeWhatsAppClient();
        _host = await CoreEndpointTestHost.StartAsync(
            app => app.MapWhatsAppWebhook(),
            builder =>
            {
                builder.Services.Configure<WhatsAppOptions>(settings =>
                {
                    settings.Enabled = true;
                    settings.AppSecret = AppSecret;
                    settings.VerifyToken = VerifyToken;
                    settings.SessionExpirationMinutes = 30;
                });
                builder.Services.AddSingleton<IWhatsAppClient>(_fake);
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<WhatsAppConversationService>();
            });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Teste", null, null);
            var user = CoreTestSeed.User("Maria Silva", "maria@example.com");
            user.Update("Maria Silva", "(11) 99999-0001");
            var unit = new Unit(condominium.Id, "101", null, null, null);
            var unitMembership = new UnitMembership(
                user.Id, unit.Id, UnitRelationshipType.Owner, true, true);
            db.AddRange(condominium, user, unit, unitMembership);
            CoreTestSeed.AddMember(db, user.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();
            _userId = user.Id;
            _condominiumId = condominium.Id;
            _unitId = unit.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Verification_returns_challenge_only_for_the_correct_token()
    {
        var client = _host.AnonymousClient();
        var accepted = await client.GetAsync(
            "/webhooks/whatsapp?hub.mode=subscribe"
            + $"&hub.verify_token={VerifyToken}&hub.challenge=12345");
        var rejected = await client.GetAsync(
            "/webhooks/whatsapp?hub.mode=subscribe"
            + "&hub.verify_token=wrong&hub.challenge=12345");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("12345", await accepted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.DoesNotContain(VerifyToken, await rejected.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Invalid_or_missing_signature_is_rejected(
        bool includeSignature,
        bool alterBody)
    {
        var body = TextPayload("wamid.invalid", "Menu");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/webhooks/whatsapp")
        {
            Content = new StringContent(alterBody ? body + " " : body, Encoding.UTF8, "application/json")
        };
        if (includeSignature)
            request.Headers.Add("X-Hub-Signature-256", Signature(body));

        var response = await _host.AnonymousClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_fake.Messages);
    }

    [Fact]
    public async Task Invalid_json_with_valid_signature_returns_bad_request()
    {
        var response = await PostAsync("{", signatureBody: "{");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Signature_validation_uses_raw_utf8_payload_bytes()
    {
        var body = Encoding.UTF8.GetBytes("""{"text":"á"}""");
        var signature = "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(AppSecret),
                body)).ToLowerInvariant();

        Assert.True(WhatsAppWebhookEndpoints.ValidateSignature(
            body, signature, AppSecret));
    }

    [Theory]
    [InlineData("SHA256=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256=ABCDEF0000000000000000000000000000000000000000000000000000000000")]
    public void Signature_validation_rejects_non_Meta_header_format(
        string signature)
    {
        Assert.False(WhatsAppWebhookEndpoints.ValidateSignature(
            [], signature, AppSecret));
    }

    [Fact]
    public async Task Status_event_without_messages_is_acknowledged_and_ignored()
    {
        var body = """{"entry":[{"changes":[{"value":{"statuses":[{"id":"out-1"}]}}]}]}""";
        var response = await PostAsync(body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_fake.Messages);
    }

    [Fact]
    public async Task Known_phone_receives_menu_and_persists_session_and_audit()
    {
        var response = await PostAsync(TextPayload("wamid.known", "Menu"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(_fake.Messages);
        Assert.Contains("Olá, Maria!", sent.Text);
        Assert.Contains("1 - Abrir uma solicitação", sent.Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(_userId, session.UserId);
            Assert.Equal(_condominiumId, session.CondominiumId);
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            var inbound = await db.WhatsAppInboundMessages.SingleAsync();
            Assert.Equal("main_menu", inbound.ProcessingResult);
            Assert.NotNull(inbound.ProcessedAt);
            Assert.False((await db.Users.SingleAsync(
                user => user.Id == _userId)).PhoneNumberConfirmed);
        });
    }

    [Fact]
    public async Task Duplicate_message_is_acknowledged_without_duplicate_reply_or_rows()
    {
        var body = TextPayload("wamid.duplicate", "Menu");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(body)).StatusCode);

        Assert.Single(_fake.Messages);
        Assert.Equal(1, await _host.WithDbAsync(db =>
            db.WhatsAppInboundMessages.CountAsync()));
        Assert.Equal(1, await _host.WithDbAsync(db =>
            db.WhatsAppSessions.CountAsync()));
    }

    [Fact]
    public async Task New_session_with_oi_sends_main_menu_in_the_same_interaction()
    {
        var response = await PostAsync(TextPayload("wamid.first-oi", "Oi"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(_fake.Messages);
        Assert.Contains("Olá, Maria!", sent.Text);
        Assert.Contains("Como posso ajudar", sent.Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Equal("main_menu", (await db.WhatsAppInboundMessages.SingleAsync()).ProcessingResult);
        });
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public async Task Unavailable_menu_options_do_not_advance_the_session(string option)
    {
        await PostAsync(TextPayload("wamid.unavailable-menu", "Menu"));
        await PostAsync(TextPayload($"wamid.unavailable-{option}", option));

        Assert.Contains("disponível em breve", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Unknown_phone_gets_closed_guidance_without_creating_a_user()
    {
        var response = await PostAsync(
            TextPayload("wamid.unknown", "Menu", "5511988887777"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Não consegui identificar", Assert.Single(_fake.Messages).Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Multiple_residential_contexts_are_rejected_without_disclosure()
    {
        await _host.WithDbAsync(async db =>
        {
            var current = await db.UnitMemberships.SingleAsync(x => x.UnitId == _unitId);
            current.Update(UnitRelationshipType.Owner, true, false);
            var second = new Condominium("Condomínio B", null, null);
            var unit = new Unit(second.Id, "202", null, null, null);
            db.AddRange(second, unit, new UnitMembership(
                _userId, unit.Id, UnitRelationshipType.Tenant, true, false));
            CoreTestSeed.AddMember(db, _userId, second.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.multi", "Menu"));

        var text = Assert.Single(_fake.Messages).Text;
        Assert.Contains("Não consegui identificar seu cadastro", text);
        Assert.DoesNotContain("Residencial Teste", text);
        Assert.DoesNotContain("Condomínio B", text);
        Assert.Equal(WhatsAppConversationState.UnknownPhone,
            await _host.WithDbAsync(db => db.WhatsAppSessions
                .Select(item => item.State).SingleAsync()));
    }

    [Fact]
    public async Task Unique_primary_residence_resolves_multiple_residential_links()
    {
        await _host.WithDbAsync(async db =>
        {
            var second = new Condominium("Condomínio B", null, null);
            var unit = new Unit(second.Id, "202", null, null, null);
            db.AddRange(second, unit, new UnitMembership(
                _userId, unit.Id, UnitRelationshipType.Tenant, true, false));
            CoreTestSeed.AddMember(db, _userId, second.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.primary-context", "Oi"));

        Assert.Contains("Como posso ajudar", Assert.Single(_fake.Messages).Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Equal(_unitId, session.UnitId);
            Assert.Equal(_condominiumId, session.CondominiumId);
        });
    }

    [Fact]
    public async Task Manager_and_platform_admin_with_residential_link_receives_menu()
    {
        await _host.WithDbAsync(async db =>
        {
            var membership = await db.CondominiumMemberships.SingleAsync(x =>
                x.UserId == _userId && x.CondominiumId == _condominiumId);
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(
                membership.Id, CondominiumRole.Manager));
            var role = new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole)
            {
                Id = Guid.NewGuid(),
                NormalizedName = DependencyInjection.PlatformAdminRole.ToUpperInvariant()
            };
            db.Roles.Add(role);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _userId, RoleId = role.Id });
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.resident-admin", "Oi"));

        Assert.Contains("Como posso ajudar", Assert.Single(_fake.Messages).Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Duplicate_canonical_phone_is_rejected_by_the_database()
    {
        await _host.WithDbAsync(async db =>
        {
            var second = CoreTestSeed.User("Outra Pessoa", "outra@example.com");
            second.Update("Outra Pessoa", "+55 11 99999-0001");
            db.Users.Add(second);
            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Inactive_user_is_not_identified()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.inactive", "Menu"));

        Assert.Contains("Não consegui identificar", Assert.Single(_fake.Messages).Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WhatsAppConversationState.UnknownPhone,
            await _host.WithDbAsync(db => db.WhatsAppSessions
                .Select(item => item.State).SingleAsync()));
    }

    [Fact]
    public async Task Inactive_residential_membership_is_not_identified()
    {
        await _host.WithDbAsync(async db =>
        {
            var membership = await db.UnitMemberships.SingleAsync(x => x.UnitId == _unitId);
            membership.End(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.inactive-unit-membership", "Menu"));

        Assert.Contains("Não consegui identificar", Assert.Single(_fake.Messages).Text);
    }

    [Fact]
    public async Task Inactive_unit_or_condominium_is_not_identified()
    {
        await _host.WithDbAsync(db => db.Units.Where(x => x.Id == _unitId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false)));
        await PostAsync(TextPayload("wamid.inactive-unit", "Menu"));
        Assert.Contains("Não consegui identificar", _fake.Messages.Last().Text);

        await _host.WithDbAsync(async db =>
        {
            await db.Units.Where(x => x.Id == _unitId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, true));
            var condominium = await db.Condominiums.SingleAsync(x => x.Id == _condominiumId);
            condominium.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });
        await PostAsync(TextPayload("wamid.inactive-condominium", "Menu"));
        Assert.Contains("Não consegui identificar", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Description_can_be_corrected_and_cancelled_without_creating_a_request()
    {
        await PostAsync(TextPayload("wamid.edit-1", "Menu"));
        await PostAsync(TextPayload("wamid.edit-2", "1"));
        await PostAsync(TextPayload("wamid.edit-3", "Descrição antiga"));
        await PostAsync(TextPayload("wamid.edit-4", "2"));
        await PostAsync(TextPayload("wamid.edit-5", "Descrição corrigida"));
        await PostAsync(TextPayload("wamid.edit-6", "3"));

        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Null(session.DraftDescription);
            Assert.Empty(await db.Requests.ToArrayAsync());
        });
    }

    [Fact]
    public async Task Global_menu_and_exit_restart_then_end_the_session()
    {
        await PostAsync(TextPayload("wamid.help", "Menu"));
        await PostAsync(TextPayload("wamid.exit", "Sair"));

        Assert.Equal(2, _fake.Messages.Count);
        Assert.Contains("Como posso ajudar", _fake.Messages[0].Text);
        Assert.Contains("encerrado", _fake.Messages[1].Text);
        Assert.Equal(WhatsAppConversationState.Ended,
            await _host.WithDbAsync(db => db.WhatsAppSessions
                .Select(item => item.State).SingleAsync()));
    }

    [Fact]
    public async Task Expired_session_restarts_with_an_explicit_explanation()
    {
        await PostAsync(TextPayload("wamid.before-expiry", "Menu"));
        await _host.WithDbAsync<int>(db => db.WhatsAppSessions.ExecuteUpdateAsync(
            setters => setters.SetProperty(
                item => item.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        await PostAsync(TextPayload("wamid.after-expiry", "qualquer coisa"));

        Assert.Contains("Como posso ajudar", _fake.Messages[1].Text);
    }

    [Fact]
    public async Task Provider_send_failure_does_not_lose_the_audited_event()
    {
        _fake.Fail = true;
        var response = await PostAsync(TextPayload("wamid.send-failure", "Menu"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await _host.WithDbAsync(db =>
            db.WhatsAppInboundMessages.AnyAsync(item =>
                item.ExternalMessageId == "wamid.send-failure"
                && item.ProcessedAt != null)));
    }

    [Fact]
    public async Task Complete_text_flow_creates_request_timeline_and_whatsapp_reply()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(
                _condominiumId, "Manutenção", null));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.flow-1", "Menu"));
        await PostAsync(TextPayload("wamid.flow-2", "1"));
        await PostAsync(TextPayload("wamid.flow-3", "Lâmpada queimada no corredor"));
        await PostAsync(TextPayload("wamid.flow-4", "1"));
        await PostAsync(TextPayload("wamid.flow-4", "1"));

        var requestId = await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            Assert.Equal(RequestSource.WhatsApp, request.Source);
            Assert.Equal(RequestPriority.Normal, request.Priority);
            Assert.Equal("Lâmpada queimada no corredor", request.Description);
            Assert.Equal(_unitId, request.TargetUnitId);
            Assert.True(await db.RequestStatusHistories.AnyAsync(item =>
                item.RequestId == request.Id
                && item.NewStatus == RequestStatus.Open));
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Null(session.DraftDescription);
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            return request.Id;
        });

        Assert.Contains(requestId.ToString("N")[..8].ToUpperInvariant(), _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Non_text_message_does_not_enter_attachment_flow_in_this_batch()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_condominiumId, "Segurança", null));
            await db.SaveChangesAsync();
        });
        _fake.Media = new WhatsAppMediaResult(
            true, [0xFF, 0xD8, 0xFF, 0xD9], "image/jpeg", null);
        await PostAsync(TextPayload("wamid.media-1", "Menu"));
        await PostAsync(TextPayload("wamid.media-2", "1"));
        await PostAsync(MediaPayload("wamid.media-3", "media-id-1", "image", "image/jpeg"));

        Assert.Contains("Descreva", _fake.Messages.Last().Text);
        Assert.Empty(await _host.WithDbAsync(db =>
            db.WhatsAppDraftAttachments.AsNoTracking().ToArrayAsync()));
    }

    private async Task<HttpResponseMessage> PostAsync(
        string body,
        string? signatureBody = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/webhooks/whatsapp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", Signature(signatureBody ?? body));
        return await _host.AnonymousClient().SendAsync(request);
    }

    private static string Signature(string body) =>
        "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(AppSecret),
                Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private static string TextPayload(
        string id,
        string text,
        string phone = "5511999990001") =>
        JsonSerializer.Serialize(new
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
                                        from = phone,
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

    private static string MediaPayload(
        string id,
        string mediaId,
        string type,
        string mimeType) =>
        JsonSerializer.Serialize(new
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
                                messages = new object[]
                                {
                                    new
                                    {
                                        from = "5511999990001",
                                        id,
                                        timestamp = "1785236400",
                                        type,
                                        image = new
                                        {
                                            id = mediaId,
                                            mime_type = mimeType
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

    private sealed class FakeWhatsAppClient : IWhatsAppClient
    {
        public List<(string Phone, string Text)> Messages { get; } = [];
        public bool Fail { get; set; }
        public WhatsAppMediaResult Media { get; set; } =
            new(false, null, null, "No media configured.");

        public Task<WhatsAppSendResult> SendTextAsync(
            string phoneNumber,
            string text,
            CancellationToken cancellationToken)
        {
            Messages.Add((phoneNumber, text));
            return Task.FromResult(Fail
                ? new WhatsAppSendResult(false, null, "simulated")
                : new WhatsAppSendResult(true, Guid.NewGuid().ToString(), null));
        }

        public Task<WhatsAppMediaResult> DownloadMediaAsync(
            string mediaId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Media);

        public Task<WhatsAppSendResult> SendTemplateAsync(
            string phoneNumber,
            string templateName,
            string language,
            CancellationToken cancellationToken) =>
            SendTextAsync(phoneNumber, $"template:{templateName}:{language}",
                cancellationToken);
    }
}
