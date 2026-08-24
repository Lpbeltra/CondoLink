using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.Agenda;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Observability;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class AgendaTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId; private Guid _otherCondominiumId;
    private Guid _managerId; private Guid _unitId; private Guid _otherUnitId;
    private Guid _firstRequestId; private Guid _secondRequestId;
    private readonly RecordingEmailSender _email = new();

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app =>
        {
            app.MapAgendaEndpoints(); app.MapUpdateRequestStatus();
        }, builder => builder.Services
            .AddScoped<WhatsAppNotificationDispatcher>()
            .AddSingleton<IEmailSender>(_email)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<OperationalTelemetry>()
            .Configure<AgendaOptions>(x => x.OperationalTimeZone = "America/Sao_Paulo")
            .Configure<WhatsAppOptions>(x => x.Enabled = true));
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Agenda", null, null);
            var other = new Condominium("Outro", null, null);
            var manager = CoreTestSeed.User("Síndico", "agenda-manager@example.com");
            manager.Update(manager.FullName, "+55 11 98888-0001");
            manager.SetEmailDeliveryEnabled(true);
            var resident = CoreTestSeed.User("Morador", "agenda-resident@example.com");
            var unit = new Unit(condominium.Id, "1201", null, null, null);
            var otherUnit = new Unit(other.Id, "99", null, null, null);
            var category = new Category(condominium.Id, "Manutenção", null);
            var requestA = new CondoLink.Domain.Entities.Request(condominium.Id,
                resident.Id, unit.Id, category.Id, "Elevador", "Ruído");
            var requestB = new CondoLink.Domain.Entities.Request(condominium.Id,
                resident.Id, unit.Id, category.Id, "Portão", "Falha");
            db.AddRange(condominium, other, manager, resident, unit, otherUnit,
                category, requestA, requestB);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();
            _condominiumId = condominium.Id; _otherCondominiumId = other.Id;
            _managerId = manager.Id; _unitId = unit.Id; _otherUnitId = otherUnit.Id;
            _firstRequestId = requestA.Id; _secondRequestId = requestB.Id;
        });
    }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public void Monthly_recurrence_preserves_original_day_with_last_valid_day()
    {
        const string zone = "America/Sao_Paulo";
        var january = new DateTime(2028, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        var february = AgendaRecurrence.Next(january, AgendaRecurrenceType.Monthly, 31, zone)!.Value;
        var march = AgendaRecurrence.Next(february, AgendaRecurrenceType.Monthly, 31, zone)!.Value;
        Assert.Equal(29, TimeZoneInfo.ConvertTimeFromUtc(february,
            TimeZoneInfo.FindSystemTimeZoneById(zone)).Day);
        Assert.Equal(31, TimeZoneInfo.ConvertTimeFromUtc(march,
            TimeZoneInfo.FindSystemTimeZoneById(zone)).Day);
    }

    [Fact]
    public async Task Manager_creates_updates_and_deletes_reminder_with_multiple_requests()
    {
        var client = _host.ClientFor(_managerId);
        var options = await client.GetAsync(
            $"/management/condominiums/{_condominiumId}/agenda/options");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        using (var json = JsonDocument.Parse(await options.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.GetProperty("units").EnumerateArray());
            var requests = json.RootElement.GetProperty("requests").EnumerateArray().ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Equal(RequestProtocol.From(_firstRequestId),
                requests.Single(x => x.GetProperty("id").GetGuid() == _firstRequestId)
                    .GetProperty("protocol").GetString());
        }
        var create = await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda", Input(
                [_firstRequestId, _secondRequestId], _unitId));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        Assert.Equal(2, await _host.WithDbAsync(db => db.AgendaReminderRequests
            .CountAsync(x => x.ReminderId == id)));
        var list = await client.GetAsync(
            $"/management/condominiums/{_condominiumId}/agenda?view=upcoming");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var update = await client.PutAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda/{id}",
            Input([_firstRequestId], _unitId, "Retorno atualizado"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Single(await _host.WithDbAsync(db => db.AgendaReminderRequests
            .Where(x => x.ReminderId == id).ToArrayAsync()));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(
            $"/management/condominiums/{_condominiumId}/agenda/{id}")).StatusCode);
        Assert.True(await _host.WithDbAsync(db => db.Requests.AnyAsync(x =>
            x.Id == _firstRequestId)));
    }

    [Fact]
    public async Task Cross_condominium_unit_and_second_reminder_link_are_blocked()
    {
        var client = _host.ClientFor(_managerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([], _otherUnitId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([_firstRequestId], _unitId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([_firstRequestId], _unitId, "Outro"))).StatusCode);
    }

    [Fact]
    public async Task Portal_terminal_status_unlinks_request_without_deleting_reminder()
    {
        var client = _host.ClientFor(_managerId);
        var create = await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([_firstRequestId], _unitId));
        var reminderId = (await create.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var resolved = await client.PatchAsJsonAsync($"/requests/{_firstRequestId}/status",
            new { status = "Resolved", reason = "Concluído." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Empty(await _host.WithDbAsync(db => db.AgendaReminderRequests
            .Where(x => x.RequestId == _firstRequestId).ToArrayAsync()));
        Assert.True(await _host.WithDbAsync(db => db.AgendaReminders
            .AnyAsync(x => x.Id == reminderId)));
    }

    [Fact]
    public async Task Manager_cannot_access_an_unmanaged_condominium()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.ClientFor(_managerId)
            .GetAsync($"/management/condominiums/{_otherCondominiumId}/agenda"))
            .StatusCode);
    }

    [Fact]
    public async Task Completion_stops_worker_and_reactivation_is_safe()
    {
        var client = _host.ClientFor(_managerId);
        var create = await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([], _unitId, startsAt: DateTime.UtcNow.AddMinutes(-1)));
        var id = (await create.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(
            $"/management/condominiums/{_condominiumId}/agenda/{id}/complete", null)).StatusCode);
        var completed = await _host.WithDbAsync(db => db.AgendaReminders
            .AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.False(completed.IsActive); Assert.NotNull(completed.CompletedAt);
        Assert.Null(completed.NextOccurrenceAtUtc);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(
            $"/management/condominiums/{_condominiumId}/agenda/{id}/reactivate", null)).StatusCode);
        var reactivated = await _host.WithDbAsync(db => db.AgendaReminders
            .AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.True(reactivated.IsActive);
        Assert.True(reactivated.NextOccurrenceAtUtc > DateTime.UtcNow);

        var oneTime = await client.PostAsJsonAsync(
            $"/management/condominiums/{_condominiumId}/agenda",
            Input([], _unitId, "Avulso", "None", DateTime.UtcNow.AddMinutes(-1)));
        var oneTimeId = (await oneTime.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        await client.PostAsync($"/management/condominiums/{_condominiumId}/agenda/{oneTimeId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(
            $"/management/condominiums/{_condominiumId}/agenda/{oneTimeId}/reactivate", null)).StatusCode);
    }

    [Fact]
    public async Task Worker_resolves_current_manager_and_records_both_channels_once()
    {
        var reminderId = await _host.WithDbAsync(async db =>
        {
            var due = DateTime.UtcNow.AddMinutes(-1);
            var reminder = new AgendaReminder(_condominiumId, _managerId,
                "Vistoria mensal", "Conferir equipamentos.", _unitId,
                "Elevadores Paraná", due, "America/Sao_Paulo",
                AgendaRecurrenceType.Monthly, true, true, due);
            var inbound = new WhatsAppInboundMessage("wamid.agenda-manager",
                "+5511988880001", "text", "Oi", due);
            inbound.Complete(_managerId, "main_menu", due);
            db.AddRange(reminder, inbound); await db.SaveChangesAsync();
            return reminder.Id;
        });
        await _host.WithServicesAsync(async services =>
        {
            var agendaOptions = Options.Create(new AgendaOptions
                { WorkerBatchSize = 10 });
            var worker = new AgendaReminderWorker(
                services.GetRequiredService<IServiceScopeFactory>(), agendaOptions,
                services.GetRequiredService<OperationalTelemetry>(),
                NullLogger<AgendaReminderWorker>.Instance);
            Assert.Equal(1, await worker.ProcessBatchAsync(DateTime.UtcNow, default));
            Assert.Equal(0, await worker.ProcessBatchAsync(DateTime.UtcNow, default));
        });
        Assert.Single(_email.Messages);
        await _host.WithDbAsync(async db =>
        {
            var occurrence = await db.AgendaReminderOccurrences.SingleAsync(x =>
                x.ReminderId == reminderId);
            Assert.Equal(AgendaDeliveryStatus.Sent, occurrence.EmailStatus);
            Assert.Equal(AgendaDeliveryStatus.Queued, occurrence.WhatsAppStatus);
            var outbound = await db.WhatsAppOutboundMessages.SingleAsync();
            Assert.Equal(WhatsAppSendMode.SessionText, outbound.SendMode);
            Assert.Equal($"agenda:{occurrence.Id}:whatsapp", outbound.IdempotencyKey);
        });
    }

    private static object Input(Guid[] requests, Guid unitId,
        string title = "Retorno elevadores", string recurrenceType = "Monthly",
        DateTime? startsAt = null) => new { title,
        description = "Cobrar posicionamento.", unitId,
        relatedThirdParty = "Elevadores Paraná",
        startsAtUtc = startsAt ?? DateTime.UtcNow.AddHours(1), recurrenceType,
        notifyByWhatsApp = true, notifyByEmail = true, requestIds = requests };
    private sealed record IdResponse(Guid Id);
    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string Recipient, string Subject, string Html)> Messages { get; } = [];
        public Task SendAsync(string recipient, string subject, string html,
            CancellationToken cancellationToken)
        { Messages.Add((recipient, subject, html)); return Task.CompletedTask; }
    }
}
