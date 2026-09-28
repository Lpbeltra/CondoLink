using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Voice;

public static class VoiceTranscriptionEndpoints
{
    public const int MaximumAudioBytes = 10 * 1024 * 1024;
    public const int MaximumRequestBytes = 11 * 1024 * 1024;
    public const int MaximumTextLength = 12000;

    public static IEndpointRouteBuilder MapVoiceTranscription(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/management/requests/{requestId:guid}/transcription", ForRequestAsync)
            .RequireAuthorization().DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBytes));
        endpoints.MapPost("/condominiums/{condominiumId:guid}/assistant/transcription", ForAssistantAsync)
            .RequireAuthorization().DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBytes));
        return endpoints;
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.TryParse(
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    private static Task<bool> ActiveUserAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        db.Set<ApplicationUser>().AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive, ct);

    private static async Task<IResult> ForRequestAsync(Guid requestId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext db, IWhatsAppAudioTranscriptionService transcription,
        CancellationToken ct)
    {
        var userId = UserId(principal);
        if (userId == Guid.Empty) return Results.Unauthorized();
        if (!await ActiveUserAsync(db, userId, ct)) return Results.Forbid();
        var condominiumId = await db.Requests.AsNoTracking().Where(item => item.Id == requestId)
            .Select(item => (Guid?)item.CondominiumId).SingleOrDefaultAsync(ct);
        if (condominiumId is null) return Results.NotFound();
        var error = await AuthorizeAsync(condominiumId.Value, userId, false,
            SubManagerModule.Attendance, db, ct);
        return error ?? await TranscribeAsync(request, transcription, ct);
    }

    private static async Task<IResult> ForAssistantAsync(Guid condominiumId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext db, IWhatsAppAudioTranscriptionService transcription,
        CancellationToken ct)
    {
        var userId = UserId(principal);
        if (userId == Guid.Empty) return Results.Unauthorized();
        if (!await ActiveUserAsync(db, userId, ct)) return Results.Forbid();
        var error = await AuthorizeAsync(condominiumId, userId,
            principal.IsInRole(DependencyInjection.PlatformAdminRole), SubManagerModule.Assistant, db, ct);
        return error ?? await TranscribeAsync(request, transcription, ct);
    }

    private static async Task<IResult?> AuthorizeAsync(Guid condominiumId, Guid userId,
        bool platformAdmin, SubManagerModule module, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Condominiums.AsNoTracking().AnyAsync(item => item.Id == condominiumId && item.IsActive, ct))
            return Results.NotFound();
        return platformAdmin || await SubManagerAccess.HasAsync(db, userId, condominiumId, module, ct)
            ? null : Results.Forbid();
    }

    private static async Task<IResult> TranscribeAsync(HttpRequest request,
        IWhatsAppAudioTranscriptionService transcription, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var requestType)
            || !string.Equals(requestType.MediaType, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return InvalidAudio("Envie o áudio no formato multipart/form-data.");
        if (request.ContentLength > MaximumRequestBytes) return TooLarge();

        // Bound chunked bodies too, and keep multipart buffering entirely in memory.
        using var body = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (body.Length + read > MaximumRequestBytes) return TooLarge();
            await body.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        body.Position = 0;
        var originalBody = request.Body;
        request.Body = body;
        try
        {
            request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
            {
                MemoryBufferThreshold = MaximumRequestBytes,
                MultipartBodyLengthLimit = MaximumRequestBytes,
                ValueCountLimit = 8,
                ValueLengthLimit = 1024,
            }));
            IFormCollection form;
            try { form = await request.ReadFormAsync(ct); }
            catch (InvalidDataException) { return InvalidAudio("Não foi possível ler o áudio enviado."); }
            catch (BadHttpRequestException) { return InvalidAudio("Não foi possível ler o áudio enviado."); }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                return InvalidAudio("O envio do áudio está incompleto. Grave novamente.");
            }
            if (form.Files.Count != 1 || form.Files[0].Name != "audio")
                return InvalidAudio("Envie somente um arquivo de áudio no campo audio.");
            var file = form.Files[0];
            if (file.Length > MaximumAudioBytes) return TooLarge();
            if (file.Length == 0) return InvalidAudio("O áudio está vazio. Grave novamente.");
            var format = Format(file.ContentType);
            if (format is null) return InvalidAudio("Formato de áudio não suportado. Use WebM, MP4, M4A ou Ogg.");
            using var audio = new MemoryStream((int)file.Length);
            await file.CopyToAsync(audio, ct);
            var bytes = audio.GetBuffer().AsMemory(0, (int)audio.Length);
            if (!HasSignature(bytes.Span, format.Value.ContentType))
                return InvalidAudio("O conteúdo enviado não corresponde ao formato do áudio.");
            AudioTranscriptionResult result;
            try
            {
                result = await transcription.TranscribeAsync(bytes,
                    format.Value.FileName, format.Value.ContentType, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ProviderFailure("timeout");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return ProviderFailure("provider_error");
            }
            ct.ThrowIfCancellationRequested();
            if (!result.Succeeded) return ProviderFailure(result.Code);
            var text = result.Text?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > MaximumTextLength)
                return ProviderFailure("invalid_response");
            return Results.Ok(new { text });
        }
        finally { request.Body = originalBody; }
    }

    private static (string ContentType, string FileName)? Format(string contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed)) return null;
        return parsed.MediaType?.ToLowerInvariant() switch
        {
            "audio/webm" => ("audio/webm", "recording.webm"),
            "audio/mp4" or "audio/x-m4a" => ("audio/mp4", "recording.m4a"),
            "audio/ogg" => ("audio/ogg", "recording.ogg"),
            _ => null,
        };
    }

    private static bool HasSignature(ReadOnlySpan<byte> bytes, string contentType) => contentType switch
    {
        "audio/webm" => bytes.StartsWith<byte>([0x1a, 0x45, 0xdf, 0xa3]),
        "audio/ogg" => bytes.StartsWith("OggS"u8),
        "audio/mp4" => bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
        _ => false,
    };

    private static IResult InvalidAudio(string message) =>
        Results.BadRequest(new { code = "invalid_audio", message });

    private static IResult TooLarge() => Results.Json(
        new { code = "audio_too_large", message = "O áudio deve ter no máximo 10 MB." },
        statusCode: StatusCodes.Status413PayloadTooLarge);

    private static IResult ProviderFailure(string code) => code switch
    {
        "disabled" or "not_configured" => Results.Json(
            new { code = "transcription_unavailable", message = "A transcrição de áudio está indisponível no momento." },
            statusCode: StatusCodes.Status503ServiceUnavailable),
        "timeout" => Results.Json(
            new { code = "transcription_timeout", message = "A transcrição demorou mais que o esperado. Tente novamente." },
            statusCode: StatusCodes.Status504GatewayTimeout),
        _ => Results.Json(
            new { code = "transcription_failed", message = "Não foi possível transcrever o áudio. Tente novamente." },
            statusCode: StatusCodes.Status502BadGateway),
    };
}
