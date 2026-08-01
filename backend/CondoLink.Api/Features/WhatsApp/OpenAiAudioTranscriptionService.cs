using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text.Json;
using CondoLink.Api.Features.RequestAttachments;
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

public interface IOpenAiAudioDiagnostics
{
    Task<OpenAiDiagnosticResponse> CheckAsync(CancellationToken cancellationToken);
}

public sealed record OpenAiDiagnosticResponse(
    bool Success,
    int? StatusCode,
    string? ReasonPhrase,
    long ElapsedMilliseconds,
    string? ExceptionType,
    string? InnerExceptionType,
    string FailureType,
    string BaseUrl,
    string Model);

public sealed record AudioTranscriptionResult(bool Succeeded, string? Text, string Code);

public enum AudioTranscriptionOutcome
{
    Disabled,
    NotConfigured,
    Timeout,
    HttpUnauthorized,
    HttpRateLimited,
    HttpBadRequest,
    ProviderError,
    EmptyResponse,
    Succeeded
}

public sealed class OpenAiAudioTranscriptionService : IWhatsAppAudioTranscriptionService,
    IOpenAiAudioDiagnostics
{
    private readonly HttpClient httpClient;
    private readonly IOptions<RequestDraftAiAudioOptions> options;
    private readonly ILogger<OpenAiAudioTranscriptionService> logger;
    private readonly Func<ReadOnlyMemory<byte>, HttpContent> audioContentFactory;
    private readonly Action<MultipartFormDataContent, HttpContent, string> addFile;

    public OpenAiAudioTranscriptionService(HttpClient httpClient,
        IOptions<RequestDraftAiAudioOptions> options,
        ILogger<OpenAiAudioTranscriptionService> logger)
        : this(httpClient, options, logger,
            bytes => new ByteArrayContent(bytes.ToArray()),
            (form, content, name) => form.Add(content, "file", name))
    {
    }

    internal OpenAiAudioTranscriptionService(HttpClient httpClient,
        IOptions<RequestDraftAiAudioOptions> options,
        ILogger<OpenAiAudioTranscriptionService> logger,
        Func<ReadOnlyMemory<byte>, HttpContent> audioContentFactory,
        Action<MultipartFormDataContent, HttpContent, string> addFile)
    {
        this.httpClient = httpClient;
        this.options = options;
        this.logger = logger;
        this.audioContentFactory = audioContentFactory;
        this.addFile = addFile;
    }

    public async Task<OpenAiDiagnosticResponse> CheckAsync(
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var stopwatch = Stopwatch.StartNew();
        var baseUrl = SafeBaseUrl(httpClient.BaseAddress, settings.BaseUrl);
        logger.LogInformation(
            "Starting OpenAI HTTPS diagnostic. BaseUrl: {BaseUrl}; Model: {Model}.",
            baseUrl, settings.Model);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI HTTPS diagnostic not configured. FailureType: {FailureType}; ElapsedMilliseconds: {ElapsedMilliseconds}.",
                AudioTranscriptionOutcome.NotConfigured, stopwatch.ElapsedMilliseconds);
            return Diagnostic(false, null, null, stopwatch.ElapsedMilliseconds,
                null, null, AudioTranscriptionOutcome.NotConfigured.ToString(), baseUrl,
                settings.Model);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "models");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", settings.ApiKey);
            logger.LogInformation("Sending OpenAI HTTPS diagnostic request.");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            var failureType = response.IsSuccessStatusCode
                ? AudioTranscriptionOutcome.Succeeded
                : HttpOutcome(response.StatusCode);
            logger.LogInformation(
                "OpenAI HTTPS diagnostic response received. StatusCode: {StatusCode}; FailureType: {FailureType}; ElapsedMilliseconds: {ElapsedMilliseconds}.",
                (int)response.StatusCode, failureType, stopwatch.ElapsedMilliseconds);
            return Diagnostic(response.IsSuccessStatusCode, (int)response.StatusCode,
                response.ReasonPhrase, stopwatch.ElapsedMilliseconds, null, null,
                failureType.ToString(), baseUrl, settings.Model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var failureType = exception is OperationCanceledException
                ? AudioTranscriptionOutcome.Timeout
                : AudioTranscriptionOutcome.ProviderError;
            logger.LogWarning(
                "OpenAI HTTPS diagnostic exception. ExceptionType: {ExceptionType}; InnerExceptionType: {InnerExceptionType}; FailureType: {FailureType}; ElapsedMilliseconds: {ElapsedMilliseconds}.",
                exception.GetType().Name, exception.InnerException?.GetType().Name,
                failureType, stopwatch.ElapsedMilliseconds);
            return Diagnostic(false, null, null, stopwatch.ElapsedMilliseconds,
                exception.GetType().Name, exception.InnerException?.GetType().Name,
                failureType.ToString(), baseUrl, settings.Model);
        }
    }

    private static OpenAiDiagnosticResponse Diagnostic(bool success, int? statusCode,
        string? reasonPhrase, long elapsedMilliseconds, string? exceptionType,
        string? innerExceptionType, string failureType, string baseUrl, string model) =>
        new(success, statusCode, reasonPhrase, elapsedMilliseconds, exceptionType,
            innerExceptionType, failureType, baseUrl, model);

    public async Task<AudioTranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> audio, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("WhatsApp audio transcription is disabled.");
            return Failure(AudioTranscriptionOutcome.Disabled);
        }
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("WhatsApp audio transcription API key is missing.");
            return Failure(AudioTranscriptionOutcome.NotConfigured);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 120)));
        var stage = "locating_audio";
        try
        {
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: FileLocated.");
            stage = "opening_audio";
            using var audioContent = audioContentFactory(audio);
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: FileOpened.");

            stage = "normalizing_mime";
            var format = AttachmentPolicy.ResolveAudioMultipartFormat(contentType)
                ?? throw new NotSupportedException("Unsupported audio media type.");
            logger.LogDebug(
                "WhatsApp audio transcription preparation. Stage: MimeNormalized; MultipartMediaType: {MultipartMediaType}.",
                format.ContentType);
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: SafeFileNameCreated.");

            stage = "creating_multipart";
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(settings.Model), "model");
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(format.ContentType);
            addFile(form, audioContent, format.FileName);
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: MultipartCreated.");

            stage = "creating_request";
            using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Content = form;
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: HttpRequestCreated.");
            logger.LogInformation(
                "Starting WhatsApp audio transcription. Model: {Model}; BaseUrl: {BaseUrl}.",
                settings.Model, SafeBaseUrl(httpClient.BaseAddress, settings.BaseUrl));
            stage = "sending_http";
            logger.LogDebug("WhatsApp audio transcription preparation. Stage: SendAsyncStarted.");
            using var response = await httpClient.SendAsync(request, timeout.Token);
            stage = "reading_response";
            logger.LogInformation("WhatsApp audio transcription HTTP status received: {StatusCode}.",
                (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ProviderErrorMetadata(response, timeout.Token);
                var outcome = HttpOutcome(response.StatusCode);
                logger.LogWarning(
                    "WhatsApp audio transcription HTTP failure. StatusCode: {StatusCode}; ErrorType: {ErrorType}; ErrorCode: {ErrorCode}; ErrorParam: {ErrorParam}; Outcome: {Outcome}.",
                    (int)response.StatusCode, error.Type, error.Code, error.Param, outcome);
                return Failure(outcome);
            }
            using var document = await JsonDocument.ParseAsync(await response.Content
                .ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            logger.LogDebug("WhatsApp audio transcription response deserialized.");
            if (!document.RootElement.TryGetProperty("text", out var text)
                || text.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(text.GetString()))
                return Failure(AudioTranscriptionOutcome.EmptyResponse);
            logger.LogInformation("WhatsApp audio transcription succeeded. Outcome: {Outcome}.",
                AudioTranscriptionOutcome.Succeeded);
            return Success(text.GetString()!.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "WhatsApp audio transcription timed out. Stage: {Stage}; FailureType: {FailureType}; Outcome: {Outcome}.",
                stage, nameof(OperationCanceledException), AudioTranscriptionOutcome.Timeout);
            return Failure(AudioTranscriptionOutcome.Timeout);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "WhatsApp audio transcription failed. Stage: {Stage}; FailureType: {FailureType}; Outcome: {Outcome}.",
                stage, exception.GetType().Name, AudioTranscriptionOutcome.ProviderError);
            return Failure(AudioTranscriptionOutcome.ProviderError);
        }
    }

    private static AudioTranscriptionResult Failure(AudioTranscriptionOutcome outcome) =>
        new(false, null, Code(outcome));

    private static AudioTranscriptionResult Success(string text) =>
        new(true, text, Code(AudioTranscriptionOutcome.Succeeded));

    private static string Code(AudioTranscriptionOutcome outcome) => outcome switch
    {
        AudioTranscriptionOutcome.NotConfigured => "not_configured",
        AudioTranscriptionOutcome.HttpUnauthorized => "http_unauthorized",
        AudioTranscriptionOutcome.HttpRateLimited => "http_rate_limited",
        AudioTranscriptionOutcome.HttpBadRequest => "http_bad_request",
        AudioTranscriptionOutcome.EmptyResponse => "empty_response",
        AudioTranscriptionOutcome.ProviderError => "provider_error",
        _ => outcome.ToString().ToLowerInvariant()
    };

    private static AudioTranscriptionOutcome HttpOutcome(System.Net.HttpStatusCode status) =>
        status switch
        {
            System.Net.HttpStatusCode.BadRequest => AudioTranscriptionOutcome.HttpBadRequest,
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                AudioTranscriptionOutcome.HttpUnauthorized,
            System.Net.HttpStatusCode.TooManyRequests =>
                AudioTranscriptionOutcome.HttpRateLimited,
            _ => AudioTranscriptionOutcome.ProviderError
        };

    private static async Task<ProviderError> ProviderErrorMetadata(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("error", out var error)) return new();
            return new(Optional(error, "type"), Optional(error, "code"),
                Optional(error, "param"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new();
        }
    }

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private sealed record ProviderError(string? Type = null, string? Code = null,
        string? Param = null);

    private static string SafeBaseUrl(Uri? clientBaseAddress, string configuredBaseUrl)
    {
        var value = clientBaseAddress?.ToString() ?? configuredBaseUrl;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Path)
            : value.Split('?', 2)[0];
    }
}
