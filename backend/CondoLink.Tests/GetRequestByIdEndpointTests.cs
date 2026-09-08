using CondoLink.Api.Features.RequestMessages;
using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// GET /requests/{id} is readable by the author and by managers of the owning
/// condominium only. Everyone else — including a manager of a different
/// condominium and a plain co-resident — must be refused.
/// </summary>
public sealed class GetRequestByIdEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _authorId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _coResidentId;
    private Guid _outsiderId;
    private Guid _requestId;
    private Guid _unitId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapGetRequestById();
            application.MapListRequestMessages();
            application.MapUpdateRequestStatus();
            application.MapListCondominiumRequests();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var block = new CondominiumBlock(condominium.Id, "Torre A");
            var unit = new Unit(condominium.Id, "101", block.Id, "1", null);
            var author = CoreTestSeed.User(
                "Autor da Silva", "autor@example.com");
            author.Update("Autor da Silva", "+55 11 99999-0001");
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var coResident = CoreTestSeed.User("Vizinho", "vizinho@example.com");
            var outsider = CoreTestSeed.User("Estranho", "estranho@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new DomainRequest(
                condominium.Id, author.Id, unit.Id, category.Id,
                "Vazamento", "Água no corredor");
            var analysis = new RequestAiAnalysis(
                request.Id, "Vazamento", "Água no corredor", "Hidráulica", 0.82,
                "[\"Informar o andar\"]", "gpt-test");
            var originalReport = new RequestMessage(
                request.Id, author.Id, "Tem água vazando no corredor desde ontem.",
                MessageChannel.WhatsApp);
            var originalAudio = new RequestAttachment(
                request.Id, author.Id, "audio.ogg", "requests/test/audio.ogg",
                "audio/ogg", 123, originalReport.Id);

            db.AddRange(
                condominium, otherCondominium, block, unit, author, manager,
                otherManager, coResident, outsider, category, request,
                analysis, originalReport, originalAudio);
            db.UnitMemberships.Add(new UnitMembership(author.Id, unit.Id,
                UnitRelationshipType.Tenant, true, true));
            CoreTestSeed.AddMember(
                db, author.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, coResident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();

            _authorId = author.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _coResidentId = coResident.Id;
            _outsiderId = outsider.Id;
            _requestId = request.Id;
            _unitId = unit.Id;
        });
    }

    [Fact]
    public async Task Timeline_and_messages_expose_only_the_correlated_resident_outbound_status()
    {
        var expected = new Dictionary<Guid, string>();
        var histories = new Dictionary<Guid, string>();
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            foreach (var status in Enum.GetValues<WhatsAppOutboundStatus>())
            {
                var message = new RequestMessage(_requestId, _managerId, status.ToString());
                var history = new RequestStatusHistory(_requestId, RequestStatus.Open,
                    RequestStatus.InProgress, _managerId, status.ToString(), DateTime.UtcNow);
                db.AddRange(message, history);
                db.Add(new WhatsAppOutboundMessage(_requestId, message.Id, _authorId,
                    request.CondominiumId, "5511999990001", WhatsAppNotificationType.AdministrationMessage,
                    WhatsAppSendMode.SessionText, Guid.NewGuid().ToString(), "content", null, null,
                    DateTime.UtcNow, status, requestStatusHistoryId: history.Id));
                // A newer outbound for a different recipient must not replace resident delivery.
                db.Add(new WhatsAppOutboundMessage(_requestId, message.Id, _managerId,
                    request.CondominiumId, "5511999990002", WhatsAppNotificationType.ManagerNewRequest,
                    WhatsAppSendMode.SessionText, Guid.NewGuid().ToString(), "internal", null, null,
                    DateTime.UtcNow.AddMinutes(1), WhatsAppOutboundStatus.Read,
                    requestStatusHistoryId: history.Id));
                expected[message.Id] = status.ToString();
                histories[history.Id] = status.ToString();
            }
            db.Add(new RequestStatusHistory(_requestId, null, RequestStatus.Open,
                _authorId, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
        var client = _host.ClientFor(_managerId);
        var messages = (await client.GetFromJsonAsync<ListRequestMessages.Response[]>(
            $"/requests/{_requestId}/messages"))!;
        Assert.Equal(expected.Count, messages.Count(x => x.WhatsAppDelivery is not null));
        foreach (var message in messages)
            Assert.Equal(expected.GetValueOrDefault(message.Id), message.WhatsAppDelivery?.Status);
        Assert.Contains(messages, x => x.WhatsAppDelivery is null);
        var details = (await client.GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}"))!;
        Assert.Equal(histories.Count, details.StatusHistory.Count(x => x.WhatsAppDelivery is not null));
        foreach (var history in details.StatusHistory)
            Assert.Equal(histories.GetValueOrDefault(history.Id), history.WhatsAppDelivery?.Status);
        Assert.Contains(details.StatusHistory, x => x.WhatsAppDelivery is null);

        await _host.WithDbAsync(async db =>
        {
            var outbound = await db.WhatsAppOutboundMessages.SingleAsync(x =>
                x.UserId == _authorId && x.Status == WhatsAppOutboundStatus.Sent);
            outbound.ApplyProviderStatus("delivered", DateTime.UtcNow, null, null);
            await db.SaveChangesAsync();
        });
        var refreshed = (await client.GetFromJsonAsync<ListRequestMessages.Response[]>(
            $"/requests/{_requestId}/messages"))!;
        var sentId = expected.Single(x => x.Value == "Sent").Key;
        Assert.Equal("Delivered", refreshed.Single(x => x.Id == sentId).WhatsAppDelivery?.Status);
    }

    [Fact]
    public async Task Message_dto_marks_status_generated_messages_as_administrative_events()
    {
        Guid eventMessageId = default, communicationId = default;
        await _host.WithDbAsync(async db =>
        {
            var reason = "Status communicated to resident.";
            var history = new RequestStatusHistory(_requestId, RequestStatus.Open,
                RequestStatus.WaitingForResidentClosure, _managerId, reason, DateTime.UtcNow);
            var eventMessage = new RequestMessage(_requestId, _managerId, reason);
            var communication = new RequestMessage(_requestId, _managerId, "Independent reply.");
            db.AddRange(history, eventMessage, communication);
            await db.SaveChangesAsync();
            eventMessageId = eventMessage.Id;
            communicationId = communication.Id;
        });
        var rows = (await _host.ClientFor(_managerId)
            .GetFromJsonAsync<ListRequestMessages.Response[]>($"/requests/{_requestId}/messages"))!;
        Assert.True(rows.Single(x => x.Id == eventMessageId).IsAdministrativeEvent);
        Assert.False(rows.Single(x => x.Id == communicationId).IsAdministrativeEvent);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Theory]
    [InlineData("Current contextual summary")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Main_description_prefers_valid_ai_without_changing_original_report(string? summary)
    {
        var original = await _host.WithDbAsync(async db =>
        {
            if (summary is null) await db.RequestAiAnalyses.Where(x => x.RequestId == _requestId).ExecuteDeleteAsync();
            else await db.RequestAiAnalyses.Where(x => x.RequestId == _requestId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.GeneratedDescription, summary));
            return await db.Requests.Where(x => x.Id == _requestId).Select(x => x.Description).SingleAsync();
        });
        var manager = await _host.ClientFor(_managerId).GetFromJsonAsync<System.Text.Json.JsonElement>($"/requests/{_requestId}");
        Assert.Equal(string.IsNullOrWhiteSpace(summary) ? original : summary,
            manager.GetProperty("mainDescription").GetString());
        Assert.Equal(original, manager.GetProperty("description").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, manager.GetProperty("originalReport").ValueKind);
        var resident = await _host.ClientFor(_authorId).GetFromJsonAsync<System.Text.Json.JsonElement>($"/requests/{_requestId}");
        Assert.Equal(original, resident.GetProperty("mainDescription").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, resident.GetProperty("aiAnalysis").ValueKind);
    }

    [Fact]
    public async Task Author_can_read_their_own_request()
    {
        var response = await _host.ClientFor(_authorId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<GetRequestById.Response>();
        Assert.Equal(_requestId, body!.Id);
        Assert.Equal(_authorId, body.Author.Id);
        Assert.Equal("Autor da Silva", body.Author.FullName);
        Assert.Equal("Manutenção", body.Category.Name);
        Assert.Equal("InProgress", body.Status);
        Assert.Null(body.AiAnalysis);
        Assert.Null(body.OriginalReport);
    }

    [Fact]
    public async Task Request_details_include_the_target_unit_with_its_block()
    {
        var body = await _host.ClientFor(_authorId)
            .GetFromJsonAsync<GetRequestById.Response>(
                $"/requests/{_requestId}");

        Assert.NotNull(body!.TargetUnit);
        Assert.Equal(_unitId, body.TargetUnit.Id);
        Assert.Equal("101", body.TargetUnit.Identifier);
        Assert.Equal("Torre A", body.TargetUnit.Block);
    }

    [Fact]
    public async Task Manager_of_the_owning_condominium_can_read_the_request()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_receives_current_resident_summary_for_the_request_unit()
    {
        var body = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}");

        Assert.NotNull(body!.ResidentSummary);
        Assert.Equal("Autor da Silva", body.ResidentSummary.FullName);
        Assert.Equal("Torre A", body.ResidentSummary.Block);
        Assert.Equal("101", body.ResidentSummary.Unit);
        Assert.Equal("+55 11 99999-0001", body.ResidentSummary.PhoneNumber);
        Assert.Equal("autor@example.com", body.ResidentSummary.Email);
        Assert.Equal("Tenant", body.ResidentSummary.Relationship);
    }

    [Fact]
    public async Task Resident_summary_is_not_exposed_to_the_request_author()
    {
        var body = await _host.ClientFor(_authorId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}");
        Assert.Null(body!.ResidentSummary);
    }

    [Fact]
    public async Task Resident_update_remains_until_manager_explicitly_acknowledges_it()
    {
        Guid otherRequestId = Guid.Empty;
        await _host.WithDbAsync(async db =>
        {
            var request = await db.Requests.SingleAsync(x => x.Id == _requestId);
            var otherRequest = new DomainRequest(
                request.CondominiumId, _authorId, null, request.CategoryId,
                "Outra solicitação", "Sem relação com a atualização");
            otherRequestId = otherRequest.Id;
            db.Add(otherRequest);
            db.AddRange(
                new Notification(_managerId, request.CondominiumId,
                    NotificationType.ResidentRequestUpdated,
                    "Morador atualizou a solicitação", "Vazamento: nova foto", _requestId),
                new Notification(_managerId, request.CondominiumId,
                    NotificationType.ResidentRequestUpdated,
                    "Morador atualizou a solicitação", "Outra: atualização", otherRequestId));
            await db.SaveChangesAsync();
        });

        var before = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<ListCondominiumRequests.Response>("/management/requests");
        Assert.True(before!.Items.Single(x => x.Id == _requestId).HasUnreadResidentUpdate);
        Assert.True(before.Items.Single(x => x.Id == otherRequestId).HasUnreadResidentUpdate);

        var opened = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}");
        Assert.True(opened!.HasUnreadResidentUpdate);

        var afterOpening = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<ListCondominiumRequests.Response>("/management/requests");
        Assert.True(afterOpening!.Items.Single(x => x.Id == _requestId).HasUnreadResidentUpdate);

        var acknowledgement = await _host.ClientFor(_managerId)
            .PostAsync($"/requests/{_requestId}/resident-update-acknowledgement", null);
        Assert.Equal(HttpStatusCode.NoContent, acknowledgement.StatusCode);

        var afterAcknowledgement = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<ListCondominiumRequests.Response>("/management/requests");
        Assert.False(afterAcknowledgement!.Items.Single(x => x.Id == _requestId).HasUnreadResidentUpdate);
        Assert.True(afterAcknowledgement.Items.Single(x => x.Id == otherRequestId).HasUnreadResidentUpdate);
        await _host.WithDbAsync(async db =>
        {
            Assert.NotNull((await db.Notifications.SingleAsync(x =>
                x.RequestId == _requestId)).ReadAt);
            Assert.Null((await db.Notifications.SingleAsync(x =>
                x.RequestId == otherRequestId)).ReadAt);
            Assert.Equal(RequestStatus.InProgress,
                (await db.Requests.SingleAsync(x => x.Id == _requestId)).Status);
            Assert.Equal("Tem água vazando no corredor desde ontem.",
                (await db.RequestMessages.SingleAsync(x =>
                    x.RequestId == _requestId)).Content);
        });
    }
    [Fact]
    public async Task Manager_details_include_ai_analysis_and_original_whatsapp_report()
    {
        var body = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}");

        Assert.NotNull(body!.AiAnalysis);
        Assert.Equal("Vazamento", body.AiAnalysis.Title);
        Assert.Equal(0.82, body.AiAnalysis.Confidence);
        Assert.Equal(["Informar o andar"], body.AiAnalysis.MissingInformation);
        Assert.Equal("gpt-test", body.AiAnalysis.Model);
        Assert.NotNull(body.OriginalReport);
        Assert.Equal("Tem água vazando no corredor desde ontem.", body.OriginalReport.Text);
        Assert.Equal("WhatsApp", body.OriginalReport.Channel);
        Assert.NotNull(body.OriginalReport.AudioAttachment);
        Assert.Equal("audio/ogg", body.OriginalReport.AudioAttachment.ContentType);
        Assert.Equal($"/request-attachments/{body.OriginalReport.AudioAttachment.Id}/content",
            body.OriginalReport.AudioAttachment.ContentUrl);
    }

    [Fact]
    public async Task Manager_details_remain_compatible_without_ai_analysis()
    {
        await _host.WithDbAsync(async db =>
        {
            var analysis = await db.RequestAiAnalyses.SingleAsync(x => x.RequestId == _requestId);
            db.RequestAiAnalyses.Remove(analysis);
            await db.SaveChangesAsync();
        });

        var body = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{_requestId}");

        Assert.Null(body!.AiAnalysis);
        Assert.NotNull(body.OriginalReport);
    }

    [Fact]
    public async Task Manager_of_another_condominium_is_forbidden()
    {
        var response = await _host.ClientFor(_otherManagerId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Co_resident_who_is_not_the_author_is_forbidden()
    {
        var response = await _host.ClientFor(_coResidentId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_member_of_the_condominium_is_forbidden()
    {
        var response = await _host.ClientFor(_outsiderId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_caller_cannot_read_a_request()
    {
        var response = await _host.AnonymousClient()
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_author_is_forbidden_from_reading_their_own_request()
    {
        await _host.WithDbAsync(async db =>
        {
            var author = await db.Set<ApplicationUser>()
                .SingleAsync(user => user.Id == _authorId);
            author.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_authorId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_request_returns_404()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_history_is_exposed_in_chronological_order_with_the_actor_name()
    {
        var manager = _host.ClientFor(_managerId);
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "WaitingForThirdParty", reason = "Equipe acionada" });
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "WaitingForResidentClosure", reason = "Serviço concluído" });

        var body = await _host.ClientFor(_authorId)
            .GetFromJsonAsync<GetRequestById.Response>(
                $"/requests/{_requestId}");

        Assert.Equal(2, body!.StatusHistory.Count);
        Assert.Equal("InProgress", body.StatusHistory[0].PreviousStatus);
        Assert.Equal("WaitingForThirdParty", body.StatusHistory[0].NewStatus);
        Assert.Equal("Equipe acionada", body.StatusHistory[0].Reason);
        Assert.Equal("Sindico Alfa", body.StatusHistory[0].ChangedByFullName);
        Assert.Equal("WaitingForThirdParty", body.StatusHistory[1].PreviousStatus);
        Assert.Equal("WaitingForResidentClosure", body.StatusHistory[1].NewStatus);
        Assert.Equal("Serviço concluído", body.StatusHistory[1].Reason);
    }
}
