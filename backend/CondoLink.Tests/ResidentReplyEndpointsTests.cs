using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class ResidentReplyEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost host = null!;
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), "condolink-resident-reply-tests", Guid.NewGuid().ToString("N"));
    private Guid residentId, otherResidentId, managerId, requestId;

    public async Task InitializeAsync()
    {
        host = await CoreEndpointTestHost.StartAsync(app =>
        {
            app.MapUpdateRequestStatus(); app.MapCreateResidentReply(); app.MapGetRequestById();
            app.MapListRequestMessages();
            app.MapListCondominiumRequests();
        }, builder =>
        {
            builder.Configuration["FileStorage:RootPath"] = storageRoot;
            builder.Services.AddSingleton<LocalFileStorage>();
            builder.Services.AddScoped<ResidentReplyService>();
        });
        await host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var manager = CoreTestSeed.User("Síndico", "manager-reply@example.com");
            var resident = CoreTestSeed.User("Morador", "resident-reply@example.com");
            var other = CoreTestSeed.User("Outro", "other-reply@example.com");
            var category = new Category(condominium.Id, "Geral", null);
            var request = new DomainRequest(condominium.Id, resident.Id, null, category.Id, "Portão", "Não abre");
            db.AddRange(condominium, manager, resident, other, category, request);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(db, other.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();
            managerId = manager.Id; residentId = resident.Id; otherResidentId = other.Id; requestId = request.Id;
        });
        await EnterWaitingAsync();
    }

    public async Task DisposeAsync()
    {
        await host.DisposeAsync();
        if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true);
    }

    [Fact]
    public async Task Resident_gets_active_requirement_but_other_resident_cannot_access_request()
    {
        var details = await host.ClientFor(residentId).GetFromJsonAsync<GetRequestById.Response>($"/requests/{requestId}");
        Assert.NotNull(details!.ResidentReplyRequirement);
        Assert.Equal("Envie uma foto do portão.", details.ResidentReplyRequirement.Question);
        Assert.False(details.HasUnreadResidentReply);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.ClientFor(otherResidentId).GetAsync($"/requests/{requestId}")).StatusCode);
    }

    [Fact]
    public async Task Text_reply_atomically_answers_requirement_moves_status_and_notifies_manager()
    {
        var response = await PostAsync(residentId, "O portão voltou a travar.");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.InProgress, (await db.Requests.SingleAsync(x => x.Id == requestId)).Status);
            var message = Assert.Single(await db.RequestMessages.Where(x => x.RequestId == requestId).ToArrayAsync());
            Assert.Equal("O portão voltou a travar.", message.Content);
            var requirement = Assert.Single(await db.RequestResidentReplyRequirements.ToArrayAsync());
            Assert.False(requirement.IsActive); Assert.True(requirement.HasUnreadAnswer);
            Assert.Equal(message.Id, requirement.AnswerMessageId); Assert.NotNull(requirement.AnsweredAt);
            var history = await db.RequestStatusHistories.OrderBy(x => x.CreatedAt).ToArrayAsync();
            Assert.Equal(RequestStatus.WaitingForResident, history[^1].PreviousStatus);
            Assert.Equal(RequestStatus.InProgress, history[^1].NewStatus);
            Assert.Contains(await db.Notifications.ToArrayAsync(), x => x.RecipientUserId == managerId);
        });
        var managerDetails = await host.ClientFor(managerId)
            .GetFromJsonAsync<GetRequestById.Response>($"/requests/{requestId}");
        Assert.True(managerDetails!.HasUnreadResidentReply);
        var replyHistory = managerDetails.StatusHistory[^1];
        Assert.Equal(residentId, replyHistory.ChangedByUserId);
        Assert.NotNull(replyHistory.AnswerMessageId);
        var messages = await host.ClientFor(managerId)
            .GetFromJsonAsync<List<CondoLink.Api.Features.RequestMessages.ListRequestMessages.Response>>(
                $"/requests/{requestId}/messages");
        var replyMessage = Assert.Single(messages!);
        Assert.True(replyMessage.IsResidentReply);
        Assert.Equal(replyMessage.Id, replyHistory.AnswerMessageId);
        var managementList = await host.ClientFor(managerId)
            .GetFromJsonAsync<ListCondominiumRequests.Response>("/management/requests");
        Assert.True(Assert.Single(managementList!.Items).HasUnreadResidentReply);

        Assert.Equal(HttpStatusCode.OK, (await host.ClientFor(managerId).PatchAsJsonAsync(
            $"/requests/{requestId}/status", new { status = "WaitingForThirdParty" })).StatusCode);
        Assert.False(await host.WithDbAsync(db => db.RequestResidentReplyRequirements
            .AnyAsync(x => x.RequestId == requestId && x.HasUnreadAnswer)));
    }

    [Fact]
    public async Task Attachment_only_reply_links_attachment_to_answer_message()
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]); file.Headers.ContentType = new("image/png");
        form.Add(file, "files", "foto.png");
        var response = await host.ClientFor(residentId).PostAsync($"/requests/{requestId}/resident-reply", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await host.WithDbAsync(async db =>
        {
            var message = Assert.Single(await db.RequestMessages.Where(x => x.RequestId == requestId).ToArrayAsync());
            var attachment = Assert.Single(await db.RequestAttachments.ToArrayAsync());
            Assert.Equal(message.Id, attachment.RequestMessageId);
        });
    }

    [Fact]
    public async Task Empty_unauthorized_out_of_state_and_duplicate_replies_are_blocked()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(residentId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(otherResidentId, "Tentativa")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(residentId, "Resposta válida")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(residentId, "Clique duplo")).StatusCode);
        Assert.Single(await host.WithDbAsync(db => db.RequestMessages.Where(x => x.RequestId == requestId).ToArrayAsync()));
    }

    [Fact]
    public async Task Database_failure_rolls_back_message_status_requirement_and_attachment_metadata()
    {
        await host.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_resident_reply_history BEFORE INSERT ON request_status_history
            WHEN NEW.new_status = 2 BEGIN SELECT RAISE(ABORT, 'simulated failure'); END;
            """));
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Resposta"), "message");
        var file = new ByteArrayContent([1]); file.Headers.ContentType = new("image/png"); form.Add(file, "files", "foto.png");
        await Assert.ThrowsAnyAsync<Exception>(() => host.ClientFor(residentId).PostAsync($"/requests/{requestId}/resident-reply", form));
        await host.WithDbAsync(async db =>
        {
            Assert.Equal(RequestStatus.WaitingForResident, (await db.Requests.SingleAsync(x => x.Id == requestId)).Status);
            Assert.True((await db.RequestResidentReplyRequirements.SingleAsync()).IsActive);
            Assert.Empty(await db.RequestMessages.ToArrayAsync()); Assert.Empty(await db.RequestAttachments.ToArrayAsync());
        });
        Assert.Empty(Directory.Exists(storageRoot) ? Directory.GetFiles(storageRoot, "*", SearchOption.AllDirectories) : []);
    }

    private async Task EnterWaitingAsync() => Assert.Equal(HttpStatusCode.OK,
        (await host.ClientFor(managerId).PatchAsJsonAsync($"/requests/{requestId}/status",
            new { status = "WaitingForResident", reason = "Envie uma foto do portão." })).StatusCode);

    private Task<HttpResponseMessage> PostAsync(Guid userId, string? message)
    {
        var form = new MultipartFormDataContent();
        if (message is not null) form.Add(new StringContent(message), "message");
        return host.ClientFor(userId).PostAsync($"/requests/{requestId}/resident-reply", form);
    }
}
