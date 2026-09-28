using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Voice;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class VoiceTranscriptionEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost host = null!;
    private readonly Transcriber transcription = new();
    private Guid managerId, subId, residentId, outsiderId, condominiumId, requestId, membershipId;
    private string RequestPath => $"/management/requests/{requestId}/transcription";
    private string AssistantPath => $"/condominiums/{condominiumId}/assistant/transcription";
    private IEnumerable<string> Paths => [RequestPath, AssistantPath];
    private static readonly byte[] Webm = [0x1a, 0x45, 0xdf, 0xa3, 0x81, 0x01, 0x00, 0x00];

    public async Task InitializeAsync()
    {
        host = await CoreEndpointTestHost.StartAsync(app => app.MapVoiceTranscription(), builder =>
            builder.Services.AddSingleton<IWhatsAppAudioTranscriptionService>(transcription));
        await host.WithDbAsync(async db =>
        {
            var condo = new Condominium("Condo", null, null);
            var other = new Condominium("Other condo", null, null);
            var manager = CoreTestSeed.User("Manager", "voice-manager@example.com");
            var sub = CoreTestSeed.User("Sub", "voice-sub@example.com");
            var resident = CoreTestSeed.User("Resident", "voice-resident@example.com");
            var outsider = CoreTestSeed.User("Outsider", "voice-outsider@example.com");
            var category = new Category(condo.Id, "Maintenance", null);
            var request = new CondoLink.Domain.Entities.Request(condo.Id, resident.Id, null, category.Id, "Leak", "Original description");
            db.AddRange(condo, other, manager, sub, resident, outsider, category, request);
            CoreTestSeed.AddMember(db, manager.Id, condo.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, resident.Id, condo.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(db, outsider.Id, other.Id, CondominiumRole.Manager);
            membershipId = CoreTestSeed.AddMember(db, sub.Id, condo.Id, CondominiumRole.SubManager).Id;
            db.AddRange(new SubManagerModulePermission(membershipId, SubManagerModule.Attendance, manager.Id),
                new SubManagerModulePermission(membershipId, SubManagerModule.Assistant, manager.Id));
            await db.SaveChangesAsync();
            managerId = manager.Id; subId = sub.Id; residentId = resident.Id; outsiderId = outsider.Id;
            condominiumId = condo.Id; requestId = request.Id;
        });
    }

    public async Task DisposeAsync() => await host.DisposeAsync();

    private static MultipartFormDataContent Audio(string mime = "audio/webm", byte[]? bytes = null,
        string field = "audio", string filename = "../../unsafe-name.webm")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes ?? Webm);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mime);
        form.Add(content, field, filename);
        return form;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authorized_manager_and_submanager_receive_text_without_persistence(bool subManager)
    {
        var initialStatus = await host.WithDbAsync(db => db.Requests.Where(item => item.Id == requestId).Select(item => item.Status).SingleAsync());
        transcription.Result = new(true, "  Texto revisável pelo gestor.  ", "succeeded");
        foreach (var path in Paths)
        {
            using var form = Audio();
            var response = await host.ClientFor(subManager ? subId : managerId).PostAsync(path, form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Texto revisável pelo gestor.", json.GetProperty("text").GetString());
            Assert.Single(json.EnumerateObject());
        }
        Assert.Equal(2, transcription.Calls);
        Assert.Equal("recording.webm", transcription.FileName);
        Assert.Equal("audio/webm", transcription.ContentType);
        Assert.Equal(Webm, transcription.Bytes);
        await host.WithDbAsync(async db =>
        {
            Assert.Empty(await db.RequestMessages.ToArrayAsync());
            Assert.Empty(await db.RequestInternalNotes.ToArrayAsync());
            Assert.Empty(await db.RequestAttachments.ToArrayAsync());
            Assert.Empty(await db.RequestStatusHistories.ToArrayAsync());
            Assert.Empty(await db.Notifications.ToArrayAsync());
            Assert.Empty(await db.WhatsAppOutboundMessages.ToArrayAsync());
            Assert.Empty(await db.CondominiumAssistantConversations.ToArrayAsync());
            Assert.Empty(await db.CondominiumAssistantMessages.ToArrayAsync());
            var persisted = await db.Requests.SingleAsync(item => item.Id == requestId);
            Assert.Equal("Original description", persisted.Description);
            Assert.Equal(initialStatus, persisted.Status);
        });
    }

    [Theory]
    [InlineData("resident", 403)]
    [InlineData("other-condominium", 403)]
    [InlineData("denied", 403)]
    [InlineData("inactive-user", 403)]
    [InlineData("missing-user", 403)]
    [InlineData("inactive-membership", 403)]
    [InlineData("inactive-role", 403)]
    [InlineData("inactive-condominium", 404)]
    [InlineData("anonymous", 401)]
    public async Task Both_routes_enforce_authorization_before_processing_audio(string scenario, int status)
    {
        var actor = scenario switch { "resident" => residentId, "other-condominium" => outsiderId, "missing-user" => Guid.NewGuid(), _ => subId };
        await host.WithDbAsync(async db =>
        {
            if (scenario == "denied")
                foreach (var permission in await db.SubManagerModulePermissions.ToListAsync()) permission.SetAllowed(false, managerId);
            if (scenario == "inactive-user")
                (await db.Set<ApplicationUser>().SingleAsync(item => item.Id == actor)).SetActiveStatus(false);
            if (scenario == "inactive-membership")
                (await db.CondominiumMemberships.SingleAsync(item => item.Id == membershipId)).Deactivate(DateTime.UtcNow);
            if (scenario == "inactive-role")
                (await db.CondominiumMembershipRoles.SingleAsync(item => item.CondominiumMembershipId == membershipId)).Deactivate();
            if (scenario == "inactive-condominium")
                (await db.Condominiums.SingleAsync(item => item.Id == condominiumId)).SetActiveStatus(false);
            await db.SaveChangesAsync();
        });
        foreach (var path in Paths)
        {
            var client = scenario == "anonymous" ? host.AnonymousClient() : host.ClientFor(actor);
            using var form = Audio();
            Assert.Equal(status, (int)(await client.PostAsync(path, form)).StatusCode);
        }
        Assert.Equal(0, transcription.Calls);
    }

    [Theory]
    [InlineData(SubManagerModule.Attendance)]
    [InlineData(SubManagerModule.Assistant)]
    public async Task Module_permissions_are_not_interchangeable(SubManagerModule denied)
    {
        await host.WithDbAsync(async db =>
        {
            (await db.SubManagerModulePermissions.SingleAsync(item => item.Module == denied)).SetAllowed(false, managerId);
            await db.SaveChangesAsync();
        });
        using var attendanceAudio = Audio();
        using var assistantAudio = Audio();
        var client = host.ClientFor(subId);
        Assert.Equal(denied == SubManagerModule.Attendance ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
            (await client.PostAsync(RequestPath, attendanceAudio)).StatusCode);
        Assert.Equal(denied == SubManagerModule.Assistant ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
            (await client.PostAsync(AssistantPath, assistantAudio)).StatusCode);
        Assert.Equal(1, transcription.Calls);
    }

    [Fact]
    public async Task Platform_admin_bypass_is_assistant_only_and_requires_active_condominium_and_user()
    {
        var client = host.ClientFor(outsiderId);
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        using (var audio = Audio()) Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(AssistantPath, audio)).StatusCode);
        using (var audio = Audio()) Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(RequestPath, audio)).StatusCode);
        using (var audio = Audio()) Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/condominiums/{Guid.NewGuid()}/assistant/transcription", audio)).StatusCode);
        await host.WithDbAsync(async db => { (await db.Condominiums.SingleAsync(item => item.Id == condominiumId)).SetActiveStatus(false); await db.SaveChangesAsync(); });
        using (var audio = Audio()) Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(AssistantPath, audio)).StatusCode);
        await host.WithDbAsync(async db => { (await db.Set<ApplicationUser>().SingleAsync(item => item.Id == outsiderId)).SetActiveStatus(false); await db.SaveChangesAsync(); });
        using (var audio = Audio()) Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(AssistantPath, audio)).StatusCode);
        Assert.Equal(1, transcription.Calls);
    }

    [Theory]
    [InlineData("audio/webm; codecs=opus", "audio/webm", "recording.webm")]
    [InlineData("audio/ogg; codecs=opus", "audio/ogg", "recording.ogg")]
    [InlineData("audio/mp4; codecs=mp4a.40.2", "audio/mp4", "recording.m4a")]
    [InlineData("audio/x-m4a", "audio/mp4", "recording.m4a")]
    public async Task Supported_mime_is_normalized_and_client_filename_is_never_forwarded(string mime, string normalized, string filename)
    {
        var bytes = normalized switch { "audio/ogg" => Encoding.ASCII.GetBytes("OggS00000000"), "audio/mp4" => new byte[] { 0, 0, 0, 12, 102, 116, 121, 112, 77, 52, 65, 32 }, _ => Webm };
        using var audio = Audio(mime, bytes);
        Assert.Equal(HttpStatusCode.OK, (await host.ClientFor(managerId).PostAsync(AssistantPath, audio)).StatusCode);
        Assert.Equal(normalized, transcription.ContentType);
        Assert.Equal(filename, transcription.FileName);
        Assert.Equal(bytes, transcription.Bytes);
    }

    [Theory]
    [InlineData("empty", 400)]
    [InlineData("missing", 400)]
    [InlineData("wrong-field", 400)]
    [InlineData("multiple", 400)]
    [InlineData("unsupported", 400)]
    [InlineData("signature-webm", 400)]
    [InlineData("signature-mp4", 400)]
    [InlineData("signature-ogg", 400)]
    [InlineData("large-file", 413)]
    [InlineData("large-body", 413)]
    public async Task Invalid_audio_never_reaches_provider(string scenario, int expectedStatus)
    {
        var bytes = scenario switch
        {
            "empty" => [],
            "signature-webm" or "signature-mp4" or "signature-ogg" => Encoding.ASCII.GetBytes("not an audio container"),
            "large-file" => new byte[VoiceTranscriptionEndpoints.MaximumAudioBytes + 1],
            "large-body" => new byte[VoiceTranscriptionEndpoints.MaximumRequestBytes + 1],
            _ => Webm,
        };
        var mime = scenario switch { "unsupported" => "image/png", "signature-mp4" => "audio/mp4", "signature-ogg" => "audio/ogg", _ => "audio/webm" };
        using var form = scenario == "missing" ? new MultipartFormDataContent() : Audio(mime, bytes, scenario == "wrong-field" ? "file" : "audio");
        if (scenario == "multiple") form.Add(new ByteArrayContent(Webm), "audio", "second.webm");
        foreach (var path in Paths)
            Assert.Equal(expectedStatus, (int)(await host.ClientFor(managerId).PostAsync(path, form)).StatusCode);
        Assert.Equal(0, transcription.Calls);
    }

    [Fact]
    public async Task Missing_request_and_non_multipart_body_are_rejected()
    {
        var client = host.ClientFor(managerId);
        using var audio = Audio();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/management/requests/{Guid.NewGuid()}/transcription", audio)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(AssistantPath, new { audio = "bad" })).StatusCode);
        using var malformed = new StringContent("incomplete boundary", Encoding.UTF8, "multipart/form-data");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(AssistantPath, malformed)).StatusCode);
        using var truncated = new StringContent("--declared\r\nContent-Disposition: form-data; name=\"audio\"; filename=\"audio.webm\"\r\nContent-Type: audio/webm\r\n\r\nbroken");
        truncated.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=declared");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(AssistantPath, truncated)).StatusCode);
        Assert.Equal(0, transcription.Calls);
    }

    [Fact]
    public async Task Exact_file_and_text_limits_are_accepted()
    {
        var bytes = new byte[VoiceTranscriptionEndpoints.MaximumAudioBytes];
        Webm.CopyTo(bytes, 0);
        transcription.Result = new(true, new string('x', VoiceTranscriptionEndpoints.MaximumTextLength), "succeeded");
        using var form = Audio(bytes: bytes);
        var response = await host.ClientFor(managerId).PostAsync(AssistantPath, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(VoiceTranscriptionEndpoints.MaximumAudioBytes, transcription.Bytes!.Length);
        Assert.Equal(VoiceTranscriptionEndpoints.MaximumTextLength,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("text").GetString()!.Length);
    }

    [Fact]
    public async Task Chunked_body_without_content_length_is_bounded()
    {
        using var content = new UnknownLengthContent(new byte[VoiceTranscriptionEndpoints.MaximumRequestBytes + 1]);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/webm");
        using var form = new MultipartFormDataContent();
        form.Add(content, "audio", "recording.webm");
        Assert.Null(form.Headers.ContentLength);
        var response = await host.ClientFor(managerId).PostAsync(AssistantPath, form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, transcription.Calls);
    }

    [Theory]
    [InlineData("disabled", 503)]
    [InlineData("not_configured", 503)]
    [InlineData("timeout", 504)]
    [InlineData("http_rate_limited", 502)]
    [InlineData("http_unauthorized", 502)]
    [InlineData("empty_response", 502)]
    [InlineData("provider_error", 502)]
    public async Task Provider_failures_have_friendly_responses(string code, int status)
    {
        transcription.Result = new(false, "sensitive provider detail", code);
        foreach (var path in Paths)
        {
            using var audio = Audio();
            var response = await host.ClientFor(managerId).PostAsync(path, audio);
            Assert.Equal(status, (int)response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("sensitive", body);
            Assert.Contains("message", body);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12001)]
    public async Task Invalid_successful_provider_text_is_rejected(int length)
    {
        transcription.Result = new(true, length == 0 ? "  " : new string('x', length), "succeeded");
        using var audio = Audio();
        Assert.Equal(HttpStatusCode.BadGateway, (await host.ClientFor(managerId).PostAsync(RequestPath, audio)).StatusCode);
    }

    [Theory]
    [InlineData(false, 502)]
    [InlineData(true, 504)]
    public async Task Unexpected_provider_exceptions_do_not_expose_details(bool timeout, int status)
    {
        transcription.Handler = _ => throw (timeout ? new OperationCanceledException("internal timeout") : new HttpRequestException("secret provider URL"));
        using var audio = Audio();
        var response = await host.ClientFor(managerId).PostAsync(AssistantPath, audio);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.DoesNotContain("internal", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Caller_cancellation_reaches_provider_instead_of_becoming_provider_failure()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transcription.Handler = async ct =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { cancelled.SetResult(); throw; }
            return new(true, "unreachable", "succeeded");
        };
        using var cancellation = new CancellationTokenSource();
        using var audio = Audio();
        var sending = host.ClientFor(managerId).PostAsync(AssistantPath, audio, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class Transcriber : IWhatsAppAudioTranscriptionService
    {
        public int Calls { get; private set; }
        public string? FileName { get; private set; }
        public string? ContentType { get; private set; }
        public byte[]? Bytes { get; private set; }
        public AudioTranscriptionResult Result { get; set; } = new(true, "Transcrição para revisão.", "succeeded");
        public Func<CancellationToken, Task<AudioTranscriptionResult>>? Handler { get; set; }
        public Task<AudioTranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> audio, string fileName, string contentType, CancellationToken cancellationToken)
        {
            Calls++; FileName = fileName; ContentType = contentType; Bytes = audio.ToArray();
            return Handler?.Invoke(cancellationToken) ?? Task.FromResult(Result);
        }
    }
}
