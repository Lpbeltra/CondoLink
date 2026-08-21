using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// Fan-out rules: who gets told, and who explicitly does not.
/// </summary>
public sealed class NotificationServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private NotificationService _service = null!;

    private Condominium _condominium = null!;
    private Category _category = null!;
    private ApplicationUser _resident = null!;
    private ApplicationUser _managerA = null!;
    private ApplicationUser _managerB = null!;
    private ApplicationUser _otherResident = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _service = new NotificationService(_db);

        _condominium = new Condominium("Alfa", null, null);
        _resident = User("Morador", "morador@example.com");
        _otherResident = User("Vizinho", "vizinho@example.com");
        _managerA = User("Sindico A", "a@example.com");
        _managerB = User("Sindico B", "b@example.com");
        _category = new Category(_condominium.Id, "Manutenção", null);

        _db.AddRange(_condominium, _resident, _otherResident, _managerA, _managerB, _category);
        AddMembership(_resident.Id, CondominiumRole.Resident);
        AddMembership(_otherResident.Id, CondominiumRole.Resident);
        AddMembership(_managerA.Id, CondominiumRole.Manager);
        AddMembership(_managerB.Id, CondominiumRole.Manager);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ---- request created ----

    [Fact]
    public async Task Creating_a_request_notifies_every_manager()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        var recipients = await RecipientsAsync();
        Assert.Equal(2, recipients.Length);
        Assert.Contains(_managerA.Id, recipients);
        Assert.Contains(_managerB.Id, recipients);
    }

    [Fact]
    public async Task Creating_a_request_does_not_notify_the_author()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.DoesNotContain(_resident.Id, await RecipientsAsync());
    }

    [Fact]
    public async Task Creating_a_request_does_not_notify_unrelated_residents()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.DoesNotContain(_otherResident.Id, await RecipientsAsync());
    }

    [Fact]
    public async Task A_manager_opening_a_request_is_not_notified_about_it()
    {
        var request = await AddRequestAsync(_managerA.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        var recipients = await RecipientsAsync();
        Assert.Equal([_managerB.Id], recipients);
    }

    [Fact]
    public async Task Revoked_managers_are_not_notified()
    {
        var membership = await _db.CondominiumMemberships
            .SingleAsync(item => item.UserId == _managerB.Id);
        var role = await _db.CondominiumMembershipRoles
            .SingleAsync(item => item.CondominiumMembershipId == membership.Id);
        role.Deactivate();
        await _db.SaveChangesAsync();

        var request = await AddRequestAsync(_resident.Id);
        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.Equal([_managerA.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Notification_records_the_request_for_deep_linking()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        var notification = await _db.Notifications.AsNoTracking().FirstAsync();
        Assert.Equal(request.Id, notification.RequestId);
        Assert.Equal(_condominium.Id, notification.CondominiumId);
        Assert.Equal(NotificationType.RequestCreated, notification.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task New_request_enqueues_one_manager_whatsapp_with_operational_content()
    {
        _managerA.Update("Síndico A", "11988887777");
        var managerBMembership = await _db.CondominiumMemberships
            .SingleAsync(x => x.UserId == _managerB.Id);
        (await _db.CondominiumMembershipRoles.SingleAsync(x =>
            x.CondominiumMembershipId == managerBMembership.Id)).Deactivate();
        var block = new CondominiumBlock(_condominium.Id, "Bloco 1");
        var unit = new Unit(_condominium.Id, "1201", block.Id, null, null);
        _db.AddRange(block, unit);
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id, "TAG da garagem", unit.Id);
        var options = new WhatsAppOptions { Enabled = true };
        options.Templates.ManagerNewRequest.Name = "manager_new_request";
        options.Templates.ManagerNewRequest.Language = "pt_BR";
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(options),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);

        await service.NotifyRequestCreatedAsync(request, _category.Name, default);
        await service.NotifyRequestCreatedAsync(request, _category.Name, default);

        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal(_managerA.Id, outbound.UserId);
        Assert.NotEqual(_resident.Id, outbound.UserId);
        Assert.Equal(WhatsAppNotificationType.ManagerNewRequest,
            outbound.NotificationType);
        Assert.Equal("manager_new_request", outbound.TemplateName);
        Assert.Equal(WhatsAppSendMode.Template, outbound.SendMode);
        Assert.Contains("*Nova solicitação recebida*", outbound.Content);
        Assert.Contains(_condominium.Name, outbound.Content);
        Assert.Contains(_resident.FullName, outbound.Content);
        Assert.Contains("Apto 1201 · Bloco 1", outbound.Content);
        Assert.Contains("Assunto: TAG da garagem", outbound.Content);
        Assert.DoesNotContain("Descrição", outbound.Content);
    }

    [Fact]
    public async Task Missing_active_manager_does_not_create_manager_outbound()
    {
        foreach (var role in await _db.CondominiumMembershipRoles
            .Where(x => x.Role == CondominiumRole.Manager).ToArrayAsync())
            role.Deactivate();
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id);
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions { Enabled = true }),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);

        await service.NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.Empty(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
        Assert.True(await _db.Requests.AnyAsync(x => x.Id == request.Id));
    }

    [Fact]
    public async Task Multiple_active_managers_do_not_choose_a_whatsapp_recipient()
    {
        _managerA.Update("Síndico A", "11988887777");
        _managerB.Update("Síndico B", "11977776666");
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id);
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions { Enabled = true }),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);

        await new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance)
            .NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.Empty(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
        Assert.True(await _db.Requests.AnyAsync(x => x.Id == request.Id));
    }

    [Fact]
    public async Task Inactive_manager_is_not_a_manager_notification_candidate()
    {
        _managerA.SetActiveStatus(false);
        var membershipB = await _db.CondominiumMemberships
            .SingleAsync(x => x.UserId == _managerB.Id);
        (await _db.CondominiumMembershipRoles.SingleAsync(x =>
            x.CondominiumMembershipId == membershipB.Id)).Deactivate();
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyRequestCreatedAsync(request, _category.Name, default);

        Assert.Empty(await RecipientsAsync());
    }

    [Fact]
    public async Task Manager_notification_is_global_and_does_not_require_unit_membership_or_residential_preference()
    {
        _managerA.Update("Síndico A", "11988887777");
        _managerA.SetReceiveWhatsAppUpdates(false);
        var membershipB = await _db.CondominiumMemberships
            .SingleAsync(x => x.UserId == _managerB.Id);
        (await _db.CondominiumMembershipRoles.SingleAsync(x =>
            x.CondominiumMembershipId == membershipB.Id)).Deactivate();
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id);
        var options = new WhatsAppOptions { Enabled = true };
        options.Templates.ManagerNewRequest.Name = "manager_new_request";
        options.Templates.ManagerNewRequest.Language = "pt_BR";
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(options),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);

        await new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance)
            .NotifyRequestCreatedAsync(request, _category.Name, default);

        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal(_managerA.Id, outbound.UserId);
        Assert.Equal(WhatsAppOutboundStatus.Pending, outbound.Status);
        Assert.False(await _db.UnitMemberships.AnyAsync(x =>
            x.UserId == _managerA.Id));
    }

    [Fact]
    public async Task Same_manager_receives_each_condominium_with_explicit_context()
    {
        _managerA.Update("Síndico A", "11988887777");
        var membershipB = await _db.CondominiumMemberships
            .SingleAsync(x => x.UserId == _managerB.Id);
        (await _db.CondominiumMembershipRoles.SingleAsync(x =>
            x.CondominiumMembershipId == membershipB.Id)).Deactivate();
        var second = new Condominium("Residencial Beta", null, null);
        var secondCategory = new Category(second.Id, "Portaria", null);
        _db.AddRange(second, secondCategory);
        var secondMembership = new CondominiumMembership(_managerA.Id, second.Id);
        _db.Add(secondMembership);
        _db.Add(new CondominiumMembershipRole(secondMembership.Id,
            CondominiumRole.Manager));
        await _db.SaveChangesAsync();
        var firstRequest = await AddRequestAsync(_resident.Id, "Garagem");
        var secondRequest = new DomainRequest(second.Id, _resident.Id, null,
            secondCategory.Id, "Visitante", "Descrição");
        _db.Add(secondRequest);
        await _db.SaveChangesAsync();
        var options = new WhatsAppOptions { Enabled = true };
        options.Templates.ManagerNewRequest.Name = "manager_new_request";
        options.Templates.ManagerNewRequest.Language = "pt_BR";
        var service = new NotificationService(_db,
            new WhatsAppNotificationDispatcher(_db, Options.Create(options),
                NullLogger<WhatsAppNotificationDispatcher>.Instance),
            NullLogger<NotificationService>.Instance);

        await service.NotifyRequestCreatedAsync(firstRequest, _category.Name, default);
        await service.NotifyRequestCreatedAsync(secondRequest, secondCategory.Name, default);

        var rows = await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, x => Assert.Equal(_managerA.Id, x.UserId));
        Assert.Contains(_condominium.Name,
            rows.Single(x => x.RequestId == firstRequest.Id).Content);
        Assert.Contains(second.Name,
            rows.Single(x => x.RequestId == secondRequest.Id).Content);
    }

    [Theory]
    [InlineData(false, true, "Telefone inválido.")]
    [InlineData(true, false, "Template não configurado.")]
    public async Task Missing_delivery_configuration_skips_whatsapp_without_losing_request(
        bool hasPhone, bool hasTemplate, string expectedReason)
    {
        if (hasPhone) _managerA.Update("Síndico A", "11988887777");
        var membershipB = await _db.CondominiumMemberships
            .SingleAsync(x => x.UserId == _managerB.Id);
        (await _db.CondominiumMembershipRoles.SingleAsync(x =>
            x.CondominiumMembershipId == membershipB.Id)).Deactivate();
        await _db.SaveChangesAsync();
        var request = await AddRequestAsync(_resident.Id);
        var options = new WhatsAppOptions { Enabled = true };
        if (hasTemplate)
        {
            options.Templates.ManagerNewRequest.Name = "manager_new_request";
            options.Templates.ManagerNewRequest.Language = "pt_BR";
        }
        var service = new NotificationService(_db,
            new WhatsAppNotificationDispatcher(_db, Options.Create(options),
                NullLogger<WhatsAppNotificationDispatcher>.Instance),
            NullLogger<NotificationService>.Instance);

        await service.NotifyRequestCreatedAsync(request, _category.Name, default);

        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal(WhatsAppOutboundStatus.Skipped, outbound.Status);
        Assert.Equal(expectedReason, outbound.LastErrorDescription);
        Assert.True(await _db.Requests.AnyAsync(x => x.Id == request.Id));
    }

    [Fact]
    public void Manager_new_request_content_works_without_block()
    {
        Assert.Equal("*Nova solicitação recebida*\n\nResidencial Monticello\n"
            + "Tatiana Custódio · Apto 1201\nAssunto: TAG da garagem",
            NotificationService.ManagerNewRequestContent(
                "Residencial Monticello", "Tatiana Custódio", "1201", null,
                "TAG da garagem"));
    }

    // ---- status changed ----

    [Fact]
    public async Task Status_change_notifies_the_author()
    {
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);

        await _service.NotifyStatusChangedAsync(
            request, RequestStatus.InProgress, _managerA.Id, default);

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(_resident.Id, notification.RecipientUserId);
        Assert.Equal(NotificationType.RequestStatusChanged, notification.Type);
        Assert.Equal("Estamos aguardando uma etapa externa para continuar seu atendimento.",
            notification.Body);
    }

    [Fact]
    public async Task Status_change_made_by_the_author_still_creates_the_resident_notification()
    {
        var request = await AddRequestAsync(_managerA.Id);
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);

        await _service.NotifyStatusChangedAsync(
            request, RequestStatus.InProgress, _managerA.Id, default,
            reason: "Aguardando fornecedor");

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(_managerA.Id, notification.RecipientUserId);
        Assert.Equal("Aguardando fornecedor", notification.Body);
    }

    [Fact]
    public async Task Waiting_for_third_party_preserves_approved_text_and_enqueues_once()
    {
        var ai = new FakeAi(new(true,
            "Estamos aguardando a emissão da TAG para a portaria. Você será avisado quando houver novidade.",
            "succeeded", "model-test"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id, "TAG de acesso");
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);
        var historyId = Guid.NewGuid();

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, historyId, "Solicitada a TAG para a portaria");
        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, historyId, "Solicitada a TAG para a portaria");

        Assert.Equal(0, ai.SynthesisCalls);
        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal("Solicitada a TAG para a portaria", outbound.Content);
        Assert.DoesNotContain("prazo", outbound.Content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WhatsAppNotificationType.StatusChanged, outbound.NotificationType);
    }

    [Fact]
    public async Task Waiting_for_third_party_ai_failure_uses_deterministic_fallback()
    {
        var ai = new FakeAi(new(false, null, "provider_error"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), "Fornecedor acionado");

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal("Fornecedor acionado", notification.Body);
        Assert.Single(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Resolved_status_with_reason_does_not_use_ai_synthesis()
    {
        var status = RequestStatus.Resolved;
        const string heading = "*Seu atendimento foi finalizado.*";
        var ai = new FakeAi(new(true,
            "A TAG está disponível na portaria.",
            "succeeded", "model-test"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id);
        if (status == RequestStatus.Resolved)
            request.ChangeStatus(RequestStatus.WaitingForResidentClosure,
                DateTime.UtcNow);
        request.ChangeStatus(status, DateTime.UtcNow);

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), "TAG entregue na portaria");

        Assert.Equal(0, ai.SynthesisCalls);
        Assert.Equal($"{heading}\n\nTAG entregue na portaria",
            Assert.Single(await _db.Notifications.AsNoTracking().ToArrayAsync()).Body);
    }

    [Fact]
    public async Task Cancelled_status_preserves_manager_comment_without_ai()
    {
        var ai = new FakeAi(new(true, "changed content", "succeeded", "model-test"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.Cancelled, DateTime.UtcNow);
        const string reason = "Esta solicitacao foi aberta em duplicidade.";

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), reason);

        Assert.Equal(0, ai.SynthesisCalls);
        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal(WhatsAppNotificationType.RequestCancelled,
            outbound.NotificationType);
        Assert.Equal("*Seu atendimento foi cancelado.*\n\n" + reason,
            outbound.Content);
    }

    [Fact]
    public void Status_notification_content_follows_the_whatsapp_template_with_optional_comment()
    {
        var withoutComment = NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress,
            RequestStatus.WaitingForThirdParty, null);
        var withComment = NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress,
            RequestStatus.WaitingForResident, "Envie uma foto.");

        Assert.Equal("Estamos aguardando uma etapa externa para continuar seu atendimento.",
            withoutComment);
        Assert.DoesNotContain("Contexto", withoutComment);
        Assert.Contains("Contexto: Envie uma foto.", withComment);
    }

    [Fact]
    public void Status_notification_uses_resident_facing_language_without_internal_labels()
    {
        Assert.Contains("etapa externa", NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, RequestStatus.WaitingForThirdParty, null));
        Assert.Contains("finalizado", NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, RequestStatus.Resolved, null));
        Assert.DoesNotContain("WaitingFor", NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, RequestStatus.WaitingForThirdParty, null));
    }

    [Fact]
    public void Waiting_for_resident_maps_only_to_specific_information_request()
    {
        Assert.Equal(WhatsAppNotificationType.InformationRequested,
            NotificationService.StatusNotificationType(
                RequestStatus.InProgress, RequestStatus.WaitingForResident));
        Assert.Equal(WhatsAppNotificationType.StatusChanged,
            NotificationService.StatusNotificationType(
                RequestStatus.InProgress, RequestStatus.WaitingForThirdParty));
        Assert.Equal(WhatsAppNotificationType.StatusChanged,
            NotificationService.StatusNotificationType(
                RequestStatus.InProgress, RequestStatus.WaitingForResidentClosure));
        var content = NotificationService.StatusChangedContent("Tag",
            RequestStatus.InProgress, RequestStatus.WaitingForResidentClosure,
            "Tag entregue na portaria.");
        Assert.Contains("Tag entregue na portaria.", content);
        Assert.Contains("1 - Sim, finalizar atendimento", content);
        Assert.Contains("2 - Ainda tenho uma dúvida", content);
        Assert.DoesNotContain("Seu atendimento foi finalizado", content);
    }

    [Fact]
    public async Task Waiting_for_resident_closure_preserves_comment_and_choices_without_ai_synthesis()
    {
        var ai = new FakeAi(new(true,
            "*Seu atendimento foi finalizado.*",
            "succeeded", "model-test"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForResidentClosure,
            DateTime.UtcNow);

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), "Tag entregue na portaria.");

        Assert.Equal(0, ai.SynthesisCalls);
        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Contains("Tag entregue na portaria.", outbound.Content);
        Assert.Contains("1 - Sim, finalizar atendimento", outbound.Content);
        Assert.Contains("2 - Ainda tenho uma dúvida", outbound.Content);
        Assert.Contains("administração", outbound.Content);
        Assert.Contains("solicitação", outbound.Content);
        Assert.Contains("concluída", outbound.Content);
        Assert.DoesNotContain("Ã", outbound.Content);
        Assert.DoesNotContain("Seu atendimento foi finalizado", outbound.Content);
    }

    [Theory]
    [InlineData(RequestStatus.WaitingForResident, true)]
    [InlineData(RequestStatus.WaitingForThirdParty, true)]
    [InlineData(RequestStatus.WaitingForResidentClosure, true)]
    [InlineData(RequestStatus.Resolved, true)]
    [InlineData(RequestStatus.Cancelled, true)]
    [InlineData(RequestStatus.WaitingForManager, false)]
    [InlineData(RequestStatus.InProgress, false)]
    public void Only_resident_relevant_statuses_generate_notifications(
        RequestStatus status, bool expected)
    {
        Assert.Equal(expected, NotificationService.ShouldNotifyResident(
            RequestStatus.InProgress, status));
    }

    [Theory]
    [InlineData(RequestStatus.WaitingForResident)]
    [InlineData(RequestStatus.WaitingForThirdParty)]
    [InlineData(RequestStatus.WaitingForManager)]
    public async Task In_progress_never_creates_resident_noise(RequestStatus previousStatus)
    {
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher);
        var request = await AddRequestAsync(_resident.Id);

        await service.NotifyStatusChangedAsync(request, previousStatus,
            _managerA.Id, default, Guid.NewGuid());

        Assert.Empty(await _db.Notifications.AsNoTracking().ToArrayAsync());
        Assert.Empty(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Waiting_for_manager_does_not_create_resident_noise()
    {
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForManager, DateTime.UtcNow);

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid());

        Assert.Empty(await _db.Notifications.AsNoTracking().ToArrayAsync());
        Assert.Empty(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
    }

    [Theory]
    [InlineData(RequestStatus.Resolved, "*Seu atendimento foi finalizado.*\n\nA administração concluiu esta solicitação.")]
    [InlineData(RequestStatus.Cancelled, "*Seu atendimento foi cancelado.*\n\nA administração encerrou esta solicitação.")]
    public void Terminal_statuses_have_deterministic_resident_fallbacks(
        RequestStatus status, string expected)
    {
        Assert.Equal(expected, NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, status, null));
    }

    [Fact]
    public async Task Terminal_ai_failure_uses_deterministic_fallback()
    {
        var ai = new FakeAi(new(false, null, "provider_error"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForResidentClosure, DateTime.UtcNow);
        request.ChangeStatus(RequestStatus.Resolved, DateTime.UtcNow.AddMilliseconds(1));

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), "O reparo foi concluído");

        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Equal("*Seu atendimento foi finalizado.*\n\n"
            + "O reparo foi concluído", outbound.Content);
    }

    // ---- messages ----

    [Fact]
    public async Task A_manager_reply_notifies_the_author_only()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyMessageAsync(
            request.Id, _condominium.Id, _resident.Id, request.Title,
            _managerA.Id, "Estamos verificando.", default);

        Assert.Equal([_resident.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task An_author_reply_notifies_the_managers_only()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyMessageAsync(
            request.Id, _condominium.Id, _resident.Id, request.Title,
            _resident.Id, "Alguma novidade?", default);

        var recipients = await RecipientsAsync();
        Assert.Equal(2, recipients.Length);
        Assert.Contains(_managerA.Id, recipients);
        Assert.Contains(_managerB.Id, recipients);
        Assert.DoesNotContain(_resident.Id, recipients);
    }

    [Fact]
    public async Task A_spontaneous_whatsapp_update_has_its_own_manager_notification()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyMessageAsync(
            request.Id, _condominium.Id, _resident.Id, request.Title,
            _resident.Id, "O vazamento aumentou.", default,
            channel: MessageChannel.WhatsAppResidentUpdate);

        var notifications = await _db.Notifications.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, notifications.Length);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(NotificationType.ResidentRequestUpdated, notification.Type);
            Assert.Equal("Morador atualizou a solicitação", notification.Title);
            Assert.Null(notification.ReadAt);
        });
    }

    [Fact]
    public async Task A_requested_whatsapp_reply_remains_a_regular_message_notification()
    {
        var request = await AddRequestAsync(_resident.Id);

        await _service.NotifyMessageAsync(
            request.Id, _condominium.Id, _resident.Id, request.Title,
            _resident.Id, "Segue a informação solicitada.", default,
            channel: MessageChannel.WhatsApp);

        var notifications = await _db.Notifications.AsNoTracking().ToArrayAsync();
        Assert.All(notifications, notification =>
            Assert.Equal(NotificationType.RequestMessageReceived, notification.Type));
    }
    [Fact]
    public async Task A_manager_replying_to_their_own_request_notifies_the_other_manager()
    {
        var request = await AddRequestAsync(_managerA.Id);

        await _service.NotifyMessageAsync(
            request.Id, _condominium.Id, _managerA.Id, request.Title,
            _managerA.Id, "Nota interna.", default);

        Assert.Equal([_managerB.Id], await RecipientsAsync());
    }

    // ---- read state ----

    [Fact]
    public async Task Marking_as_read_is_idempotent()
    {
        var notification = new Notification(
            _resident.Id, _condominium.Id, NotificationType.RequestCreated, "T", "B");
        var first = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        notification.MarkAsRead(first);
        notification.MarkAsRead(first.AddHours(3));

        Assert.Equal(first, notification.ReadAt);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public async Task Marking_as_unread_clears_the_timestamp()
    {
        var notification = new Notification(
            _resident.Id, _condominium.Id, NotificationType.RequestCreated, "T", "B");
        notification.MarkAsRead(DateTime.UtcNow);

        notification.MarkAsUnread();

        Assert.Null(notification.ReadAt);
        Assert.False(notification.IsRead);
    }

    // ---- body construction ----

    [Fact]
    public void Long_text_is_shortened_to_fit_the_column()
    {
        var shortened = NotificationService.Shorten(new string('a', 400));

        Assert.True(shortened.Length <= 160);
        Assert.EndsWith("…", shortened);
    }

    [Fact]
    public void Short_text_is_left_intact()
    {
        Assert.Equal("Vazamento", NotificationService.Shorten("  Vazamento  "));
    }

    [Fact]
    public async Task A_very_long_title_and_body_still_persist()
    {
        var request = await AddRequestAsync(_resident.Id, new string('t', 200));

        await _service.NotifyRequestCreatedAsync(
            request, new string('c', 200), default);

        var notification = await _db.Notifications.AsNoTracking().FirstAsync();
        Assert.True(notification.Body.Length <= 500);
        Assert.True(notification.Title.Length <= 160);
    }

    // ---- validation ----

    [Fact]
    public void Notification_requires_a_recipient()
    {
        Assert.Throws<ArgumentException>(() => new Notification(
            Guid.Empty, _condominium.Id, NotificationType.RequestCreated, "T", "B"));
    }

    [Fact]
    public void Notification_requires_a_condominium()
    {
        Assert.Throws<ArgumentException>(() => new Notification(
            _resident.Id, Guid.Empty, NotificationType.RequestCreated, "T", "B"));
    }

    [Fact]
    public void Notification_rejects_an_undefined_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notification(
            _resident.Id, _condominium.Id, (NotificationType)999, "T", "B"));
    }

    [Fact]
    public void Notification_requires_a_title_and_body()
    {
        Assert.Throws<ArgumentException>(() => new Notification(
            _resident.Id, _condominium.Id, NotificationType.RequestCreated, " ", "B"));
        Assert.Throws<ArgumentException>(() => new Notification(
            _resident.Id, _condominium.Id, NotificationType.RequestCreated, "T", " "));
    }

    // ---- helpers ----

    private async Task<DomainRequest> AddRequestAsync(Guid authorId,
        string title = "Vazamento", Guid? unitId = null)
    {
        var request = new DomainRequest(
            _condominium.Id, authorId, unitId, _category.Id, title, "Descrição");
        _db.Requests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task<Guid[]> RecipientsAsync() =>
        await _db.Notifications
            .AsNoTracking()
            .Select(notification => notification.RecipientUserId)
            .ToArrayAsync();

    private void AddMembership(Guid userId, CondominiumRole role)
    {
        var membership = new CondominiumMembership(userId, _condominium.Id);
        _db.CondominiumMemberships.Add(membership);
        _db.CondominiumMembershipRoles.Add(
            new CondominiumMembershipRole(membership.Id, role));
    }

    private static ApplicationUser User(string name, string email)
    {
        var user = new ApplicationUser(name, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        return user;
    }

    private sealed class FakeAi(ResidentStatusSynthesisResult synthesis)
        : IRequestDraftAiService
    {
        public int SynthesisCalls { get; private set; }
        public Task<RequestDraftAiResult> ProposeAsync(string originalReport,
            IReadOnlyCollection<string> activeCategories, string condominiumName,
            CancellationToken cancellationToken) => Task.FromResult(
                new RequestDraftAiResult(false, null, "unused"));
        public Task<ResidentStatusSynthesisResult> SynthesizeResidentStatusAsync(
            string requestTitle, string newStatus, string reason,
            CancellationToken cancellationToken)
        {
            SynthesisCalls++;
            return Task.FromResult(synthesis);
        }
    }
}
