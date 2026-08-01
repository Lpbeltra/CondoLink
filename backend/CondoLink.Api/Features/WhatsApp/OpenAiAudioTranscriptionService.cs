using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class RequestDraftAiAudioOptions
{
    public const string SectionName = "RequestDraftAiAudio";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string Model { get; set; } = "gpt-4o-mini-transcribe";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public interface IWhatsAppAudioTranscriptionService
{
    Task<AudioTranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> audio, string fileName, string contentType,
        CancellationToken cancellationToken);
}

public sealed record AudioTranscriptionResult(bool Succeeded, string? Text, string Code);

public sealed class OpenAiAudioTranscriptionService(HttpClient httpClient,
    IOptions<RequestDraftAiAudioOptions> options,
    ILogger<OpenAiAudioTranscriptionService> logger)
    : IWhatsAppAudioTranscriptionService
{
    public async Task<AudioTranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> audio, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("WhatsApp audio transcription is disabled.");
            return Failure("disabled");
        }
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("WhatsApp audio transcription API key is missing.");
            return Failure("not_configured");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 120)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(settings.Model), "model");
        var audioContent = new ByteArrayContent(audio.ToArray());
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(audioContent, "file", Path.GetFileName(fileName));
        request.Content = form;

        try
        {
            logger.LogInformation(
                "Starting WhatsApp audio transcription. Model: {Model}; BaseUrl: {BaseUrl}.",
                settings.Model, SafeBaseUrl(httpClient.BaseAddress, settings.BaseUrl));
            using var response = await httpClient.SendAsync(request, timeout.Token);
            logger.LogInformation("WhatsApp audio transcription HTTP status: {StatusCode}.",
                (int)response.StatusCode);
            if (!response.IsSuccessStatusCode) return Failure("provider_error");
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token),
                cancellationToken: timeout.Token);
            if (!document.RootElement.TryGetProperty("text", out var text)
                || text.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(text.GetString()))
                return Failure("empty_response");
            logger.LogInformation("WhatsApp audio transcription succeeded.");
            return new(true, text.GetString()!.Trim(), "succeeded");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("WhatsApp audio transcription timed out.");
            return Failure("timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            logger.LogWarning(
                "WhatsApp audio transcription failed. FailureType: {FailureType}.",
                exception.GetType().Name);
            return Failure("provider_error");
        }
    }

    private static AudioTranscriptionResult Failure(string code) => new(false, null, code);

    private static string SafeBaseUrl(Uri? clientBaseAddress, string configuredBaseUrl)
    {
        var value = clientBaseAddress?.ToString() ?? configuredBaseUrl;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path)
            : value.Split('?', 2)[0];
    }
}
