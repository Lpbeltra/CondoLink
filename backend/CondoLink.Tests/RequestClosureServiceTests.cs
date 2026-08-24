using CondoLink.Api.Features.Notifications;
using System.Net.Http.Json;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class RequestClosureServiceTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _requestId;
    private Guid _residentId;
    private Guid _managerId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app => app.MapManageResidentClosure(), builder =>
        {
            builder.Services.AddScoped<RequestClosureService>();
        });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var resident = CoreTestSeed.User("Morador", "closure-resident@example.com");
            var manager = CoreTestSeed.User("SÃ­ndico", "closure-manager@example.com");
            var category = new Category(condominium.Id, "Portaria", null);
            var request = new DomainRequest(condominium.Id, resident.Id, null,
                category.Id, "Tag", "Preciso de uma tag");
            db.AddRange(condominium, resident, manager, category, request);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();
            _requestId = request.Id; _residentId = resident.Id; _managerId = manager.Id;
        });
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public async Task Portal_confirmation_uses_the_shared_closure_service()
    {
        await ArrangePendingAsync(DateTime.UtcNow);
        var response = await _host.ClientFor(_residentId)
            .PostAsync($"/requests/{_requestId}/resident-closure/confirm", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(RequestStatus.Resolved,
            await _host.WithDbAsync(db => db.Requests.Where(x => x.Id == _requestId).Select(x => x.Status).SingleAsync()));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await _host.ClientFor(_residentId)
            .PostAsync($"/requests/{_requestId}/resident-closure/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task Portal_question_is_an_update_on_the_same_request()
    {
        await ArrangePendingAsync(DateTime.UtcNow);
        var response = await _host.ClientFor(_residentId).PostAsJsonAsync(
            $"/requests/{_requestId}/resident-closure/question", new { message = "Ainda não funcionou." });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.InProgress,
                await db.Requests.Where(x => x.Id == _requestId).Select(x => x.Status).SingleAsync());
            var message = Assert.Single(await db.RequestMessages.Where(x => x.RequestId == _requestId).ToArrayAsync());
            Assert.Equal(MessageChannel.Portal, message.Channel);
            Assert.Equal(RequestClosureConfirmationStatus.Questioned,
                await db.RequestClosureConfirmations.Select(x => x.Status).SingleAsync());
        });
    }

    [Fact]
    public async Task Resident_confirmation_resolves_once_and_closes_session()
    {
        await ArrangePendingAsync(DateTime.UtcNow);
        var first = await InvokeAsync(service => service.ConfirmAsync(_requestId, _residentId, default));
        var duplicate = await InvokeAsync(service => service.ConfirmAsync(_requestId, _residentId, default));
        Assert.True(first.Succeeded); Assert.False(duplicate.Succeeded);
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.AsNoTracking().SingleAsync(x => x.Id == _requestId);
            Assert.Equal(RequestStatus.Resolved, request.Status); Assert.NotNull(request.ResolvedAt);
            var confirmation = await db.RequestClosureConfirmations.SingleAsync();
            Assert.Equal(RequestClosureConfirmationStatus.Confirmed, confirmation.Status);
            Assert.NotNull(confirmation.DecidedAt);
            Assert.Single(await db.RequestStatusHistories.Where(x => x.NewStatus == RequestStatus.Resolved).ToArrayAsync());
            Assert.Empty(await db.AgendaReminderRequests.ToArrayAsync());
        });
    }

    [Fact]
    public async Task Resident_question_stays_on_same_request_and_notifies_manager_once()
    {
        await ArrangePendingAsync(DateTime.UtcNow);
        var first = await InvokeAsync(service => service.QuestionAsync(_requestId, _residentId,
            "A tag funciona no portÃ£o lateral?", default));
        var duplicate = await InvokeAsync(service => service.QuestionAsync(_requestId, _residentId,
            "A tag funciona no portÃ£o lateral?", default));
        Assert.True(first.Succeeded); Assert.False(duplicate.Succeeded);
        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.InProgress, await db.Requests.Where(x => x.Id == _requestId).Select(x => x.Status).SingleAsync());
            var message = Assert.Single(await db.RequestMessages.ToArrayAsync());
            Assert.Equal(_requestId, message.RequestId); Assert.Equal(_residentId, message.AuthorUserId);
            Assert.Equal(MessageChannel.WhatsAppResidentUpdate, message.Channel);
            Assert.Equal(RequestClosureConfirmationStatus.Questioned,
                await db.RequestClosureConfirmations.Select(x => x.Status).SingleAsync());
            Assert.Single(await db.Notifications.Where(x => x.RecipientUserId == _managerId).ToArrayAsync());
        });
    }

    [Fact]
    public async Task Expiration_is_idempotent_across_two_worker_cycles()
    {
        var now = DateTime.UtcNow;
        await ArrangePendingAsync(now.AddHours(-2));
        var first = await InvokeAsync(service => service.ExpireBatchAsync(now, 100, default));
        var second = await InvokeAsync(service => service.ExpireBatchAsync(now, 100, default));
        Assert.Equal(1, first); Assert.Equal(0, second);
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            Assert.Equal(RequestStatus.Resolved, request.Status); Assert.NotNull(request.ResolvedAt);
            Assert.Equal(RequestClosureConfirmationStatus.Expired,
                await db.RequestClosureConfirmations.Select(x => x.Status).SingleAsync());
            Assert.Single(await db.RequestStatusHistories.Where(x => x.NewStatus == RequestStatus.Resolved).ToArrayAsync());
            Assert.Empty(await db.AgendaReminderRequests.ToArrayAsync());
        });
    }

    [Fact]
    public async Task Confirmation_winning_before_worker_is_the_only_result()
    {
        var now = DateTime.UtcNow;
        await ArrangePendingAsync(now.AddHours(-2));
        // Move expiry just ahead so confirmation wins this interleaving.
        await _host.WithDbAsync(db => db.RequestClosureConfirmations.ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.ExpiresAt, now.AddMinutes(1))));
        Assert.True((await InvokeAsync(s => s.ConfirmAsync(_requestId, _residentId, default))).Succeeded);
        Assert.Equal(0, await InvokeAsync(s => s.ExpireBatchAsync(now.AddMinutes(2), 100, default)));
    }

    [Fact]
    public async Task Worker_winning_before_question_rejects_late_response()
    {
        var now = DateTime.UtcNow;
        await ArrangePendingAsync(now.AddHours(-2));
        Assert.Equal(1, await InvokeAsync(s => s.ExpireBatchAsync(now, 100, default)));
        Assert.False((await InvokeAsync(s => s.QuestionAsync(_requestId, _residentId,
            "DÃºvida tardia", default))).Succeeded);
        Assert.Empty(await _host.WithDbAsync(db => db.RequestMessages.ToArrayAsync()));
    }

    [Fact]
    public async Task Cancellation_closes_pending_confirmation_and_worker_ignores_it()
    {
        var now = DateTime.UtcNow;
        await ArrangePendingAsync(now.AddHours(-2));
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.AsNoTracking().SingleAsync(x => x.Id == _requestId);
            request.ChangeStatus(RequestStatus.Cancelled, now);
            var history = new RequestStatusHistory(_requestId,
                RequestStatus.WaitingForResidentClosure, RequestStatus.Cancelled,
                _managerId, "Cancelamento administrativo", now);
            Assert.True(await UpdateRequestStatus.TryPersistStatusChangeAsync(db,
                request, RequestStatus.WaitingForResidentClosure, history, default));
        });
        Assert.Equal(0, await InvokeAsync(s => s.ExpireBatchAsync(now.AddHours(1), 100, default)));
        Assert.Equal(RequestClosureConfirmationStatus.Cancelled,
            await _host.WithDbAsync(db => db.RequestClosureConfirmations.Select(x => x.Status).SingleAsync()));
        Assert.Empty(await _host.WithDbAsync(db => db.AgendaReminderRequests.ToArrayAsync()));
    }

    private async Task ArrangePendingAsync(DateTime requestedAt)
    {
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            request.ChangeStatus(RequestStatus.WaitingForResidentClosure, requestedAt);
            var history = new RequestStatusHistory(_requestId, RequestStatus.InProgress,
                RequestStatus.WaitingForResidentClosure, _managerId,
                "Tag entregue na portaria.", requestedAt);
            db.RequestStatusHistories.Add(history);
            db.RequestClosureConfirmations.Add(new RequestClosureConfirmation(
                _requestId, history.Id, history.Reason!, requestedAt));
            var reminder = new AgendaReminder(request.CondominiumId, _managerId,
                "Acompanhar tag", null, null, null, requestedAt.AddDays(1),
                "America/Sao_Paulo", AgendaRecurrenceType.None, false, false,
                requestedAt);
            db.AddRange(reminder, new AgendaReminderRequest(reminder.Id,
                _requestId, _managerId, requestedAt));
            var session = new WhatsAppSession("5511999999999", requestedAt, requestedAt.AddMinutes(30));
            session.AwaitClosure(_requestId, requestedAt, requestedAt.AddMinutes(30));
            db.WhatsAppSessions.Add(session);
            await db.SaveChangesAsync();
        });
    }

    private async Task<T> InvokeAsync<T>(Func<RequestClosureService, Task<T>> action)
    {
        T result = default!;
        await _host.WithServicesAsync(async provider =>
            result = await action(provider.GetRequiredService<RequestClosureService>()));
        return result;
    }
}

public sealed class RequestClosureWorkerConfigurationTests
{
    [Fact]
    public void Worker_uses_two_minute_polling_and_batches_of_one_hundred()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), RequestClosureWorker.PollingInterval);
        Assert.Equal(100, RequestClosureWorker.BatchSize);
    }
}
