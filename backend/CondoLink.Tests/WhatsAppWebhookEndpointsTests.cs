using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class WhatsAppWebhookEndpointsTests : IAsyncLifetime
{
    private const string AppSecret = "test-app-secret";
    private const string VerifyToken = "test-verify-token";
    private CoreEndpointTestHost _host = null!;
    private FakeWhatsAppClient _fake = null!;
    private FakeRequestDraftAiService _ai = null!;
    private FakeResidentReplyAiService _replyAi = null!;
    private FakeAudioTranscriptionService _transcription = null!;
    private FakeAdministrativeResidentExtractionService _administrativeExtraction = null!;
    private FakeAdministrativeResidentLookupExtractionService _administrativeLookupExtraction = null!;
    private RecordingLogger<AdministrativeResidentLookupService> _administrativeLookupLogger = null!;
    private FakeAdministrativeResidentMutationExtractionService _administrativeMutationExtraction = null!;
    private Guid _userId;
    private Guid _condominiumId;
    private Guid _unitId;

    public async Task InitializeAsync()
    {
        _fake = new FakeWhatsAppClient();
        _ai = new FakeRequestDraftAiService();
        _replyAi = new FakeResidentReplyAiService();
        _transcription = new FakeAudioTranscriptionService();
        _administrativeExtraction = new FakeAdministrativeResidentExtractionService();
        _administrativeLookupExtraction = new FakeAdministrativeResidentLookupExtractionService();
        _administrativeLookupLogger = new RecordingLogger<AdministrativeResidentLookupService>();
        _administrativeMutationExtraction = new FakeAdministrativeResidentMutationExtractionService();
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
                builder.Services.AddSingleton<IResidentReplyAiService>(_replyAi);
                builder.Services.AddSingleton<IWhatsAppAudioTranscriptionService>(_transcription);
                builder.Services.AddSingleton<IAdministrativeResidentExtractionService>(_administrativeExtraction);
                builder.Services.AddSingleton<IAdministrativeResidentLookupExtractionService>(_administrativeLookupExtraction);
                builder.Services.AddSingleton<ILogger<AdministrativeResidentLookupService>>(_administrativeLookupLogger);
                builder.Services.AddSingleton<IAdministrativeResidentMutationExtractionService>(_administrativeMutationExtraction);
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddScoped<ResidentReplyService>();
                builder.Services.AddScoped<AdministrativeResidentRegistrationService>();
                builder.Services.AddScoped<AdministrativeResidentLookupService>();
                builder.Services.AddScoped<AdministrativeUnitResolver>();
                builder.Services.AddScoped<AdministrativeResidentMembershipMutationService>();
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

    [Fact]
    public async Task Active_requirement_quick_reply_accepts_text_through_resident_reply_service()
    {
        var requestId = await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Manutenção", null);
            var request = new CondoLink.Domain.Entities.Request(_condominiumId,
                _userId, _unitId, category.Id, "Portão da garagem",
                "Relato original");
            var changedAt = DateTime.UtcNow;
            request.ChangeStatus(RequestStatus.WaitingForResident, changedAt);
            var manager = CoreTestSeed.User("Gestor", "reply-manager@example.com");
            var history = new RequestStatusHistory(request.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResident,
                manager.Id, "Qual foi o horário?", changedAt);
            db.AddRange(category, manager, request, history,
                new RequestResidentReplyRequirement(request.Id, manager.Id,
                    history.Id, history.Reason!, changedAt));
            await db.SaveChangesAsync();
            return request.Id;
        });

        await PostAsync(TemplateQuickReplyPayload("wamid.reply-now",
            "resident_reply_now", "Responder agora"));
        Assert.Contains("A administração precisa da seguinte informação:",
            _fake.Messages.Last().Text);
        Assert.Contains("Qual foi o horário?", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Como posso ajudar?", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db => Assert.Equal(
            WhatsAppConversationState.CollectingResidentReply,
            (await db.WhatsAppSessions.SingleAsync()).State));
        await PostAsync(TextPayload("wamid.reply-text", "Foi por volta das 22h"));
        Assert.Contains("Você respondeu", _fake.Messages.Last().Text);
        await PostAsync(TextPayload("wamid.reply-review", "1"));
        await PostAsync(TextPayload("wamid.reply-no-files", "2"));

        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.InProgress,
                (await db.Requests.SingleAsync(x => x.Id == requestId)).Status);
            var requirement = await db.RequestResidentReplyRequirements.SingleAsync();
            Assert.False(requirement.IsActive);
            Assert.True(requirement.HasUnreadAnswer);
            var message = Assert.Single(await db.RequestMessages
                .Where(x => x.RequestId == requestId).ToArrayAsync());
            Assert.Equal("Foi por volta das 22h", message.Content);
            Assert.Equal(MessageChannel.WhatsApp, message.Channel);
        });
    }

    [Fact]
    public async Task Template_quick_reply_correlates_outbound_across_brazilian_phone_variants()
    {
        var expectedRequestId = await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Portaria", null);
            var manager = CoreTestSeed.User("Gestor", "template-manager@example.com");
            var now = DateTime.UtcNow;
            var expected = new CondoLink.Domain.Entities.Request(
                _condominiumId, _userId, _unitId, category.Id,
                "Acesso principal", "Relato principal");
            expected.ChangeStatus(RequestStatus.WaitingForResident, now);
            var expectedHistory = new RequestStatusHistory(expected.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResident,
                manager.Id, "Qual foi o horário do acesso?", now);
            var other = new CondoLink.Domain.Entities.Request(
                _condominiumId, _userId, _unitId, category.Id,
                "Acesso secundário", "Outro relato");
            other.ChangeStatus(RequestStatus.WaitingForResident, now);
            var otherHistory = new RequestStatusHistory(other.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResident,
                manager.Id, "Qual foi o outro horário?", now);
            var outbound = new WhatsAppOutboundMessage(
                expected.Id, null, _userId, _condominiumId,
                "+5511999990001", WhatsAppNotificationType.InformationRequested,
                WhatsAppSendMode.Template, "template-correlation-test",
                "content", "resident_reply_required", "pt_BR", now);
            outbound.MarkSent("wamid.original-template", now);
            db.AddRange(category, manager, expected, expectedHistory,
                new RequestResidentReplyRequirement(expected.Id, manager.Id,
                    expectedHistory.Id, expectedHistory.Reason!, now),
                other, otherHistory,
                new RequestResidentReplyRequirement(other.Id, manager.Id,
                    otherHistory.Id, otherHistory.Reason!, now),
                outbound);
            await db.SaveChangesAsync();
            return expected.Id;
        });

        await PostAsync(TemplateQuickReplyPayload(
            "wamid.template-reply", "resident_reply_now", "Responder agora",
            "wamid.original-template", "551199990001"));

        Assert.Contains("Qual foi o horário do acesso?", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Qual foi o outro horário?", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(expectedRequestId, session.RequestId);
            Assert.Equal(WhatsAppConversationState.CollectingResidentReply,
                session.State);
        });
    }

    [Fact]
    public async Task Reply_later_button_keeps_requirement_active_and_ends_session()
    {
        var requestId = await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Portaria", null);
            var request = new CondoLink.Domain.Entities.Request(_condominiumId,
                _userId, _unitId, category.Id, "Acesso", "Relato");
            var now = DateTime.UtcNow;
            request.ChangeStatus(RequestStatus.WaitingForResident, now);
            var manager = CoreTestSeed.User("Gestor", "later-manager@example.com");
            var history = new RequestStatusHistory(request.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResident,
                manager.Id, "Confirme o horário.", now);
            var requirement = new RequestResidentReplyRequirement(request.Id,
                manager.Id, history.Id, history.Reason!, now);
            var session = new WhatsAppSession("+5511999990001", now,
                now.AddMinutes(30));
            session.ResolveContext(_userId, _condominiumId, _unitId);
            session.OfferResidentReply(request.Id, now, now.AddMinutes(30));
            db.AddRange(category, manager, request, history, requirement, session);
            await db.SaveChangesAsync();
            return request.Id;
        });

        await PostAsync(InteractiveReplyPayload("wamid.reply-later",
            "resident_reply_later", "Responder depois"));

        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.WaitingForResident,
                (await db.Requests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.True((await db.RequestResidentReplyRequirements.SingleAsync()).IsActive);
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.Ended, session.State);
            Assert.Null(session.RequestId);
        });
    }

    [Fact]
    public async Task Known_title_is_not_used_when_button_id_is_unknown()
    {
        await PostAsync(TextPayload("wamid.unknown-button-menu", "Oi"));
        await PostAsync(InteractiveReplyPayload("wamid.unknown-button",
            "another_action", "Responder agora"));

        Assert.Contains("Não reconheci essa opção", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Known_title_is_fallback_when_button_id_is_absent()
    {
        await PostAsync(TextPayload("wamid.title-fallback-menu", "Oi"));
        await PostAsync(InteractiveReplyPayload("wamid.title-fallback",
            string.Empty, "Responder agora"));

        Assert.Contains("Não consegui localizar a solicitação que precisa da sua resposta",
            _fake.Messages.Last().Text);
        Assert.Equal("resident_reply_correlation_failed",
            await _host.WithDbAsync(db => db.WhatsAppInboundMessages
                .Where(x => x.ExternalMessageId == "wamid.title-fallback")
                .Select(x => x.ProcessingResult).SingleAsync()));
    }

    [Fact]
    public async Task Platform_admin_confirms_resident_registration_and_replay_is_idempotent()
    {
        await _host.WithDbAsync(async db =>
        {
            var role = new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole)
            {
                Id = Guid.NewGuid(), NormalizedName = "PLATFORMADMIN"
            };
            db.Roles.Add(role);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _userId, RoleId = role.Id });
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x =>
                x.UserId == _userId));
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", "João da Silva", "47999998888",
                "joao@example.com", null, null, "101",
                "Owner", false, null), "succeeded");

        await PostAsync(TextPayload(
            "wamid.admin-register", "Cadastrar morador", "551199990001"));
        Assert.Contains("Envie os dados do morador em uma única mensagem", _fake.Messages.Last().Text);
        await PostAsync(TextPayload(
            "wamid.admin-register-data", "Cadastre o João da Silva como proprietário da unidade 101. E-mail joao@example.com e telefone 47999998888.", "551199990001"));

        Assert.Contains("1 - Confirmar", _fake.Messages.Last().Text);
        Assert.Contains("Condomínio: Residencial Teste", _fake.Messages.Last().Text);
        Assert.Contains("Relação: Proprietário", _fake.Messages.Last().Text);
        Assert.DoesNotContain("IsResident", _fake.Messages.Last().Text);
        Assert.DoesNotContain("IsPrimaryResidence", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Owner", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "joao@example.com")));
        Assert.Equal(_userId, await _host.WithDbAsync(db =>
            db.WhatsAppInboundMessages
                .Where(x => x.ExternalMessageId == "wamid.admin-register")
                .Select(x => x.IdentifiedUserId).SingleAsync()));

        await PostAsync(TextPayload(
            "wamid.admin-confirm", "1", "551199990001"));
        var messageCountAfterConfirmation = _fake.Messages.Count;
        await PostAsync(TextPayload(
            "wamid.admin-confirm", "1", "551199990001"));
        Assert.Equal(messageCountAfterConfirmation, _fake.Messages.Count);

        var completed = _fake.Messages.Last().Text;
        Assert.Contains("Morador cadastrado com sucesso", completed);
        Assert.Contains("Senha temporária:", completed);
        Assert.Contains("Mensagem para o morador:", completed);
        var passwords = completed.Split('\n')
            .Where(line => line.StartsWith("Senha temporária: "))
            .Select(line => line["Senha temporária: ".Length..])
            .Distinct().ToArray();
        var temporaryPassword = Assert.Single(passwords);
        await _host.WithServicesAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByEmailAsync("joao@example.com");
            Assert.True(await manager.CheckPasswordAsync(user!, temporaryPassword));
        });
        var messagesBeforeDistinctReplay = _fake.Messages.Count;
        await PostAsync(TextPayload(
            "wamid.admin-confirm-replay", "1", "551199990001"));
        if (_fake.Messages.Count > messagesBeforeDistinctReplay)
            Assert.DoesNotContain("Senha temporária", _fake.Messages.Last().Text);
        Assert.Equal(1, await _host.WithDbAsync(db =>
            db.Users.CountAsync(x => x.Email == "joao@example.com")));
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Email == "joao@example.com");
            Assert.True(user.MustChangePassword);
            var unitMembership = await db.UnitMemberships.SingleAsync(x =>
                x.UserId == user.Id && x.UnitId == _unitId);
            Assert.True(unitMembership.IsResident);
            Assert.False(unitMembership.IsPrimaryResidence);
        });
    }

    [Fact]
    public async Task Administrative_registration_requires_phone()
    {
        await _host.WithDbAsync(AddPlatformAdminRole);
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Sem Telefone", null,
                "sem-telefone@example.com", null, null, "101",
                "Tenant", null, null), "succeeded");

        await PostAsync(TextPayload("wamid.admin-phone-required", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-phone-required-data",
            "Sem Telefone, sem-telefone@example.com, unidade 101, inquilino"));

        Assert.Contains("telefone do morador", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db =>
            db.Users.CountAsync(x => x.Email == "sem-telefone@example.com")));
    }

    [Fact]
    public async Task Platform_admin_looks_up_unit_residents_with_default_safe_fields()
    {
        await _host.WithDbAsync(AddPlatformAdminRole);
        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, null, "101", ["phone"]),
            "succeeded");

        await PostAsync(TextPayload("wamid.admin-unit-lookup",
            "Quem mora na unidade 101?"));

        var response = _fake.Messages.Last().Text;
        Assert.Contains("Moradores da unidade 101", response);
        Assert.Contains("Maria Silva", response);
        Assert.Contains("Proprietário", response);
        Assert.Contains("Telefone: (11) 99999-0001", response);
        Assert.DoesNotContain("maria@example.com", response);
        Assert.DoesNotContain("MustChangePassword", response);
        var logs = string.Join('\n', _administrativeLookupLogger.Messages);
        Assert.DoesNotContain("maria@example.com", logs);
        Assert.DoesNotContain("99999-0001", logs);
        Assert.DoesNotContain(response, logs);

        _administrativeLookupExtraction.Result = new(true,
            new("resident_lookup", "Maria Silva", null, null, "101",
                ["phone", "email"]), "succeeded");
        await PostAsync(TextPayload("wamid.admin-resident-lookup",
            "Preciso dos dados da moradora Maria Silva da unidade 101"));
        var residentResponse = _fake.Messages.Last().Text;
        Assert.Contains("Maria Silva", residentResponse);
        Assert.Contains("Unidade: 101", residentResponse);
        Assert.Contains("Telefone: (11) 99999-0001", residentResponse);
        Assert.Contains("E-mail: maria@example.com", residentResponse);
    }

    [Fact]
    public async Task Manager_without_unit_membership_queries_only_authorized_scope()
    {
        Guid outsideId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            var membershipId = await db.CondominiumMemberships
                .Where(x => x.UserId == _userId && x.CondominiumId == _condominiumId)
                .Select(x => x.Id).SingleAsync();
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(
                membershipId, CondominiumRole.Manager));
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x => x.UserId == _userId));
            var resident = CoreTestSeed.User("Morador Consultado", "consultado@example.com");
            resident.Update("Morador Consultado", "11977776666");
            db.AddRange(resident, new UnitMembership(resident.Id, _unitId,
                UnitRelationshipType.Tenant, true, false));
            var outside = new Condominium("Fora do Escopo", null, null);
            outsideId = outside.Id;
            db.Add(outside);
            await db.SaveChangesAsync();
        });
        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, null, "101", ["phone"]),
            "succeeded");

        await PostAsync(TextPayload("wamid.manager-authorized-lookup",
            "Moradores da unidade 101"));
        Assert.Contains("Moradores da unidade 101", _fake.Messages.Last().Text);

        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, "Fora do Escopo", null, "101",
                ["phone"]), "succeeded");
        await PostAsync(TextPayload("wamid.manager-outside-lookup",
            "Moradores da unidade 101 no Fora do Escopo"));
        Assert.Contains("apenas para a administração", _fake.Messages.Last().Text);
        Assert.DoesNotContain(outsideId.ToString(), _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Resident_cannot_use_administrative_lookup()
    {
        _administrativeLookupExtraction.Result = new(true,
            new("resident_lookup", "Maria Silva", null, null, "101",
                ["phone", "email"]), "succeeded");

        await PostAsync(TextPayload("wamid.resident-forbidden-lookup",
            "Qual o telefone da Maria Silva da unidade 101?"));

        Assert.Equal("Esse recurso está disponível apenas para a administração do condomínio.",
            _fake.Messages.Last().Text);
        Assert.DoesNotContain("maria@example.com", _fake.Messages.Last().Text);
        Assert.DoesNotContain("99999", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Platform_admin_without_unit_routes_natural_infos_lookup_before_residential_context()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x => x.UserId == _userId));
            var block = new CondominiumBlock(_condominiumId, "Bloco 1");
            var unit = new Unit(_condominiumId, "1201", block.Id, null, null);
            var tatiana = CoreTestSeed.User("Tatiana Lima", "tatiana@example.com");
            tatiana.Update("Tatiana Lima", "44999998888");
            db.AddRange(block, unit, tatiana, new UnitMembership(tatiana.Id, unit.Id,
                UnitRelationshipType.AuthorizedOccupant, true, false));
            await db.SaveChangesAsync();
        });
        _administrativeLookupExtraction.Result = new(true,
            new("resident_lookup", "Tatiana", null, "1", "1201",
                ["phone", "email"]), "succeeded");

        await PostAsync(TextPayload("wamid.platform-infos-no-unit",
            "Oi, me dê as infos da Tatiana do 1201/1"));

        Assert.Contains("Tatiana Lima", _fake.Messages.Last().Text);
        Assert.Contains("Unidade: Bloco 1 - 1201", _fake.Messages.Last().Text);
        Assert.DoesNotContain("unidade residencial ativa", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Administrative_audio_is_transcribed_and_routed_before_residential_context()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x => x.UserId == _userId));
            var block = new CondominiumBlock(_condominiumId, "Bloco 1");
            var unit = new Unit(_condominiumId, "1201", block.Id, null, null);
            var resident = CoreTestSeed.User("Tatiana Áudio", "tatiana.audio@example.com");
            resident.Update("Tatiana Áudio", "44988887777");
            db.AddRange(block, unit, resident, new UnitMembership(resident.Id, unit.Id,
                UnitRelationshipType.Tenant, true, false));
            await db.SaveChangesAsync();
        });
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);
        _transcription.Result = new(true,
            "Me passe os moradores do bloco 1 apartamento 1201 com telefone.",
            "succeeded");
        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, "1", "1201", ["phone"]),
            "succeeded");

        await PostAsync(MediaPayload("wamid.admin-audio-lookup", "admin-audio",
            "audio", "audio/ogg", "consulta.ogg"));

        Assert.Contains("Tatiana Áudio", _fake.Messages.Last().Text);
        Assert.DoesNotContain("unidade residencial ativa", _fake.Messages.Last().Text);
        Assert.Equal(1, _transcription.Calls);
    }

    [Fact]
    public async Task Administrative_user_without_unit_gets_role_oriented_fallback()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x => x.UserId == _userId));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.admin-unknown-action",
            "Quais os contextos estão disponíveis?"));

        var response = _fake.Messages.Last().Text;
        Assert.Contains("Não consegui identificar o que você deseja fazer", response);
        Assert.Contains("Cadastrar morador", response);
        Assert.DoesNotContain("unidade residencial ativa", response);
    }

    [Theory]
    [InlineData("proprietário", UnitRelationshipType.Owner)]
    [InlineData("proprietária", UnitRelationshipType.Owner)]
    [InlineData("inquilino", UnitRelationshipType.Tenant)]
    [InlineData("inquilina", UnitRelationshipType.Tenant)]
    [InlineData("morador", UnitRelationshipType.AuthorizedOccupant)]
    [InlineData("moradora", UnitRelationshipType.AuthorizedOccupant)]
    public void Administrative_relationship_synonyms_map_deterministically(
        string value, UnitRelationshipType expected)
    {
        Assert.True(AdministrativeResidentRegistrationService.TryRelationship(
            value, out var relationship));
        Assert.Equal(expected, relationship);
    }

    [Fact]
    public async Task Platform_admin_deactivates_only_confirmed_unit_membership()
    {
        Guid targetMembershipId = Guid.Empty;
        Guid targetUserId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x => x.UserId == _userId));
            var block = new CondominiumBlock(_condominiumId, "Bloco 1");
            var unit = new Unit(_condominiumId, "105", block.Id, null, null);
            var otherUnit = new Unit(_condominiumId, "106", block.Id, null, null);
            var target = CoreTestSeed.User("Fulano Silva", "fulano@example.com");
            var membership = new UnitMembership(target.Id, unit.Id,
                UnitRelationshipType.AuthorizedOccupant, true, false);
            db.AddRange(block, unit, otherUnit, target, membership,
                new UnitMembership(target.Id, otherUnit.Id,
                    UnitRelationshipType.Tenant, true, false));
            await db.SaveChangesAsync();
            targetMembershipId = membership.Id;
            targetUserId = target.Id;
        });
        _administrativeMutationExtraction.Result = new(true,
            new("resident_membership_deactivate", "Fulano", null,
                "1", "105", null, null, null), "succeeded");

        await PostAsync(TextPayload("wamid.deactivate-membership",
            "Inative o morador Fulano do 105 bloco 1"));
        Assert.Contains("Confirme a alteração", _fake.Messages.Last().Text);
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships
            .Where(x => x.Id == targetMembershipId).Select(x => x.IsActive).SingleAsync()));

        await PostAsync(TextPayload("wamid.deactivate-membership-confirm", "1"));
        Assert.Contains("Vínculo encerrado", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var ended = await db.UnitMemberships.SingleAsync(x => x.Id == targetMembershipId);
            Assert.False(ended.IsActive);
            Assert.NotNull(ended.EndedAt);
            Assert.True(await db.UnitMemberships.AnyAsync(x => x.UserId == targetUserId
                && x.IsActive));
            Assert.True((await db.Users.SingleAsync(x => x.Id == targetUserId)).IsActive);
        });
        var activeCount = await _host.WithDbAsync(db => db.UnitMemberships
            .CountAsync(x => x.UserId == targetUserId && x.IsActive));
        await PostAsync(TextPayload("wamid.deactivate-membership-replay", "1"));
        Assert.Equal(activeCount, await _host.WithDbAsync(db => db.UnitMemberships
            .CountAsync(x => x.UserId == targetUserId && x.IsActive)));
    }

    [Fact]
    public async Task Confirmed_move_ends_origin_and_creates_destination_atomically()
    {
        Guid targetUserId = Guid.Empty;
        Guid sourceId = Guid.Empty;
        Guid destinationId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            var block1 = new CondominiumBlock(_condominiumId, "Bloco 1");
            var block2 = new CondominiumBlock(_condominiumId, "Bloco 2");
            var source = new Unit(_condominiumId, "105", block1.Id, null, null);
            var destination = new Unit(_condominiumId, "405", block2.Id, null, null);
            var target = CoreTestSeed.User("Ciclano Souza", "ciclano@example.com");
            db.AddRange(block1, block2, source, destination, target,
                new UnitMembership(target.Id, source.Id,
                    UnitRelationshipType.Owner, true, true));
            await db.SaveChangesAsync();
            targetUserId = target.Id;
            sourceId = source.Id;
            destinationId = destination.Id;
        });
        _administrativeMutationExtraction.Result = new(true,
            new("resident_membership_move", "Ciclano", null,
                "1", "105", "2", "405", "Tenant"), "succeeded");
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);
        _transcription.Result = new(true,
            "Mude o Ciclano do 105/1 para o 405/2 como inquilino", "succeeded");

        await PostAsync(MediaPayload("wamid.move-membership", "move-membership-audio",
            "audio", "audio/ogg", "mudanca.ogg"));
        Assert.Contains("De:\nBloco 1 - 105", _fake.Messages.Last().Text);
        Assert.Contains("Para:\nBloco 2 - 405", _fake.Messages.Last().Text);
        Assert.Contains("Relação: Inquilino", _fake.Messages.Last().Text);
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships.AnyAsync(x =>
            x.UserId == targetUserId && x.UnitId == sourceId && x.IsActive)));

        await PostAsync(TextPayload("wamid.move-membership-confirm", "1"));

        await _host.WithDbAsync(async db =>
        {
            Assert.True(await db.UnitMemberships.AnyAsync(x => x.UserId == targetUserId
                && x.UnitId == sourceId && !x.IsActive && x.EndedAt != null));
            var moved = await db.UnitMemberships.SingleAsync(x => x.UserId == targetUserId
                && x.UnitId == destinationId && x.IsActive);
            Assert.Equal(UnitRelationshipType.Tenant, moved.RelationshipType);
            Assert.True(moved.IsResident);
            Assert.True(moved.IsPrimaryResidence);
        });
    }

    [Fact]
    public async Task Resident_cannot_start_membership_mutation()
    {
        _administrativeMutationExtraction.Result = new(true,
            new("resident_membership_deactivate", "Maria", null,
                null, "101", null, null, null), "succeeded");

        await PostAsync(TextPayload("wamid.resident-mutation-forbidden",
            "Remova a Maria da unidade 101"));

        Assert.Equal("Esse recurso está disponível apenas para a administração do condomínio.",
            _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Lookup_handles_condominium_unit_and_resident_ambiguity()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            var secondCondominium = new Condominium("Residencial Dois", null, null);
            var blockA = new CondominiumBlock(_condominiumId, "A");
            var blockB = new CondominiumBlock(_condominiumId, "B");
            var unitA = new Unit(_condominiumId, "302", blockA.Id, null, null);
            var unitB = new Unit(_condominiumId, "302", blockB.Id, null, null);
            var secondResident = CoreTestSeed.User("Maria Souza", "maria.souza@example.com");
            secondResident.Update("Maria Souza", "11988887777");
            db.AddRange(secondCondominium, blockA, blockB, unitA, unitB,
                secondResident, new UnitMembership(secondResident.Id, _unitId,
                    UnitRelationshipType.Tenant, true, false));
            await db.SaveChangesAsync();
        });
        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, null, "302", ["phone"]),
            "succeeded");

        await PostAsync(TextPayload("wamid.lookup-condo-ambiguous",
            "Moradores da unidade 302"));
        Assert.Contains("Em qual condomínio?", _fake.Messages.Last().Text);
        Assert.Contains("Residencial Teste", _fake.Messages.Last().Text);
        Assert.Contains("Residencial Dois", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.lookup-condo-choice", "2"));
        Assert.Contains("Bloco A - 302", _fake.Messages.Last().Text);
        Assert.Contains("Bloco B - 302", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.lookup-cancel", "0"));
        Assert.Contains("Consulta cancelada", _fake.Messages.Last().Text);

        _administrativeLookupExtraction.Result = new(true,
            new("resident_lookup", "Maria", "Residencial Teste", null, null,
                ["phone", "email"]), "succeeded");
        await PostAsync(TextPayload("wamid.lookup-resident-ambiguous",
            "Dados da moradora Maria no Residencial Teste"));
        Assert.Contains("Encontrei mais de um morador", _fake.Messages.Last().Text);
        Assert.Contains("Maria Silva", _fake.Messages.Last().Text);
        Assert.Contains("Maria Souza", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Lookup_reports_missing_unit_and_empty_unit_without_creating_data()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.Add(new Unit(_condominiumId, "404", null, null, null));
            await db.SaveChangesAsync();
        });
        var unitsBefore = await _host.WithDbAsync(db => db.Units.CountAsync());
        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, null, "999", ["phone"]),
            "succeeded");
        await PostAsync(TextPayload("wamid.lookup-unit-missing",
            "Moradores da unidade 999"));
        Assert.Contains("Não encontrei essa unidade", _fake.Messages.Last().Text);

        _administrativeLookupExtraction.Result = new(true,
            new("unit_residents_lookup", null, null, null, "404", ["phone"]),
            "succeeded");
        await PostAsync(TextPayload("wamid.lookup-unit-empty",
            "Moradores da unidade 404"));
        Assert.Contains("Não há moradores ativos", _fake.Messages.Last().Text);
        Assert.Equal(unitsBefore, await _host.WithDbAsync(db => db.Units.CountAsync()));
    }

    [Fact]
    public async Task Missing_fields_are_grouped_and_complement_preserves_the_draft()
    {
        await _host.WithDbAsync(AddPlatformAdminRole);
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Zemilto Custódio", null,
                "zemilto@example.com", null, null, "101",
                null, null, null), "succeeded");

        await PostAsync(TextPayload("wamid.admin-missing-start", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-missing-data",
            "Zemilto Custódio, zemilto@example.com, unidade 101"));

        Assert.Contains("• telefone", _fake.Messages.Last().Text);
        Assert.Contains("• relação com a unidade", _fake.Messages.Last().Text);
        Assert.DoesNotContain("• nome", _fake.Messages.Last().Text);
        Assert.DoesNotContain("• e-mail", _fake.Messages.Last().Text);

        _administrativeExtraction.Result = new(true,
            new("register_resident", null, "44999999999", null,
                null, null, null, "Owner", null, null), "succeeded");
        await PostAsync(TextPayload("wamid.admin-missing-complement",
            "44999999999, proprietário"));

        Assert.Contains("Nome: Zemilto Custódio", _fake.Messages.Last().Text);
        Assert.Contains("E-mail: zemilto@example.com", _fake.Messages.Last().Text);
        Assert.Contains("Relação: Proprietário", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Unit_clarification_preserves_registration_draft_and_relationship()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            var block1 = new CondominiumBlock(_condominiumId, "Bloco 1");
            var block2 = new CondominiumBlock(_condominiumId, "Bloco 2");
            db.AddRange(block1, block2,
                new Unit(_condominiumId, "1201", block1.Id, null, null),
                new Unit(_condominiumId, "1201", block2.Id, null, null));
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Tatiana Lima", "44999998888",
                "tatiana.cadastro@example.com", null, null, "1201",
                "morador", null, null), "succeeded");

        await PostAsync(TextPayload("wamid.registration-unit-start", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.registration-unit-data",
            "Tatiana Lima, tatiana.cadastro@example.com, 44999998888, 1201/1, morador"));
        Assert.Contains("Encontrei mais de uma unidade", _fake.Messages.Last().Text);

        _administrativeExtraction.Result = new(true,
            new("register_resident", null, null, null, null, "1", "1201",
                null, null, null), "succeeded");
        await PostAsync(TextPayload("wamid.registration-unit-clarification",
            "bloco 1 apto 1201"));

        var confirmation = _fake.Messages.Last().Text;
        Assert.Contains("Nome: Tatiana Lima", confirmation);
        Assert.Contains("E-mail: tatiana.cadastro@example.com", confirmation);
        Assert.Contains("Telefone: (44) 99999-8888", confirmation);
        Assert.Contains("Relação: Ocupante autorizado", confirmation);
        Assert.Contains("Unidade: Bloco 1 - 1201", confirmation);
        Assert.Contains("1 - Confirmar", confirmation);
    }

    [Fact]
    public async Task Explicit_admin_command_does_not_fall_back_when_ai_intent_is_unknown()
    {
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x =>
                x.UserId == _userId));
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("unknown", null, null, null, null, null, null,
                null, null, null), "succeeded");

        await PostAsync(TextPayload(
            "wamid.admin-explicit-unknown-ai", "Cadastrar morador"));
        await PostAsync(TextPayload(
            "wamid.admin-explicit-unknown-ai-data", "dados incompletos"));

        Assert.Contains("• nome", _fake.Messages.Last().Text);
        Assert.DoesNotContain("unidade residencial ativa", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Resident_cannot_start_administrative_registration()
    {
        await PostAsync(TextPayload("wamid.admin-forbidden", "Cadastrar morador"));

        Assert.Contains("somente para administradores autorizados", _fake.Messages.Last().Text);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Authorized_manager_gets_only_missing_data_and_can_cancel_without_writes()
    {
        await _host.WithDbAsync(async db =>
        {
            var membershipId = await db.CondominiumMemberships
                .Where(x => x.UserId == _userId && x.CondominiumId == _condominiumId)
                .Select(x => x.Id).SingleAsync();
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(
                membershipId, CondominiumRole.Manager));
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x =>
                x.UserId == _userId));
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", null, null, null, null, null, null,
                null, null, null), "succeeded");

        await PostAsync(TextPayload("wamid.admin-manager-start", "Novo morador"));
        Assert.Contains("Envie os dados do morador em uma única mensagem", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.CollectingAdminResidentData,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));

        await PostAsync(TextPayload("wamid.admin-manager-cancel", "0"));
        Assert.Contains("rascunho foi mantido", _fake.Messages.Last().Text);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Users.CountAsync()));

        await PostAsync(TextPayload("wamid.admin-manager-resident-menu", "Oi"));
        Assert.Contains("unidade residencial ativa", _fake.Messages.Last().Text);
        Assert.DoesNotContain("identificar seu cadastro", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Administrative_resident_uses_intent_to_select_context()
    {
        await _host.WithDbAsync(AddPlatformAdminRole);
        _administrativeExtraction.Result = new(true,
            new("register_resident", null, null, null, null, null, null,
                null, null, null), "succeeded");

        await PostAsync(TextPayload("wamid.admin-resident-command", "Cadastrar morador"));
        Assert.Contains("Envie os dados do morador em uma única mensagem", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.admin-resident-cancel", "0"));
        await PostAsync(TextPayload("wamid.admin-resident-menu", "menu"));

        Assert.Contains("Como posso ajudar", _fake.Messages.Last().Text);
        Assert.DoesNotContain("unidade residencial ativa", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Manager_without_unit_cannot_register_in_another_condominium()
    {
        await _host.WithDbAsync(async db =>
        {
            var membershipId = await db.CondominiumMemberships
                .Where(x => x.UserId == _userId && x.CondominiumId == _condominiumId)
                .Select(x => x.Id).SingleAsync();
            var outside = new Condominium("Condomínio fora do escopo", null, null);
            db.AddRange(outside, new Unit(outside.Id, "202", null, null, null),
                new CondominiumMembershipRole(membershipId, CondominiumRole.Manager));
            db.UnitMemberships.RemoveRange(db.UnitMemberships.Where(x =>
                x.UserId == _userId));
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Fora do Escopo", "11988887777",
                "fora@example.com", "Condomínio fora do escopo", null, "202",
                "Tenant", null, null), "succeeded");

        await PostAsync(TextPayload("wamid.admin-manager-outside", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-manager-outside-data",
            "Fora do Escopo, fora@example.com, 11988887777, unidade 202, inquilino"));

        Assert.Contains("dentro do seu acesso administrativo", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db =>
            db.Users.CountAsync(x => x.Email == "fora@example.com")));
    }

    [Fact]
    public async Task Ambiguous_units_require_selection_before_confirmation()
    {
        await _host.WithDbAsync(async db =>
        {
            var role = new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole)
            { Id = Guid.NewGuid(), NormalizedName = "PLATFORMADMIN" };
            var blockA = new CondominiumBlock(_condominiumId, "A");
            var blockB = new CondominiumBlock(_condominiumId, "B");
            db.AddRange(role, blockA, blockB,
                new Unit(_condominiumId, "302", blockA.Id, null, null),
                new Unit(_condominiumId, "302", blockB.Id, null, null));
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _userId, RoleId = role.Id });
            await db.SaveChangesAsync();
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Ana Souza", "47999995555", "ana@example.com",
                "Residencial Teste", null, "302", "Tenant", true, true), "succeeded");

        await PostAsync(TextPayload("wamid.admin-ambiguous", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-ambiguous-data",
            "Ana Souza, ana@example.com, 47999995555, unidade 302, inquilina"));
        Assert.Contains("Bloco A - 302", _fake.Messages.Last().Text);
        Assert.Contains("Bloco B - 302", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "ana@example.com")));

        await PostAsync(TextPayload("wamid.admin-unit-choice", "2"));
        Assert.Contains("1 - Confirmar", _fake.Messages.Last().Text);
        Assert.Contains("Bloco B - 302", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Extraction_failure_never_writes_registration_data()
    {
        await _host.WithDbAsync(async db =>
        {
            var role = new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole)
            { Id = Guid.NewGuid(), NormalizedName = "PLATFORMADMIN" };
            db.Add(role);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _userId, RoleId = role.Id });
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.admin-ai-failure", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-ai-failure-data", "Dados do morador"));

        Assert.Contains("Nenhuma alteração", _fake.Messages.Last().Text);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Existing_user_is_reused_and_only_linked_after_confirmation()
    {
        Guid existingId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            await AddPlatformAdminRole(db);
            var existing = CoreTestSeed.User("Carlos Existente", "carlos@example.com");
            existing.Update("Carlos Existente", "(47) 99999-7777");
            db.Add(existing);
            await db.SaveChangesAsync();
            existingId = existing.Id;
        });
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Nome ignorado", "47999997777",
                "carlos@example.com", "Residencial Teste", null, "101",
                "AuthorizedOccupant", true, false), "succeeded");

        await PostAsync(TextPayload("wamid.admin-existing", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-existing-data",
            "Carlos, carlos@example.com, 47999997777, unidade 101, ocupante autorizado"));
        Assert.Contains("já possui cadastro no Comvy", _fake.Messages.Last().Text);
        Assert.False(await _host.WithDbAsync(db => db.UnitMemberships.AnyAsync(x => x.UserId == existingId)));

        await PostAsync(TextPayload("wamid.admin-existing-confirm", "1"));
        Assert.Contains("Morador vinculado com sucesso", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Senha temporária", _fake.Messages.Last().Text);
        Assert.Equal(2, await _host.WithDbAsync(db => db.Users.CountAsync()));
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships.AnyAsync(x =>
            x.UserId == existingId && x.UnitId == _unitId)));
    }

    [Fact]
    public async Task Correction_replaces_draft_and_expired_confirmation_writes_nothing()
    {
        await _host.WithDbAsync(AddPlatformAdminRole);
        _administrativeExtraction.Result = new(true,
            new("register_resident", "Beatriz Lima", "47999996666",
                "bia@example.com", "Residencial Teste", null, "101",
                "Owner", true, true), "succeeded");
        await PostAsync(TextPayload("wamid.admin-correction-start", "Cadastrar morador"));
        await PostAsync(TextPayload("wamid.admin-correction-start-data",
            "Beatriz Lima, bia@example.com, 47999996666, unidade 101, proprietária"));
        await PostAsync(TextPayload("wamid.admin-correction-choice", "2"));
        Assert.Contains("Envie apenas o que deseja corrigir", _fake.Messages.Last().Text);

        _administrativeExtraction.Result = new(true,
            new("register_resident", null, "47988885555", null, null, null,
                null, "Tenant", null, false), "succeeded");
        await PostAsync(TextPayload("wamid.admin-correction-data", "Telefone correto e relação inquilino"));
        Assert.Contains("47988885555", _fake.Messages.Last().Text);
        Assert.Contains("Inquilino", _fake.Messages.Last().Text);
        Assert.Contains("Nome: Beatriz Lima", _fake.Messages.Last().Text);
        Assert.Contains("E-mail: bia@example.com", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "bia@example.com")));

        await _host.WithDbAsync(db => db.WhatsAppSessions.ExecuteUpdateAsync(setters =>
            setters.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        await PostAsync(TextPayload("wamid.admin-expired-confirm", "1"));
        Assert.Contains("expirou", _fake.Messages.Last().Text);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "bia@example.com")));
    }

    private async Task AddPlatformAdminRole(AppDbContext db)
    {
        var role = new IdentityRole<Guid>(DependencyInjection.PlatformAdminRole)
        { Id = Guid.NewGuid(), NormalizedName = "PLATFORMADMIN" };
        db.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _userId, RoleId = role.Id });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Arbitrary_title_is_not_a_button_when_button_id_is_absent()
    {
        await PostAsync(TextPayload("wamid.arbitrary-title-menu", "Oi"));
        await PostAsync(InteractiveReplyPayload("wamid.arbitrary-title",
            string.Empty, "Quero falar com alguém"));

        Assert.Contains("Não reconheci essa opção", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
            await _host.WithDbAsync(db => db.WhatsAppSessions
                .Select(x => x.State).SingleAsync()));
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
    public async Task Noncanonical_stored_number_does_not_override_exact_canonical_match()
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

        Assert.Contains("Como posso ajudar", Assert.Single(_fake.Messages).Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
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
        Assert.DoesNotContain("Falar com a administração", sent.Text);
        Assert.DoesNotContain("4 -", sent.Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Equal("main_menu", (await db.WhatsAppInboundMessages.SingleAsync()).ProcessingResult);
        });
    }

    [Fact]
    public async Task Invalid_menu_option_does_not_advance_the_session()
    {
        await PostAsync(TextPayload("wamid.unavailable-menu", "Menu"));
        await PostAsync(TextPayload("wamid.unavailable-4", "4"));

        Assert.Contains("Não reconheci essa opção", _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Option_two_lists_no_requests_without_leaving_the_tracking_flow()
    {
        await PostAsync(TextPayload("wamid.own-empty-menu", "Menu"));
        await PostAsync(TextPayload("wamid.own-empty", "2"));

        Assert.Equal("Você ainda não possui solicitações para consultar.\n\n0 - Voltar ao menu",
            _fake.Messages.Last().Text);
        Assert.Equal(WhatsAppConversationState.ListingOwnRequests,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Option_three_adds_multiple_spontaneous_updates_without_answering_requirement()
    {
        var requestId = await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Manutenção", null);
            var manager = CoreTestSeed.User("Gestor", "updates-manager@example.com");
            var now = DateTime.UtcNow;
            var request = new CondoLink.Domain.Entities.Request(
                _condominiumId, _userId, _unitId, category.Id,
                "Portão da garagem", "Relato original");
            request.ChangeStatus(RequestStatus.WaitingForResident, now);
            var history = new RequestStatusHistory(request.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResident,
                manager.Id, "Informe o horário.", now);
            var closed = new CondoLink.Domain.Entities.Request(
                _condominiumId, _userId, _unitId, category.Id,
                "Solicitação encerrada", "Não deve aparecer");
            closed.ChangeStatus(RequestStatus.Resolved, now);
            db.AddRange(category, manager, request, history,
                new RequestResidentReplyRequirement(request.Id, manager.Id,
                    history.Id, history.Reason!, now), closed);
            await db.SaveChangesAsync();
            return request.Id;
        });

        await PostAsync(TextPayload("wamid.update-menu", "Menu"));
        await PostAsync(TextPayload("wamid.update-option", "3"));
        Assert.Contains("Portão da garagem", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Solicitação encerrada", _fake.Messages.Last().Text);
        await PostAsync(TextPayload("wamid.update-select", "1"));
        Assert.Contains("Envie sua mensagem", _fake.Messages.Last().Text);

        await PostAsync(TextPayload("wamid.update-text-1", "Primeira atualização."));
        await PostAsync(TextPayload("wamid.update-text-2", "Segunda atualização."));
        _fake.Media = new WhatsAppMediaResult(
            true, [0xFF, 0xD8, 0xFF, 0xD9], "image/jpeg", null);
        await PostAsync(MediaPayload(
            "wamid.update-image", "update-image", "image", "image/jpeg", "foto.jpg"));
        _fake.Media = new WhatsAppMediaResult(
            true, [1, 2, 3], "audio/ogg", null);
        await PostAsync(MediaPayload(
            "wamid.update-audio", "update-audio", "audio", "audio/ogg", "audio.ogg"));
        await PostAsync(TextPayload("wamid.update-finish", "Finalizar"));

        await _host.WithDbAsync(async db =>
        {
            var messages = await db.RequestMessages
                .Where(x => x.RequestId == requestId)
                .OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(4, messages.Length);
            Assert.All(messages, message => Assert.Equal(
                MessageChannel.WhatsAppResidentUpdate, message.Channel));
            Assert.Equal(2, await db.RequestAttachments.CountAsync(x =>
                x.RequestId == requestId && x.RequestMessageId != null));
            Assert.True((await db.RequestResidentReplyRequirements.SingleAsync()).IsActive);
            Assert.Equal(RequestStatus.WaitingForResident,
                (await db.Requests.SingleAsync(x => x.Id == requestId)).Status);
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.Ended, session.State);
            Assert.Null(session.RequestId);
        });
        Assert.Equal(1, _transcription.Calls);
        await _host.WithDbAsync(async db =>
        {
            var audio = await db.RequestAttachments.SingleAsync(x =>
                x.RequestId == requestId && x.ContentType == "audio/ogg");
            var audioMessage = await db.RequestMessages.SingleAsync(x =>
                x.Id == audio.RequestMessageId);
            Assert.Equal("Transcrição do relato em áudio.", audioMessage.Content);
            Assert.Equal(MessageChannel.WhatsAppResidentUpdate, audioMessage.Channel);
        });
    }

    [Fact]
    public async Task Spontaneous_update_keeps_audio_when_transcription_fails()
    {
        var requestId = await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Manutenção", null);
            var request = new CondoLink.Domain.Entities.Request(
                _condominiumId, _userId, _unitId, category.Id,
                "Interfone", "Relato original");
            db.AddRange(category, request);
            await db.SaveChangesAsync();
            return request.Id;
        });
        await PostAsync(TextPayload("wamid.failed-update-menu", "Menu"));
        await PostAsync(TextPayload("wamid.failed-update-option", "3"));
        await PostAsync(TextPayload("wamid.failed-update-select", "1"));
        _transcription.Result = new(false, null, "provider_error");
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);

        await PostAsync(MediaPayload("wamid.failed-update-audio", "failed-update-audio",
            "audio", "audio/ogg", "audio.ogg"));

        await _host.WithDbAsync(async db =>
        {
            var attachment = await db.RequestAttachments.SingleAsync(x =>
                x.RequestId == requestId && x.ContentType == "audio/ogg");
            var message = await db.RequestMessages.SingleAsync(x =>
                x.Id == attachment.RequestMessageId);
            Assert.Equal("Áudio enviado pelo morador.", message.Content);
            Assert.Equal(MessageChannel.WhatsAppResidentUpdate, message.Channel);
        });
        Assert.Equal(1, _transcription.Calls);
    }

    [Fact]
    public async Task Cancelling_spontaneous_update_clears_only_session_context()
    {
        await SeedOwnRequests(2);
        await PostAsync(TextPayload("wamid.update-cancel-menu", "Menu"));
        await PostAsync(TextPayload("wamid.update-cancel-option", "3"));
        await PostAsync(TextPayload("wamid.update-cancel-select", "1"));
        await PostAsync(TextPayload("wamid.update-before-cancel", "Atualização já enviada."));
        await PostAsync(TextPayload("wamid.update-cancel", "Cancelar"));

        await _host.WithDbAsync(async db =>
        {
            Assert.Single(await db.RequestMessages.Where(x =>
                x.Channel == MessageChannel.WhatsAppResidentUpdate).ToArrayAsync());
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
            Assert.Null(session.RequestId);
        });
    }

    [Fact]
    public async Task Status_query_lists_all_non_cancelled_requests_in_one_message_with_only_back()
    {
        var requestIds = await SeedOwnRequests(7);
        await _host.WithDbAsync(db => db.Requests
            .Where(request => request.Id == requestIds[1])
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(request => request.Status, RequestStatus.Cancelled)));
        await SeedInaccessibleRequests();
        await PostAsync(TextPayload("wamid.own-list-menu", "Menu"));
        await PostAsync(TextPayload("wamid.own-list", "2"));

        var result = _fake.Messages.Last().Text;
        Assert.Contains("Status de suas solicitações", result);
        Assert.Contains("Solicitação 7", result);
        Assert.Contains("Solicitação 1", result);
        Assert.DoesNotContain("Solicitação 2", result);
        Assert.DoesNotContain("Solicitação externa", result);
        Assert.Contains("Status: Aberta", result);
        Assert.Contains("Status: Resolvida", result);
        Assert.Contains("Atualizada em:", result);
        Assert.EndsWith("0 - Voltar ao menu", result);
        Assert.DoesNotContain("6 - Ver mais", result);
        Assert.DoesNotContain("7 - Página anterior", result);
        Assert.DoesNotContain("Digite o número", result);
    }

    [Fact]
    public async Task Status_query_does_not_open_a_request_or_start_a_conversation()
    {
        await SeedOwnRequests(1);
        await PostAsync(TextPayload("wamid.own-details-menu", "Menu"));
        await PostAsync(TextPayload("wamid.own-details-list", "2"));
        await PostAsync(TextPayload("wamid.own-details-select", "1"));
        var response = _fake.Messages.Last().Text;
        Assert.Contains("Escolha uma opção válida", response);
        Assert.Contains("Status de suas solicitações", response);
        Assert.DoesNotContain("*Descrição:*", response);
        Assert.Equal(WhatsAppConversationState.ListingOwnRequests,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
    }

    [Fact]
    public async Task Request_that_becomes_inaccessible_is_not_disclosed_after_listing()
    {
        var requestId = Assert.Single(await SeedOwnRequests(1));
        await PostAsync(TextPayload("wamid.inaccessible-menu", "Menu"));
        await PostAsync(TextPayload("wamid.inaccessible-list", "2"));
        await _host.WithDbAsync(async db =>
        {
            var other = CoreTestSeed.User("Outro", "other-owner@example.com");
            db.Users.Add(other);
            await db.SaveChangesAsync();
            await db.Requests.Where(x => x.Id == requestId).ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.AuthorUserId, other.Id));
        });

        await PostAsync(TextPayload("wamid.inaccessible-select", "1"));

        Assert.Contains("Escolha uma opção válida", _fake.Messages.Last().Text);
        Assert.DoesNotContain("Solicitação 1", _fake.Messages.Last().Text);
    }

    [Theory]
    [InlineData("menu", WhatsAppConversationState.MainMenu)]
    [InlineData("cancelar", WhatsAppConversationState.MainMenu)]
    [InlineData("sair", WhatsAppConversationState.Ended)]
    public async Task Global_commands_leave_own_request_flow(
        string command, WhatsAppConversationState expected)
    {
        await SeedOwnRequests(1);
        await PostAsync(TextPayload($"wamid.command-menu-{command}", "Menu"));
        await PostAsync(TextPayload($"wamid.command-list-{command}", "2"));
        await PostAsync(TextPayload($"wamid.command-{command}", command));

        Assert.Equal(expected, await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.State).SingleAsync()));
        Assert.Equal(0, await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.Page).SingleAsync()));
    }

    [Theory]
    [InlineData(RequestStatus.Open, "Aberta")]
    [InlineData(RequestStatus.InProgress, "Em andamento")]
    [InlineData(RequestStatus.WaitingForResident, "Aguardando morador")]
    [InlineData(RequestStatus.WaitingForManager, "Dar andamento")]
    [InlineData(RequestStatus.WaitingForThirdParty, "Aguardando terceiro")]
    [InlineData(RequestStatus.Resolved, "Resolvida")]
    [InlineData(RequestStatus.Cancelled, "Cancelada")]
    public void WhatsApp_request_statuses_have_central_friendly_labels(
        RequestStatus status, string expected) =>
        Assert.Equal(expected, WhatsAppConversationService.FriendlyStatus(status));

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
        Assert.Contains("unidade residencial ativa", text);
        Assert.DoesNotContain("Residencial Teste", text);
        Assert.DoesNotContain("Condomínio B", text);
        Assert.Equal(WhatsAppConversationState.MainMenu,
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
    public async Task Inactive_residential_membership_has_no_residential_context()
    {
        await _host.WithDbAsync(async db =>
        {
            var membership = await db.UnitMemberships.SingleAsync(x => x.UnitId == _unitId);
            membership.End(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.inactive-unit-membership", "Menu"));

        Assert.Contains("unidade residencial ativa", Assert.Single(_fake.Messages).Text);
        Assert.DoesNotContain("identificar seu cadastro", _fake.Messages.Last().Text);
    }

    [Fact]
    public async Task Old_unknown_phone_session_recovers_after_context_is_fixed()
    {
        await SetCondominiumMembershipActive(false);
        await PostAsync(TextPayload("wamid.unknown-old", "Oi"));
        var sessionId = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.Id).SingleAsync());

        await SetCondominiumMembershipActive(true);
        await PostAsync(TextPayload("wamid.unknown-recovered", "reiniciar"));

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
    public async Task Identified_session_stays_without_residential_context_while_invalid()
    {
        await SetCondominiumMembershipActive(false);
        await PostAsync(TextPayload("wamid.unknown-still-1", "Oi"));
        var previousExpiry = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.ExpiresAt).SingleAsync());

        await PostAsync(TextPayload("wamid.unknown-still-2", "reiniciar"));

        Assert.Contains("unidade residencial ativa", _fake.Messages.Last().Text);
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Equal(WhatsAppConversationState.MainMenu, session.State);
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
    public async Task Inactive_unit_or_condominium_makes_residential_context_unavailable()
    {
        await _host.WithDbAsync(db => db.Units.Where(x => x.Id == _unitId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false)));
        await PostAsync(TextPayload("wamid.inactive-unit", "Menu"));
        Assert.Contains("unidade residencial ativa", _fake.Messages.Last().Text);

        await _host.WithDbAsync(async db =>
        {
            await db.Units.Where(x => x.Id == _unitId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, true));
            var condominium = await db.Condominiums.SingleAsync(x => x.Id == _condominiumId);
            condominium.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });
        await PostAsync(TextPayload("wamid.inactive-condominium", "Menu"));
        Assert.Contains("unidade residencial ativa", _fake.Messages.Last().Text);
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
            Assert.Empty(await db.RequestAiAnalyses.ToArrayAsync());
        });

        await PostAsync(TextPayload("wamid.edit-9", "1"));
        Assert.Contains("Conte o que aconteceu em uma mensagem", _fake.Messages.Last().Text);
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
                && item.NewStatus == RequestStatus.InProgress));
            var session = await db.WhatsAppSessions.SingleAsync();
            Assert.Null(session.DraftDescription);
            Assert.Null(session.CategoryId);
            Assert.Null(session.RequestId);
            Assert.Null(session.DraftAiProposalJson);
            Assert.Equal(WhatsAppConversationState.Ended, session.State);
            return request.Id;
        });

        Assert.Contains(requestId.ToString("N")[..8].ToUpperInvariant(), _fake.Messages.Last().Text);
        Assert.Contains("histórico completo no Comvy", _fake.Messages.Last().Text);
        Assert.Contains("https://www.comvy.com.br", _fake.Messages.Last().Text);
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

        await PostAsync(TextPayload("wamid.flow-second-1", "1"));
        await PostAsync(TextPayload("wamid.flow-second-2", "Outra lâmpada queimada"));
        await PostAsync(TextPayload("wamid.flow-second-3", "2"));
        await PostAsync(TextPayload("wamid.flow-second-4", "1"));

        Assert.Contains("Solicitação criada com sucesso", _fake.Messages.Last().Text);
        Assert.DoesNotContain("histórico completo no Comvy", _fake.Messages.Last().Text);
        Assert.Equal(2, await _host.WithDbAsync(db => db.Requests.CountAsync()));
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
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Portão danificado", "Portão danificado", "Manutenção", [], 0.8),
            null, RequestDraftAiOutcome.Succeeded, "test-model");
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
            Assert.Single(await db.RequestAiAnalyses.Where(x => x.RequestId == request.Id)
                .ToArrayAsync());
        });
    }

    [Fact]
    public async Task Request_analysis_message_and_attachments_roll_back_together()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Portão danificado", "Portão danificado", "Manutenção", [], 0.8),
            null, RequestDraftAiOutcome.Succeeded, "test-model");
        await AddCategoryAndStartAttachmentFlow();
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "image/jpeg", null);
        await PostAsync(MediaPayload("wamid.atomic-file", "atomic-file",
            "image", "image/jpeg", "foto.jpg"));
        await PostAsync(TextPayload("wamid.atomic-review", "1"));
        var storageKey = await _host.WithDbAsync(db => db.WhatsAppDraftAttachments
            .Select(x => x.StorageKey).SingleAsync());
        await _host.WithServicesAsync(services =>
        {
            services.GetRequiredService<LocalFileStorage>().Delete(storageKey);
            return Task.CompletedTask;
        });

        var response = await PostAsync(TextPayload("wamid.atomic-confirm", "1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.Empty(await db.Requests.ToArrayAsync());
            Assert.Empty(await db.RequestAiAnalyses.ToArrayAsync());
            Assert.Empty(await db.RequestMessages.ToArrayAsync());
            Assert.Empty(await db.RequestAttachments.ToArrayAsync());
            Assert.Empty(await db.WhatsAppDraftAttachments.ToArrayAsync());
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
            0.91), null, RequestDraftAiOutcome.Succeeded, "test-model");
        await AddCategoryAndStartAttachmentFlow();

        await PostAsync(TextPayload("wamid.ai-finished", "2"));

        var review = _fake.Messages.Last().Text;
        Assert.StartsWith("Revise sua solicitação antes de enviá-la.", review);
        Assert.Contains("*Título:*\n", review);
        Assert.Contains("Portão da garagem danificado", review);
        Assert.Contains("*Descrição:*\n", review);
        Assert.Contains("O portão da garagem está danificado", review);
        Assert.DoesNotContain("Categoria", review);
        Assert.DoesNotContain("Confidence", review);
        Assert.DoesNotContain("Informe desde quando", review);
        Assert.Equal("Residencial Teste", _ai.CondominiumName);
        Assert.Contains("\"Source\":\"ai\"", await _host.WithDbAsync(db =>
            db.WhatsAppSessions.Select(x => x.DraftAiProposalJson).SingleAsync()));
        var persistedProposal = await _host.WithDbAsync(db => db.WhatsAppSessions
            .Select(x => x.DraftAiProposalJson).SingleAsync());
        using var persistedJson = JsonDocument.Parse(persistedProposal!);
        var internalProposal = persistedJson.RootElement.GetProperty("Proposal");
        Assert.Equal("Manutenção", internalProposal.GetProperty("SuggestedCategory").GetString());
        Assert.Equal("Informe desde quando o problema acontece.", internalProposal
            .GetProperty("MissingInformation")[0].GetString());
        Assert.Equal(0.91, internalProposal.GetProperty("Confidence").GetDouble());
        await PostAsync(TextPayload("wamid.ai-confirmed", "1"));
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
            var analysis = await db.RequestAiAnalyses.SingleAsync(x => x.RequestId == request.Id);
            var response = RequestAiAnalysisResponse.FromEntity(analysis);
            Assert.Equal("Portão da garagem danificado", response.Title);
            Assert.Equal("O portão da garagem está danificado e não fecha corretamente.",
                response.Description);
            Assert.Equal("Manutenção", response.SuggestedCategory);
            Assert.Equal(0.91, response.Confidence);
            Assert.Equal(["Informe desde quando o problema acontece."],
                response.MissingInformation);
            Assert.Equal("test-model", response.Model);
            Assert.NotEqual(default, response.GeneratedAt);
            var completedSession = await db.WhatsAppSessions.SingleAsync();
            Assert.Null(completedSession.RequestId);
            Assert.Null(completedSession.DraftAiProposalJson);
            var requestIdIndex = db.Model.FindEntityType(typeof(RequestAiAnalysis))!
                .GetIndexes().Single(x => x.Properties
                    .Select(property => property.Name).SequenceEqual(["RequestId"]));
            Assert.True(requestIdIndex.IsUnique);
        });

        await PostAsync(TextPayload("wamid.ai-new-attendance", "Olá"));
        Assert.Single(await _host.WithDbAsync(db => db.RequestAiAnalyses.ToArrayAsync()));
    }

    [Theory]
    [InlineData("Categoria inexistente")]
    [InlineData("Categoria inativa")]
    public async Task Invalid_ai_category_uses_others(string suggestedCategory)
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
        Assert.Equal("Outros", await _host.WithDbAsync(db => db.Requests
            .Join(db.Categories, request => request.CategoryId, category => category.Id,
                (_, category) => category.Name).SingleAsync()));
    }

    [Fact]
    public async Task Invalid_ai_category_uses_active_others_category_when_available()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título organizado", "Descrição organizada", "Jardinagem", [], 0.4), null,
            RequestDraftAiOutcome.Succeeded);
        await AddCategoryAndStartAttachmentFlow();
        await _host.WithDbAsync(async db =>
        {
            db.Categories.AddRange(
                new Category(_condominiumId, "Outros", null),
                new Category(_condominiumId, "Segurança", null));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.others-review", "2"));
        await PostAsync(TextPayload("wamid.others-confirm", "1"));

        Assert.Equal("Outros", await _host.WithDbAsync(db => db.Requests
            .Join(db.Categories, request => request.CategoryId, category => category.Id,
                (_, category) => category.Name).SingleAsync()));
    }

    [Fact]
    public async Task Invalid_ai_category_creates_others_when_it_does_not_exist()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título organizado", "Descrição organizada", "Jardinagem", [], 0.4), null,
            RequestDraftAiOutcome.Succeeded);
        await AddCategoryAndStartAttachmentFlow();
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_condominiumId, "Segurança", null));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.manual-category-review", "2"));
        await PostAsync(TextPayload("wamid.manual-category-confirm", "1"));

        Assert.DoesNotContain("categoria", _fake.Messages.Last().Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Outros", await _host.WithDbAsync(db => db.Requests
            .Join(db.Categories, request => request.CategoryId, category => category.Id,
                (_, category) => category.Name).SingleAsync()));
    }

    [Fact]
    public async Task Request_without_categories_creates_and_uses_others()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título organizado", "Descrição organizada", null, [], null), null,
            RequestDraftAiOutcome.Succeeded);

        await PostAsync(TextPayload("wamid.no-categories-menu", "Oi"));
        await PostAsync(TextPayload("wamid.no-categories-open", "1"));
        await PostAsync(TextPayload(
            "wamid.no-categories-description", "Assunto sem categoria específica"));
        await PostAsync(TextPayload("wamid.no-categories-attachments", "2"));
        await PostAsync(TextPayload("wamid.no-categories-confirm", "1"));

        await _host.WithDbAsync(async db =>
        {
            var category = await db.Categories.SingleAsync();
            var request = await db.Requests.SingleAsync();
            Assert.Equal("Outros", category.Name);
            Assert.Equal(category.Id, request.CategoryId);
        });
    }

    [Fact]
    public async Task Legacy_selecting_category_session_returns_to_review_without_showing_categories()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Título organizado", "Descrição organizada", "Manutenção", [], 0.8), null);
        await AddCategoryAndStartAttachmentFlow();
        await PostAsync(TextPayload("wamid.legacy-review", "2"));
        await _host.WithDbAsync(async db =>
        {
            var session = await db.WhatsAppSessions.SingleAsync();
            session.BeginCategorySelection(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));
            await db.SaveChangesAsync();
        });

        await PostAsync(TextPayload("wamid.legacy-category-input", "1"));

        Assert.StartsWith("Revise sua solicitação", _fake.Messages.Last().Text);
        Assert.DoesNotContain("categoria", _fake.Messages.Last().Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WhatsAppConversationState.ReviewingNewRequest,
            await _host.WithDbAsync(db => db.WhatsAppSessions.Select(x => x.State).SingleAsync()));
        Assert.Empty(await _host.WithDbAsync(db => db.Requests.ToArrayAsync()));
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
    public async Task Ai_failure_uses_traditional_review_and_others(string error)
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
            "2 - Corrigir relato\n" +
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
        Assert.DoesNotContain("categoria", _fake.Messages.Last().Text,
            StringComparison.OrdinalIgnoreCase);

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            Assert.Equal("Solicitação recebida pelo WhatsApp", request.Title);
            Assert.Equal("Portão danificado", request.Description);
            Assert.Equal("Outros", await db.Categories
                .Where(x => x.Id == request.CategoryId).Select(x => x.Name).SingleAsync());
            Assert.Equal("Portão danificado", await db.RequestMessages
                .Where(x => x.RequestId == request.Id).Select(x => x.Content).SingleAsync());
            Assert.Empty(await db.RequestAiAnalyses.ToArrayAsync());
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

    [Fact]
    public async Task Audio_report_is_transcribed_reviewed_and_persisted_with_original_file()
    {
        _ai.Result = new(true, new RequestDraftAiProposal(
            "Barulho no elevador", "O elevador está fazendo barulho.",
            "Manutenção", ["Informar o horário."], 0.9), null,
            RequestDraftAiOutcome.Succeeded, "draft-model");
        _transcription.Result = new(true, "O elevador está fazendo barulho.", "succeeded");
        await AddCategoryAndStartAudioFlow("valid-audio");
        await PostAsync(MediaPayload(
            "wamid.valid-audio", "valid-audio", "audio", "audio/ogg; codecs=opus"));

        Assert.Equal(1, _transcription.Calls);
        Assert.Equal(1, _fake.DownloadCalls);
        await PostAsync(TextPayload("wamid.audio-finished", "2"));
        Assert.Equal(1, _ai.Calls);
        Assert.Contains("Revise sua solicitação", _fake.Messages.Last().Text);
        await PostAsync(TextPayload("wamid.audio-confirm", "1"));
        await PostAsync(TextPayload("wamid.audio-confirm", "1"));

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync();
            var message = await db.RequestMessages.SingleAsync(x => x.RequestId == request.Id);
            var attachment = await db.RequestAttachments.SingleAsync(x => x.RequestId == request.Id);
            Assert.Equal("O elevador está fazendo barulho.", message.Content);
            Assert.Equal(message.Id, attachment.RequestMessageId);
            Assert.Equal("audio/ogg", attachment.ContentType);
            Assert.Single(await db.RequestAiAnalyses.Where(x => x.RequestId == request.Id)
                .ToArrayAsync());
        });
    }

    [Fact]
    public async Task Audio_can_be_corrected_with_text_while_other_attachments_remain()
    {
        await AddCategoryAndStartAudioFlow("audio-to-text");
        _fake.Media = new WhatsAppMediaResult(true, [4, 5, 6], "image/jpeg", null);
        await PostAsync(MediaPayload("wamid.audio-photo", "audio-photo",
            "image", "image/jpeg", "foto.jpg"));
        await PostAsync(TextPayload("wamid.audio-first-review", "1"));
        await PostAsync(TextPayload("wamid.audio-correct", "2"));

        Assert.Single(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments
            .Where(x => x.ContentType == "image/jpeg").ToArrayAsync()));
        Assert.Empty(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments
            .Where(x => x.ContentType.StartsWith("audio/")).ToArrayAsync()));
        await PostAsync(TextPayload("wamid.audio-new-text", "Novo relato escrito."));
        await PostAsync(TextPayload("wamid.audio-new-text-review", "2"));
        Assert.Equal(2, _ai.Calls);
        Assert.Single(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
    }

    [Fact]
    public async Task Text_can_be_corrected_with_audio_and_audio_with_another_audio()
    {
        await AddCategoryAndStartAttachmentFlow();
        await PostAsync(TextPayload("wamid.text-first-review", "2"));
        await PostAsync(TextPayload("wamid.text-correct", "2"));
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);
        await PostAsync(MediaPayload("wamid.text-to-audio", "text-to-audio", "audio", "audio/ogg"));
        await PostAsync(TextPayload("wamid.second-review", "2"));
        await PostAsync(TextPayload("wamid.audio-correct-again", "2"));
        await PostAsync(MediaPayload("wamid.audio-to-audio", "audio-to-audio", "audio", "audio/ogg"));

        Assert.Equal(2, _transcription.Calls);
        Assert.Single(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments
            .Where(x => x.ContentType.StartsWith("audio/")).ToArrayAsync()));
    }

    [Theory]
    [InlineData("provider_error", "audio_transcription_failed")]
    [InlineData("timeout", "audio_transcription_timeout")]
    public async Task Audio_transcription_failure_does_not_create_request(
        string code, string processingResult)
    {
        _transcription.Result = new(false, null, code);
        await AddCategoryAndOpenDescription();
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);
        await PostAsync(MediaPayload($"wamid.audio-failure-{code}", code, "audio", "audio/ogg"));

        Assert.Contains("Não consegui compreender o áudio", _fake.Messages.Last().Text);
        Assert.Equal(processingResult, await _host.WithDbAsync(db => db.WhatsAppInboundMessages
            .Where(x => x.ExternalMessageId == $"wamid.audio-failure-{code}")
            .Select(x => x.ProcessingResult).SingleAsync()));
        Assert.Empty(await _host.WithDbAsync(db => db.Requests.ToArrayAsync()));
        Assert.Empty(await _host.WithDbAsync(db => db.WhatsAppDraftAttachments.ToArrayAsync()));
        await PostAsync(TextPayload($"wamid.audio-write-{code}", "2"));
        Assert.Contains("Conte o que aconteceu", _fake.Messages.Last().Text);
        await PostAsync(TextPayload($"wamid.audio-written-{code}", "Relato escrito após a falha."));
        Assert.Equal(WhatsAppConversationState.CollectingAttachments,
            await _host.WithDbAsync(db => db.WhatsAppSessions
                .Select(x => x.State).SingleAsync()));
    }

    [Theory]
    [InlineData("audio/wav", 3, "não é suportado")]
    [InlineData("audio/ogg", AttachmentPolicy.MaximumFileSize + 1, "no máximo 15 MB")]
    public async Task Invalid_or_oversized_audio_is_rejected(
        string contentType, int size, string expected)
    {
        await AddCategoryAndOpenDescription();
        _fake.Media = new WhatsAppMediaResult(true, new byte[size], contentType, null);
        await PostAsync(MediaPayload($"wamid.audio-rejected-{size}", "rejected", "audio", contentType));
        Assert.Contains(expected, _fake.Messages.Last().Text);
        Assert.Equal(0, _transcription.Calls);
    }

    private async Task AddCategoryAndStartAudioFlow(string id)
    {
        await AddCategoryAndOpenDescription();
        _fake.Media = new WhatsAppMediaResult(true, [1, 2, 3], "audio/ogg", null);
        await PostAsync(MediaPayload($"wamid.{id}", id, "audio", "audio/ogg"));
    }

    private async Task AddCategoryAndOpenDescription()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Categories.Add(new Category(_condominiumId, "Manutenção", null));
            await db.SaveChangesAsync();
        });
        await PostAsync(TextPayload($"wamid.audio-menu-{Guid.NewGuid():N}", "Oi"));
        await PostAsync(TextPayload($"wamid.audio-open-{Guid.NewGuid():N}", "1"));
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

    private static string InteractiveReplyPayload(
        string id, string replyId, string title) =>
        JsonSerializer.Serialize(new
        {
            entry = new[] { new { changes = new[] { new { value = new
            {
                messages = new[] { new
                {
                    from = "5511999990001", id, timestamp = "1785236400",
                    type = "interactive",
                    interactive = new
                    {
                        type = "button_reply",
                        button_reply = new { id = replyId, title }
                    }
                } }
            } } } } }
        });

    private static string TemplateQuickReplyPayload(
        string id, string replyId, string title, string? contextId = null,
        string phone = "5511999990001") =>
        JsonSerializer.Serialize(new
        {
            entry = new[] { new { changes = new[] { new { value = new
            {
                messages = new[] { new
                {
                    from = phone, id, timestamp = "1785236400",
                    type = "button",
                    context = contextId is null ? null : new { id = contextId },
                    button = new { payload = replyId, text = title }
                } }
            } } } } }
        });

    private static string MediaPayload(
        string id,
        string mediaId,
        string type,
        string mimeType,
        string? fileName = null) => type switch
        {
            "audio" => JsonSerializer.Serialize(new
            {
                entry = new[] { new { changes = new[] { new { value = new { messages = new object[]
                {
                    new { from = "5511999990001", id, timestamp = "1785236400", type,
                        audio = new { id = mediaId, mime_type = mimeType, filename = fileName } }
                } } } } } }
            }),
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

    private async Task<Guid[]> SeedOwnRequests(int count)
    {
        return await _host.WithDbAsync(async db =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(
                x => x.CondominiumId == _condominiumId);
            if (category is null)
            {
                category = new Category(_condominiumId, "Manutenção", null);
                db.Categories.Add(category);
            }
            var requests = Enumerable.Range(1, count).Select(index =>
                new CondoLink.Domain.Entities.Request(
                    _condominiumId, _userId, _unitId, category.Id,
                    $"Solicitação {index}", $"Descrição {index}",
                    RequestSource.WhatsApp)).ToArray();
            db.Requests.AddRange(requests);
            await db.SaveChangesAsync();
            var baseTime = new DateTime(2026, 7, 31, 18, 0, 0, DateTimeKind.Utc);
            for (var index = 0; index < requests.Length; index++)
            {
                var requestId = requests[index].Id;
                var updatedAt = index == 0
                    ? baseTime.AddDays(1)
                    : baseTime.AddMinutes(index);
                await db.Requests.Where(x => x.Id == requestId).ExecuteUpdateAsync(setters =>
                    setters.SetProperty(x => x.UpdatedAt, updatedAt)
                        .SetProperty(x => x.Status, index == 0
                            ? RequestStatus.Resolved : RequestStatus.Open));
            }
            return requests.Select(x => x.Id).ToArray();
        });
    }

    private async Task SeedInaccessibleRequests()
    {
        await _host.WithDbAsync(async db =>
        {
            var other = CoreTestSeed.User("Outra Moradora", "other-list@example.com");
            var foreignCondominium = new Condominium("Residencial Externo", null, null);
            var localCategory = await db.Categories.FirstAsync(
                x => x.CondominiumId == _condominiumId);
            var foreignCategory = new Category(foreignCondominium.Id, "Outros", null);
            db.AddRange(other, foreignCondominium, foreignCategory,
                new CondoLink.Domain.Entities.Request(
                    _condominiumId, other.Id, null, localCategory.Id,
                    "Solicitação externa", "Não pode aparecer"),
                new CondoLink.Domain.Entities.Request(
                    foreignCondominium.Id, _userId, null, foreignCategory.Id,
                    "Outro condomínio", "Não pode aparecer"));
            await db.SaveChangesAsync();
        });
    }

    private sealed class FakeWhatsAppClient : IWhatsAppClient
    {
        public List<(string Phone, string Text)> Messages { get; } = [];
        public bool Fail { get; set; }
        public int DownloadCalls { get; private set; }
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
            CancellationToken cancellationToken)
        {
            DownloadCalls++;
            return Task.FromResult(Media);
        }

        public Task<WhatsAppSendResult> SendTemplateAsync(
            string phoneNumber,
            string templateName,
            string language,
            IReadOnlyList<string> bodyParameters,
            IReadOnlyList<string> quickReplyPayloads,
            CancellationToken cancellationToken,
            string? bodyParameterName = null) =>
            SendTextAsync(phoneNumber, $"template:{templateName}:{language}",
                cancellationToken);
    }

    private sealed class FakeRequestDraftAiService : IRequestDraftAiService
    {
        public RequestDraftAiResult Result { get; set; } =
            new(false, null, "not configured");
        public int Calls { get; private set; }
        public string? CondominiumName { get; private set; }

        public Task<RequestDraftAiResult> ProposeAsync(string originalReport,
            IReadOnlyCollection<string> activeCategories,
            string condominiumName,
            CancellationToken cancellationToken)
        {
            Calls++;
            CondominiumName = condominiumName;
            return Task.FromResult(Result);
        }

        public Task<ResidentStatusSynthesisResult> SynthesizeResidentStatusAsync(
            string requestTitle, string newStatus, string reason,
            CancellationToken cancellationToken) => Task.FromResult(
                new ResidentStatusSynthesisResult(false, null, "not_configured"));
    }

    private sealed class FakeAudioTranscriptionService
        : IWhatsAppAudioTranscriptionService
    {
        public AudioTranscriptionResult Result { get; set; } =
            new(true, "Transcrição do relato em áudio.", "succeeded");
        public int Calls { get; private set; }

        public Task<AudioTranscriptionResult> TranscribeAsync(
            ReadOnlyMemory<byte> audio, string fileName, string contentType,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeResidentReplyAiService : IResidentReplyAiService
    {
        public ResidentReplyAiResult Result { get; set; } = new(false, null);
        public Task<ResidentReplyAiResult> OrganizeAsync(string question,
            string originalAnswer, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FakeAdministrativeResidentExtractionService
        : IAdministrativeResidentExtractionService
    {
        public AdministrativeResidentExtractionResult Result { get; set; } =
            new(false, null, "not_configured");
        public Task<AdministrativeResidentExtractionResult> ExtractAsync(
            string message, AdministrativeResidentExtraction? current,
            CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class FakeAdministrativeResidentLookupExtractionService
        : IAdministrativeResidentLookupExtractionService
    {
        public AdministrativeResidentLookupExtractionResult Result { get; set; } =
            new(true, new("unknown", null, null, null, null, []), "succeeded");
        public Task<AdministrativeResidentLookupExtractionResult> ExtractAsync(
            string message, AdministrativeResidentLookupExtraction? current,
            CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class FakeAdministrativeResidentMutationExtractionService
        : IAdministrativeResidentMutationExtractionService
    {
        public AdministrativeResidentMutationExtractionResult Result { get; set; } =
            new(true, new("unknown", null, null, null, null, null, null, null),
                "succeeded");
        public Task<AdministrativeResidentMutationExtractionResult> ExtractAsync(
            string message, AdministrativeResidentMutationExtraction? current,
            CancellationToken ct) => Task.FromResult(Result);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
