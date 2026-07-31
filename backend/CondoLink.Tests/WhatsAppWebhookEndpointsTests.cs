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
    private FakeRequestDraftAiService _ai = null!;
    private Guid _userId;
    private Guid _condominiumId;
    private Guid _unitId;

    public async Task InitializeAsync()
    {
        _fake = new FakeWhatsAppClient();
        _ai = new FakeRequestDraftAiService();
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
                builder.Services.AddSingleton<IRequestDraftAiService>(_ai);
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
    public async Task Legacy_meta_number_matches_registered_brazilian_mobile_with_ninth_digit()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update("Maria Silva", "(44) 99756-2161");
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.legacy-mobile", "Oi", "554497562161"));

        var sent = Assert.Single(_fake.Messages);
        Assert.Equal("+554497562161", sent.Phone);
        Assert.Contains("Como posso ajudar", sent.Text);
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            Assert.Equal("+5544997562161", user.NormalizedPhoneNumber);
            Assert.Equal("(44) 99756-2161", user.PhoneNumber);
            Assert.Equal("+554497562161", (await db.WhatsAppSessions.SingleAsync()).PhoneNumber);
            Assert.Equal("+554497562161", (await db.WhatsAppInboundMessages.SingleAsync()).PhoneNumber);
        });
    }

    [Fact]
    public async Task Brazilian_phone_variants_pointing_to_different_users_are_ambiguous()
    {
        await _host.WithDbAsync(async db =>
        {
            var canonical = await db.Users.SingleAsync(x => x.Id == _userId);
            canonical.Update("Maria Silva", "(44) 99756-2161");
            var legacy = CoreTestSeed.User("Outra Pessoa", "legacy@example.com");
            legacy.Update("Outra Pessoa", "(45) 99999-0002");
            db.Users.Add(legacy);
            await db.SaveChangesAsync();
            await db.Users.Where(x => x.Id == legacy.Id).ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.NormalizedPhoneNumber, "+554497562161"));
        });

        await PostAsync(TextPayload("wamid.ambiguous-variant", "Oi", "554497562161"));

        Assert.Contains("Não consegui identificar", Assert.Single(_fake.Messages).Text);
        Assert.Equal(WhatsAppConversationState.UnknownPhone,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Inactive_user_is_not_resolved_through_brazilian_variant()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update("Maria Silva", "(44) 99756-2161");
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.inactive-variant", "Oi", "554497562161"));

        Assert.Contains("Não consegui identificar", Assert.Single(_fake.Messages).Text);
    }

    [Fact]
    public async Task Old_unknown_phone_session_recovers_when_brazilian_variant_becomes_available()
    {
        await PostAsync(TextPayload("wamid.variant-unknown", "Oi", "554497562161"));
        var sessionId = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Where(x => x.State == WhatsAppConversationState.UnknownPhone)
            .Select(x => x.Id).SingleAsync());
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == _userId);
            user.Update("Maria Silva", "(44) 99756-2161");
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.variant-recovered", "Oi novamente", "554497562161"));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(sessionId, session.Id);
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Equal(_userId, session.UserId);
        });
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
    public async Task Old_unknown_phone_session_recovers_after_context_is_fixed()
    {
        await SetCondominiumMembershipActive(false);
        await PostAsync(TextPayload("wamid.unknown-old", "Oi"));
        var sessionId = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Where(x => x.State == WhatsAppConversationState.UnknownPhone)
            .Select(x => x.Id).SingleAsync());

        await SetCondominiumMembershipActive(true);
        await PostAsync(TextPayload("wamid.unknown-recovered", "Oi novamente"));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(sessionId, session.Id);
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Equal(_userId, session.UserId);
            Assert.Equal(_condominiumId, session.CondominiumId);
            Assert.Equal(_unitId, session.UnitId);
            Assert.Equal(1, await db.WhatsAppSessions.CountAsync());
        });
    }

    [Fact]
    public async Task Unknown_phone_session_stays_unknown_while_context_is_invalid()
    {
        await SetCondominiumMembershipActive(false);
        await PostAsync(TextPayload("wamid.unknown-still-1", "Oi"));
        var previousExpiry = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.ExpiresAt).SingleAsync());

        await PostAsync(TextPayload("wamid.unknown-still-2", "reiniciar"));

        Assert.Contains("Não consegui identificar", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.UnknownPhone, session.State);
            Assert.Null(session.UserId);
            Assert.True(session.ExpiresAt >= previousExpiry);
            Assert.Equal(1, await db.WhatsAppSessions.CountAsync());
        });
    }

    [Fact]
    public async Task Restart_command_recovers_old_unknown_phone_session()
    {
        await SetCondominiumMembershipActive(false);
        await PostAsync(TextPayload("wamid.restart-old", "Oi"));
        await SetCondominiumMembershipActive(true);

        await PostAsync(TextPayload("wamid.restart-recovered", "  REINICIAR  "));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
        Assert.Equal(1, await _host.WithDbAsync(db => db.WhatsAppSessions.CountAsync()));
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
        await PostAsync(TextPayload("wamid.edit-5", "2"));
        await PostAsync(TextPayload("wamid.edit-6", "Descrição corrigida"));
        await PostAsync(TextPayload("wamid.edit-7", "2"));
        Assert.Contains("3 - Cancelar e voltar ao início", _fake.Messages.Last().Text);
        await PostAsync(TextPayload("wamid.edit-8", "3"));

        Assert.StartsWith("A abertura foi cancelada.", _fake.Messages.Last().Text);
        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);

        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Null(session.DraftDescription);
            Assert.Empty(await db.Requests.ToArrayAsync());
        });

        await PostAsync(TextPayload("wamid.edit-9", "1"));
        Assert.Contains("Descreva o que aconteceu em uma só mensagem", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.CollectingDescription,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Global_cancel_abandons_draft_and_keeps_session_at_menu()
    {
        await PostAsync(TextPayload("wamid.global-cancel-1", "Oi"));
        await PostAsync(TextPayload("wamid.global-cancel-2", "1"));
        await PostAsync(TextPayload("wamid.global-cancel-3", "Descrição temporária"));
        await PostAsync(TextPayload("wamid.global-cancel-4", "cancelar"));

        Assert.StartsWith("A abertura foi cancelada.", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Null(session.DraftDescription);
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
        await PostAsync(TextPayload("wamid.flow-4", "2"));
        await PostAsync(TextPayload("wamid.flow-5", "1"));
        await PostAsync(TextPayload("wamid.flow-5", "1"));

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
            Assert.Null(session.CategoryId);
            Assert.Null(session.RequestId);
            Assert.Equal(WhatsAppConversationState.Ended, session.State);
            return request.Id;
        });

        Assert.Contains(requestId.ToString("N")[..8].ToUpperInvariant(), _fake.Messages.Last().Text);
        Assert.Contains("basta chamar novamente", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Digite ‘menu’", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.flow-new-attendance", "Bom dia"));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Para abrir uma solicitação, digite 1", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Requests.CountAsync());
            Assert.Equal(1, await db.WhatsAppSessions.CountAsync());
            Assert.Equal(WhatsAppConversationState.MainMenu,
                await db.WhatsAppSessions.Select(x => x.State).SingleAsync());
        });
    }

    [Fact]
    public async Task Valid_image_is_kept_as_temporary_draft_attachment()
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
        await PostAsync(TextPayload("wamid.media-description", "Portão danificado"));
        await PostAsync(MediaPayload("wamid.media-3", "media-id-1", "image", "image/jpeg"));

        Assert.Contains("Arquivo recebido", _fake.Messages.Last().Text);
        Assert.Single(await _host.WithDbAsync(db =>
            db.WhatsAppDraftAttachments.AsNoTracking().ToArrayAsync()));
    }

    [Fact]
    public async Task Mixed_attachments_are_promoted_only_when_request_is_confirmed()
    {
        await AddCategoryAndStartAttachmentFlow();
        var media = new[]
        {
            ("mix-image-1", "image", "image/jpeg", "foto.jpg"),
            ("mix-image-2", "image", "image/png", "foto.png"),
            ("mix-video", "video", "video/mp4", "video.mp4"),
            ("mix-document", "document", "application/pdf", "documento.pdf")
        };
        foreach (var (id, type, contentType, name) in media)
        {
            _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], contentType, null);
            await PostAsync(MediaPayload($"wamid.{id}", id, type, contentType, name));
            Assert.Contains("Arquivo recebido", _fake.Messages.Last().Text);
        }
        Assert.Equal(4, await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.CountAsync()));

        await PostAsync(TextPayload("wamid.mix-finished", "1"));
        Assert.Contains("Confirmar", _fake.Messages.Last().Text);
        await PostAsync(TextPayload("wamid.mix-confirmed", "1"));

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            Assert.Equal(4, await db.RequestAttachments.CountAsync(x => x.RequestId == request.Id));
            Assert.Empty(await db.WhatsAppDraftAttachments.ToArrayAsync());
            var initial = await db.RequestMessages.SingleAsync(x => x.RequestId == request.Id);
            Assert.Equal(MessageChannel.WhatsApp, initial.Channel);
            Assert.Equal("Portão danificado", initial.Content);
        });
    }

    [Fact]
    public async Task Option_two_preserves_uploaded_files_and_moves_to_review()
    {
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "image/jpeg", null);
        await PostAsync(MediaPayload("wamid.keep-file", "keep-file", "image", "image/jpeg", "foto.jpg"));

        await PostAsync(TextPayload("wamid.no-more-files", "2"));

        Assert.Contains("Confirmar", _fake.Messages.Last().Text);
        Assert.Single(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
    }

    [Fact]
    public async Task Ai_proposal_drives_review_and_preserves_original_report()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Portão da garagem danificado",
            "O portão da garagem está danificado e não fecha corretamente.",
            "Manutenção",
            ["Informe desde quando o problema acontece."],
            0.91), null);
        await AddCategoryAndStartAttachmentFlow();

        await PostAsync(TextPayload("wamid.ai-finished", "2"));

        var review = _fake.Messages.Last().Text;
        Assert.Contains("Título", review);
        Assert.Contains("Portão da garagem danificado", review);
        Assert.Contains("O portão da garagem está danificado", review);
        Assert.Contains("Categoria\n\nManutenção", review);
        Assert.Contains("talvez faltem estas informações", review);
        Assert.Contains("\"Source\":\"ai\"", await _host.WithDbAsync(db =>
            db.WhatsAppSessions.Select(x => x.DraftAiProposalJson).SingleAsync()));
        await PostAsync(TextPayload("wamid.ai-confirmed", "1"));

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            Assert.Equal("Portão da garagem danificado", request.Title);
            Assert.Equal("O portão da garagem está danificado e não fecha corretamente.", request.Description);
            Assert.Equal("Manutenção", await db.Categories.Where(x => x.Id == request.CategoryId)
                .Select(x => x.Name).SingleAsync());
            Assert.Equal("Portão danificado", await db.RequestMessages
                .Where(x => x.RequestId == request.Id).Select(x => x.Content).SingleAsync());
        });
    }

    [Theory]
    [InlineData("Categoria inexistente")]
    [InlineData("Categoria inativa")]
    public async Task Invalid_ai_category_uses_existing_category_flow(string suggestedCategory)
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título organizado", "Descrição organizada", suggestedCategory, [], null), null);
        await AddCategoryAndStartAttachmentFlow();
        if (suggestedCategory == "Categoria inativa")
        {
            await _host.WithDbAsync(async db =>
            {
                var inactive = new Category(_condominiumId, suggestedCategory, null);
                db.Categories.Add(inactive);
                await db.SaveChangesAsync();
                await db.Categories.Where(x => x.Id == inactive.Id).ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.IsActive, false));
            });
        }
        await PostAsync(TextPayload($"wamid.invalid-ai-category-{suggestedCategory}", "2"));

        await PostAsync(TextPayload($"wamid.confirm-invalid-category-{suggestedCategory}", "1"));

        Assert.Single(await _host.WithDbAsync(db => db.Requests.ToArrayAsync()));
        Assert.Equal("Manutenção", await _host.WithDbAsync(db => db.Requests
            .Join(db.Categories, request => request.CategoryId, category => category.Id,
                (_, category) => category.Name).SingleAsync()));
    }

    [Fact]
    public async Task Rewriting_calls_ai_only_after_new_report_and_preserves_attachments()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Primeiro título", "Primeira descrição", null, [], null), null);
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(true, [1], "image/jpeg", null);
        await PostAsync(MediaPayload("wamid.ai-rewrite-file", "ai-rewrite-file",
            "image", "image/jpeg", "foto.jpg"));
        await PostAsync(TextPayload("wamid.ai-first-review", "1"));
        Assert.Equal(1, _ai.Calls);

        await PostAsync(TextPayload("wamid.ai-rewrite", "2"));

        Assert.Equal(1, _ai.Calls);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Null(session.DraftAiProposalJson);
            Assert.Single(await db.WhatsAppDraftAttachments.ToArrayAsync());
        });
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título reescrito", "Descrição reescrita", null, [], null), null);
        await PostAsync(TextPayload("wamid.ai-new-report", "Novo relato original"));
        await PostAsync(TextPayload("wamid.ai-second-review", "2"));
        Assert.Equal(2, _ai.Calls);
        Assert.Contains("Título reescrito", _fake.Messages.Last().Text);
        Assert.Single(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("missing api key")]
    [InlineData("timeout")]
    [InlineData("http error")]
    [InlineData("refusal")]
    [InlineData("empty response")]
    [InlineData("invalid json")]
    [InlineData("outside schema")]
    [InlineData("manual validation failure")]
    public async Task Ai_failure_uses_traditional_review_and_manual_category(string error)
    {
        _ai.Result = new(false, null, error);
        await AddCategoryAndStartAttachmentFlow();
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_condominiumId, "Segurança", null));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload($"wamid.ai-fallback-{error}", "2"));

        var review = _fake.Messages.Last().Text;
        Assert.Equal("Você descreveu:\n\nPortão danificado\n\n" +
            "1 - Confirmar e continuar\n" +
            "2 - Reescrever descrição\n" +
            "3 - Cancelar e voltar ao início", review);
        Assert.DoesNotContain("Título", review);
        Assert.DoesNotContain("Categoria", review);
        Assert.DoesNotContain("MissingInformation", review);
        Assert.DoesNotContain("Confidence", review);
        Assert.DoesNotContain(error, review, StringComparison.OrdinalIgnoreCase);
        var storedReview = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.DraftAiProposalJson).SingleAsync());
        Assert.Contains("\"Source\":\"fallback\"", storedReview);

        await PostAsync(TextPayload($"wamid.ai-fallback-confirm-{error}", "1"));
        Assert.Contains("Escolha a categoria", _fake.Messages.Last().Text);
        await PostAsync(TextPayload($"wamid.ai-fallback-category-{error}", "1"));

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            Assert.Equal("Solicitação recebida pelo WhatsApp", request.Title);
            Assert.Equal("Portão danificado", request.Description);
            Assert.Equal("Portão danificado", await db.RequestMessages
                .Where(x => x.RequestId == request.Id).Select(x => x.Content).SingleAsync());
        });
    }

    [Theory]
    [InlineData("3", WhatsAppConversationState.MainMenu)]
    [InlineData("cancelar", WhatsAppConversationState.MainMenu)]
    [InlineData("menu", WhatsAppConversationState.MainMenu)]
    [InlineData("sair", WhatsAppConversationState.Ended)]
    public async Task Leaving_attachment_flow_removes_temporary_files(
        string command, WhatsAppConversationState expectedState)
    {
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "image/jpeg", null);
        await PostAsync(MediaPayload($"wamid.cleanup-{command}", $"cleanup-{command}", "image", "image/jpeg", "foto.jpg"));
        var storageKey = await _host.WithDbAsync(db => db.WhatsAppDraftAttachments
            .Select(x => x.StorageKey).SingleAsync());

        await PostAsync(TextPayload($"wamid.cleanup-command-{command}", command));

        Assert.Empty(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
        Assert.Equal(expectedState, await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.State).SingleAsync()));
        await _host.WithServicesAsync(services =>
        {
            Assert.Null(services.GetRequiredService<LocalFileStorage>().OpenRead(storageKey));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Expired_attachment_flow_removes_temporary_files_and_returns_menu()
    {
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(true, [1], "application/pdf", null);
        await PostAsync(MediaPayload("wamid.expiring-file", "expiring-file", "document", "application/pdf", "arquivo.pdf"));
        await _host.WithDbAsync(db => db.WhatsAppSessions.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        await PostAsync(TextPayload("wamid.expired-with-file", "Oi"));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        Assert.Empty(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
    }

    [Fact]
    public async Task Download_failure_and_invalid_media_keep_attachment_state()
    {
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(false, null, null, "simulated");
        await PostAsync(MediaPayload("wamid.download-failed", "download-failed", "image", "image/jpeg", "foto.jpg"));
        Assert.Contains("Não foi possível baixar", _fake.Messages.Last().Text);

        _fake.Media = new WhatsAppMediaResult(true, [1, 2], "image/svg+xml", null);
        await PostAsync(MediaPayload("wamid.invalid-media", "invalid-media", "image", "image/svg+xml", "imagem.svg"));
        Assert.Contains("não é suportado", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.CollectingAttachments,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Attachment_quantity_and_size_limits_match_portal_policy()
    {
        await AddCategoryAndStartAttachmentFlow();
        for (var index = 0; index < AttachmentPolicy.MaximumFileCount; index++)
        {
            _fake.Media = new WhatsAppMediaResult(true, [1], "image/jpeg", null);
            await PostAsync(MediaPayload($"wamid.limit-{index}", $"limit-{index}", "image", "image/jpeg", $"foto-{index}.jpg"));
        }
        await PostAsync(MediaPayload("wamid.limit-extra", "limit-extra", "image", "image/jpeg", "extra.jpg"));
        Assert.Contains("no máximo 10 arquivos", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.limit-menu", "menu"));
        await PostAsync(TextPayload("wamid.limit-open", "1"));
        await PostAsync(TextPayload("wamid.limit-description", "Nova descrição"));
        _fake.Media = new WhatsAppMediaResult(
            true, new byte[AttachmentPolicy.MaximumFileSize + 1], "video/mp4", null);
        await PostAsync(MediaPayload("wamid.too-large", "too-large", "video", "video/mp4", "grande.mp4"));
        Assert.Contains("no máximo 15 MB", _fake.Messages.Last().Text);
    }

    private async Task AddCategoryAndStartAttachmentFlow()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_condominiumId, "Manutenção", null));
            await db.SaveChangesAsync();
        });
        await PostAsync(TextPayload($"wamid.attachment-menu-{Guid.NewGuid():N}", "Oi"));
        await PostAsync(TextPayload($"wamid.attachment-open-{Guid.NewGuid():N}", "1"));
        await PostAsync(TextPayload($"wamid.attachment-description-{Guid.NewGuid():N}", "Portão danificado"));
        Assert.Equal(WhatsAppConversationState.CollectingAttachments,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
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

    private Task SetCondominiumMembershipActive(bool active) =>
        _host.WithDbAsync(async db =>
        {
            var membership = await db.CondominiumMemberships.SingleAsync(x =>
                x.UserId == _userId && x.CondominiumId == _condominiumId);
            if (active) membership.Activate();
            else membership.Deactivate(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

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
        string mimeType,
        string? fileName = null) => type switch
        {
            "video" => JsonSerializer.Serialize(new
            {
                entry = new[] { new { changes = new[] { new { value = new { messages = new object[]
                {
                    new { from = "5511999990001", id, timestamp = "1785236400", type,
                        video = new { id = mediaId, mime_type = mimeType, filename = fileName } }
                } } } } } }
            }),
            "document" => JsonSerializer.Serialize(new
            {
                entry = new[] { new { changes = new[] { new { value = new { messages = new object[]
                {
                    new { from = "5511999990001", id, timestamp = "1785236400", type,
                        document = new { id = mediaId, mime_type = mimeType, filename = fileName } }
                } } } } } }
            }),
            _ => JsonSerializer.Serialize(new
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
                                            mime_type = mimeType,
                                            filename = fileName
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        })
        };

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

    private sealed class FakeRequestDraftAiService : IRequestDraftAiService
    {
        public RequestDraftAiResult Result { get; set; } =
            new(false, null, "not configured");
        public int Calls { get; private set; }

        public Task<RequestDraftAiResult> ProposeAsync(string originalReport,
            IReadOnlyCollection<string> activeCategories,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
