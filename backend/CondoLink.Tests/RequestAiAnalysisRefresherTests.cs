using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class RequestAiAnalysisRefresherTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private FakeAi _ai = null!;
    private Guid _requestId;
    private Guid _residentId;
    private Guid _managerId;

    public async Task InitializeAsync()
    {
        _ai = new FakeAi();
        _host = await CoreEndpointTestHost.StartAsync(app =>
        {
            app.MapCreateRequestMessage();
            app.MapUpdateRequestStatus();
        }, builder =>
        {
            builder.Services.AddSingleton<IRequestDraftAiService>(_ai);
            builder.Services.AddScoped<RequestAiAnalysisRefresher>();
        });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var resident = CoreTestSeed.User("Morador", "reactive-resident@example.com");
            var manager = CoreTestSeed.User("Gestor", "reactive-manager@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new DomainRequest(condominium.Id, resident.Id, null,
                category.Id, "Portão", "Portão com defeito");
            db.AddRange(condominium, resident, manager, category, request);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id,
                CondominiumRole.Resident);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id,
                CondominiumRole.Manager);
            await db.SaveChangesAsync();
            await db.Requests.Where(item => item.Id == request.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status,
                    RequestStatus.Open));
            _requestId = request.Id;
            _residentId = resident.Id;
            _managerId = manager.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Resident_and_manager_messages_each_refresh_once()
    {
        var resident = await _host.ClientFor(_residentId).PostAsJsonAsync(
            $"/requests/{_requestId}/messages", new { content = "Piorou hoje." });
        Assert.Equal(HttpStatusCode.Created, resident.StatusCode);
        Assert.Equal(1, _ai.ProposalCalls);

        var manager = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/requests/{_requestId}/messages", new { content = "Equipe acionada." });
        Assert.Equal(HttpStatusCode.Created, manager.StatusCode);
        Assert.Equal(2, _ai.ProposalCalls);
        Assert.Equal(2, await _host.WithDbAsync(db => db.RequestMessages
            .CountAsync(item => item.RequestId == _requestId)));
    }

    [Fact]
    public async Task Status_change_refreshes_once()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _ai.ProposalCalls);
        Assert.Equal(RequestStatus.InProgress, await _host.WithDbAsync(db =>
            db.Requests.Where(item => item.Id == _requestId)
                .Select(item => item.Status).SingleAsync()));
    }

    [Fact]
    public async Task Ai_failure_does_not_prevent_primary_operation()
    {
        _ai.Result = new(false, null, "provider failure");

        var response = await _host.ClientFor(_residentId).PostAsJsonAsync(
            $"/requests/{_requestId}/messages", new { content = "Mensagem preservada." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, _ai.ProposalCalls);
        Assert.True(await _host.WithDbAsync(db => db.RequestMessages
            .AnyAsync(item => item.RequestId == _requestId
                && item.Content == "Mensagem preservada.")));
    }

    private sealed class FakeAi : IRequestDraftAiService
    {
        public int ProposalCalls { get; private set; }
        public RequestDraftAiResult Result { get; set; } = new(true,
            new RequestDraftAiProposal("Resumo atualizado", "Contexto atualizado",
                "Manutenção", [], .9), null, RequestDraftAiOutcome.Succeeded,
            "model-test");

        public Task<RequestDraftAiResult> ProposeAsync(string originalReport,
            IReadOnlyCollection<string> activeCategories, string condominiumName,
            CancellationToken cancellationToken)
        {
            ProposalCalls++;
            return Task.FromResult(Result);
        }

        public Task<ResidentStatusSynthesisResult> SynthesizeResidentStatusAsync(
            string requestTitle, string newStatus, string reason,
            CancellationToken cancellationToken) => Task.FromResult(
                new ResidentStatusSynthesisResult(false, null, "unused"));
    }
}
