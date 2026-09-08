using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using NoteResponse = CondoLink.Api.Features.Requests.RequestInternalNoteEndpoints.Response;

namespace CondoLink.Tests;

public sealed class RequestInternalNoteEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost host = null!;
    private Guid managerId, residentId, subManagerId, otherManagerId, requestId, otherRequestId, noteId, subMembershipId;
    private string Path => $"/management/requests/{requestId}/internal-notes";
    private const string Secret = "Private instruction never sent to the resident";

    public async Task InitializeAsync()
    {
        host = await CoreEndpointTestHost.StartAsync(app =>
        {
            app.MapRequestInternalNotes();
            app.MapGetRequestById();
            app.MapListRequestMessages();
        });
        await host.WithDbAsync(async db =>
        {
            var condo = new Condominium("Condo", null, null);
            var otherCondo = new Condominium("Other condo", null, null);
            var manager = CoreTestSeed.User("Manager", "notes-manager@example.com");
            var resident = CoreTestSeed.User("Resident", "notes-resident@example.com");
            var sub = CoreTestSeed.User("SubManager", "notes-sub@example.com");
            var other = CoreTestSeed.User("Other manager", "notes-other@example.com");
            var category = new Category(condo.Id, "Maintenance", null);
            var request = new CondoLink.Domain.Entities.Request(condo.Id, resident.Id, null, category.Id, "Leak", "Original report");
            var otherRequest = new CondoLink.Domain.Entities.Request(condo.Id, resident.Id, null, category.Id, "Door", "Door report");
            var note = new RequestInternalNote(request.Id, manager.Id, Secret);
            db.AddRange(condo, otherCondo, manager, resident, sub, other, category, request, otherRequest, note);
            CoreTestSeed.AddMember(db, manager.Id, condo.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, resident.Id, condo.Id, CondominiumRole.Resident);
            subMembershipId = CoreTestSeed.AddMember(db, sub.Id, condo.Id, CondominiumRole.SubManager).Id;
            CoreTestSeed.AddMember(db, other.Id, otherCondo.Id, CondominiumRole.Manager);
            db.Add(new SubManagerModulePermission(subMembershipId, SubManagerModule.Attendance, manager.Id));
            await db.SaveChangesAsync();
            managerId = manager.Id; residentId = resident.Id; subManagerId = sub.Id; otherManagerId = other.Id;
            requestId = request.Id; otherRequestId = otherRequest.Id; noteId = note.Id;
        });
    }

    public async Task DisposeAsync() => await host.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authorized_administrators_can_create_edit_and_delete_without_public_side_effects(bool subManager)
    {
        var actorId = subManager ? subManagerId : managerId;
        var client = host.ClientFor(actorId);
        Assert.Equal(Secret, Assert.Single((await client.GetFromJsonAsync<NoteResponse[]>(Path))!).Content);
        var created = await client.PostAsJsonAsync(Path, new { content = "  Follow up tomorrow  " });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var note = (await created.Content.ReadFromJsonAsync<NoteResponse>())!;
        Assert.Equal(actorId, note.Author.Id);
        Assert.Equal("Follow up tomorrow", note.Content);
        Assert.NotEqual(default, note.CreatedAt);
        Assert.Null(note.UpdatedAt);
        var edited = await client.PutAsJsonAsync($"{Path}/{note.Id}", new { content = "Updated instruction" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var updated = (await edited.Content.ReadFromJsonAsync<NoteResponse>())!;
        Assert.Equal(note.Author, updated.Author);
        Assert.Equal(note.CreatedAt, updated.CreatedAt);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal("Updated instruction", updated.Content);
        // Notes are shared within authorized management; editing retains the original author.
        var existing = await client.PutAsJsonAsync($"{Path}/{noteId}", new { content = Secret });
        Assert.Equal(managerId, (await existing.Content.ReadFromJsonAsync<NoteResponse>())!.Author.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Path}/{note.Id}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<NoteResponse[]>(Path))!);
        await host.WithDbAsync(async db =>
        {
            Assert.Empty(await db.RequestMessages.ToArrayAsync());
            Assert.Empty(await db.RequestStatusHistories.ToArrayAsync());
            Assert.Empty(await db.Notifications.ToArrayAsync());
            Assert.Empty(await db.WhatsAppOutboundMessages.ToArrayAsync());
            Assert.Equal("Original report", (await db.Requests.SingleAsync(x => x.Id == requestId)).Description);
        });
    }

    [Theory]
    [InlineData("resident")]
    [InlineData("other-manager")]
    [InlineData("attendance-denied")]
    [InlineData("legacy-only")]
    [InlineData("revoked-manager")]
    [InlineData("inactive-user")]
    [InlineData("inactive-membership")]
    [InlineData("inactive-role")]
    public async Task Every_operation_enforces_active_attendance_access(string scenario)
    {
        var actor = subManagerId;
        if (scenario == "resident") actor = residentId;
        if (scenario == "other-manager") actor = otherManagerId;
        await host.WithDbAsync(async db =>
        {
            if (scenario is "attendance-denied" or "revoked-manager")
                (await db.SubManagerModulePermissions.SingleAsync()).SetAllowed(false, managerId);
            if (scenario == "legacy-only")
            {
                db.SubManagerModulePermissions.RemoveRange(db.SubManagerModulePermissions);
                db.Add(new SubManagerModulePermission(subMembershipId, SubManagerModule.Requests, managerId));
            }
            if (scenario == "revoked-manager")
            {
                var oldRole = new CondominiumMembershipRole(subMembershipId, CondominiumRole.Manager);
                oldRole.Deactivate(); db.Add(oldRole);
            }
            if (scenario == "inactive-user")
                (await db.Set<ApplicationUser>().SingleAsync(x => x.Id == actor)).SetActiveStatus(false);
            if (scenario == "inactive-membership")
                (await db.CondominiumMemberships.SingleAsync(x => x.Id == subMembershipId)).Deactivate(DateTime.UtcNow);
            if (scenario == "inactive-role")
                (await db.CondominiumMembershipRoles.SingleAsync(x => x.CondominiumMembershipId == subMembershipId)).Deactivate();
            await db.SaveChangesAsync();
        });
        var client = host.ClientFor(actor);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path, new { content = "Unauthorized" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Path}/{noteId}", new { content = "Unauthorized" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Path}/{noteId}")).StatusCode);
        Assert.Equal(Secret, await host.WithDbAsync(db => db.RequestInternalNotes.Select(x => x.Content).SingleAsync()));
    }

    [Fact]
    public async Task Anonymous_callers_are_rejected()
    {
        var client = host.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Path, new { content = "note" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Path}/{noteId}", new { content = "note" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"{Path}/{noteId}")).StatusCode);
    }

    [Fact]
    public async Task Note_ids_cannot_be_used_under_another_request()
    {
        var client = host.ClientFor(managerId);
        var otherPath = $"/management/requests/{otherRequestId}/internal-notes";
        Assert.Empty((await client.GetFromJsonAsync<NoteResponse[]>(otherPath))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{otherPath}/{noteId}", new { content = "Wrong request" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{otherPath}/{noteId}")).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3001)]
    public async Task Invalid_content_is_rejected_on_create_and_edit(int length)
    {
        var client = host.ClientFor(managerId);
        var body = new { content = new string('x', length) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Path}/{noteId}", body)).StatusCode);
    }

    [Fact]
    public async Task Resident_dtos_never_serialize_notes_or_their_contents()
    {
        var client = host.ClientFor(residentId);
        foreach (var path in new[] { $"/requests/{requestId}", $"/requests/{requestId}/messages" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(Secret, json);
            Assert.DoesNotContain("\"internalNotes\"", json);
            Assert.DoesNotContain(noteId.ToString(), json);
        }
        var details = await client.GetFromJsonAsync<JsonElement>($"/requests/{requestId}");
        Assert.False(details.GetProperty("canManageInternalNotes").GetBoolean());
        Assert.True((await host.ClientFor(subManagerId).GetFromJsonAsync<JsonElement>($"/requests/{requestId}"))
            .GetProperty("canManageInternalNotes").GetBoolean());
    }
}
