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
        Assert.Contains("Aguardando terceiro", notification.Body);
        Assert.DoesNotContain("Comentário da administração", notification.Body);
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
        Assert.Contains("Aguardando terceiro", notification.Body);
        Assert.DoesNotContain("Aguardando fornecedor", notification.Body);
    }

    [Fact]
    public async Task Waiting_for_third_party_uses_ai_synthesis_and_enqueues_once()
    {
        var ai = new FakeAi(new(true,
            "Estamos aguardando a emissão da TAG para a portaria. Você será avisado quando houver novidade.",
            "succeeded", "model-test"));
        var dispatcher = new WhatsAppNotificationDispatcher(_db,
            Options.Create(new WhatsAppOptions()),
            NullLogger<WhatsAppNotificationDispatcher>.Instance);
        var service = new NotificationService(_db, dispatcher,
            NullLogger<NotificationService>.Instance, ai);
        var request = await AddRequestAsync(_resident.Id, "TAG de acesso");
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);
        var historyId = Guid.NewGuid();

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, historyId, "Solicitada a TAG para a portaria");
        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, historyId, "Solicitada a TAG para a portaria");

        Assert.Equal(1, ai.SynthesisCalls);
        var outbound = Assert.Single(await _db.WhatsAppOutboundMessages
            .AsNoTracking().ToArrayAsync());
        Assert.Contains("aguardando a emissão da TAG", outbound.Content);
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
            NullLogger<NotificationService>.Instance, ai);
        var request = await AddRequestAsync(_resident.Id);
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow);

        await service.NotifyStatusChangedAsync(request, RequestStatus.InProgress,
            _managerA.Id, default, Guid.NewGuid(), "Fornecedor acionado");

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal("Seu atendimento foi atualizado para 'Aguardando terceiro'. "
            + "A administração continuará acompanhando a solicitação.",
            notification.Body);
        Assert.Single(await _db.WhatsAppOutboundMessages.AsNoTracking().ToArrayAsync());
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

        Assert.Equal("A solicitação *\"Vazamento\"* foi alterada de "
            + "*Em andamento* para *Aguardando terceiro*.", withoutComment);
        Assert.DoesNotContain("Comentário", withoutComment);
        Assert.Contains("Comentário da administração:\n\nEnvie uma foto.", withComment);
    }

    [Fact]
    public void Status_notification_uses_the_revised_semantic_labels()
    {
        Assert.Contains("Aguardando morador", NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, RequestStatus.WaitingForResident, null));
        Assert.Contains("Aguardando você", NotificationService.StatusChangedContent(
            "Vazamento", RequestStatus.InProgress, RequestStatus.WaitingForManager, null));
        Assert.Contains("Aguardando terceiro", NotificationService.StatusChangedContent(
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

    private async Task<DomainRequest> AddRequestAsync(Guid authorId, string title = "Vazamento")
    {
        var request = new DomainRequest(
            _condominium.Id, authorId, null, _category.Id, title, "Descrição");
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
