using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.OperationalMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class AdministrativeRequestUpdateTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _managerId, _residentId, _requestId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app =>
            app.MapCreateAdministrativeRequestUpdate(), builder =>
        {
            builder.Services.AddScoped<WhatsAppNotificationDispatcher>();
            builder.Services.AddScoped<OperationalMessageTemplateService>();
            builder.Services.Configure<WhatsAppOptions>(options =>
            {
                options.Enabled = true;
                options.Templates.StatusChanged.Name = "request_status_update";
                options.Templates.StatusChanged.Language = "pt_BR";
            });
        });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var manager = CoreTestSeed.User("Lisandro Beltrã", "manager-update@example.com");
            var resident = CoreTestSeed.User("Creuza Silva", "resident-update@example.com");
            resident.Update(resident.FullName, "+55 44 99756-2161");
            var unit = new Unit(condominium.Id, "1201", null, null, null);
            var category = new Category(condominium.Id, "Portaria", null);
            var request = new CondoLink.Domain.Entities.Request(condominium.Id,
                resident.Id, unit.Id, category.Id, "TAG veicular", "Solicitação");
            request.ChangeStatus(RequestStatus.WaitingForThirdParty, DateTime.UtcNow.AddHours(-1));
            db.AddRange(condominium, manager, resident, unit, category, request);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
            var inboundAt = DateTime.UtcNow.AddHours(-25);
            var inbound = new WhatsAppInboundMessage("wamid.old", "+554497562161",
                "text", "Oi", inboundAt);
            inbound.Complete(resident.Id, "main_menu", inboundAt);
            db.Add(inbound);
            await db.SaveChangesAsync();
            await db.WhatsAppInboundMessages.Where(x => x.Id == inbound.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ReceivedAt, inboundAt));
            _managerId = manager.Id; _residentId = resident.Id; _requestId = request.Id;
        });
    }

    [Fact]
    public async Task In_progress_with_recent_identified_inbound_uses_session_text()
    {
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            typeof(CondoLink.Domain.Entities.Request).GetProperty(nameof(request.Status))!
                .SetValue(request, RequestStatus.InProgress);
            await db.WhatsAppInboundMessages.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ReceivedAt, DateTime.UtcNow.AddMinutes(-5)));
            await db.SaveChangesAsync();
        });
        var text = "A vistoria continua em andamento — sem alteração de status.";
        Assert.Equal(HttpStatusCode.Created, (await _host.ClientFor(_managerId)
            .PostAsJsonAsync($"/management/requests/{_requestId}/updates",
                new { content = text })).StatusCode);
        var outbound = await _host.WithDbAsync(db => db.WhatsAppOutboundMessages
            .AsNoTracking().SingleAsync());
        Assert.Equal(WhatsAppSendMode.SessionText, outbound.SendMode);
        Assert.Contains(text, outbound.Content);
        Assert.Contains("continua em andamento", outbound.Content);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Persists_distinct_updates_without_status_history_and_queues_correlated_template()
    {
        var before = await _host.WithDbAsync(db => db.RequestStatusHistories.CountAsync(x => x.RequestId == _requestId));
        var client = _host.ClientFor(_managerId);
        foreach (var text in new[] { "Empresa acionada.", "Visita amanhã às 14h." })
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
                $"/management/requests/{_requestId}/updates", new { content = text })).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.AsNoTracking().SingleAsync(x => x.Id == _requestId);
            Assert.Equal(RequestStatus.WaitingForThirdParty, request.Status);
            Assert.Null(request.ResolvedAt);
            Assert.Equal(before, await db.RequestStatusHistories.CountAsync(x => x.RequestId == _requestId));
            var messages = await db.RequestMessages.AsNoTracking().Where(x => x.RequestId == _requestId).OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(2, messages.Length);
            var outbounds = await db.WhatsAppOutboundMessages.AsNoTracking().OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(2, outbounds.Length);
            Assert.All(outbounds, outbound =>
            {
                Assert.Equal(WhatsAppSendMode.Template, outbound.SendMode);
                Assert.Equal("request_status_update", outbound.TemplateName);
                Assert.NotNull(outbound.RequestMessageId);
                Assert.Null(outbound.RequestStatusHistoryId);
                Assert.Equal(WhatsAppNotificationType.AdministrativeRequestUpdate, outbound.NotificationType);
            });
            Assert.Equal(2, outbounds.Select(x => x.IdempotencyKey).Distinct().Count());
            Assert.Contains(messages[1].Content, outbounds[1].Content);
        });
    }

    [Theory]
    [InlineData(RequestStatus.WaitingForResidentClosure)]
    [InlineData(RequestStatus.Resolved)]
    [InlineData(RequestStatus.Cancelled)]
    public async Task Blocks_structural_and_terminal_states(RequestStatus status)
    {
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            typeof(CondoLink.Domain.Entities.Request).GetProperty(nameof(request.Status))!
                .SetValue(request, status);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _host.ClientFor(_managerId)
            .PostAsJsonAsync($"/management/requests/{_requestId}/updates",
                new { content = "Não deve enviar." })).StatusCode);
    }
}
