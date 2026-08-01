using System.Net;
using System.Text;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class OpenAiAudioTranscriptionServiceTests
{
    [Fact]
    public async Task Sends_audio_as_multipart_with_configured_model()
    {
        string? body = null;
        string? mediaType = null;
        var service = Service(async (request, ct) =>
        {
            mediaType = request.Content!.Headers.ContentType!.MediaType;
            body = await request.Content.ReadAsStringAsync(ct);
            return JsonResponse("{\"text\":\"  Relato transcrito.  \"}");
        });

        var result = await service.TranscribeAsync(
            new byte[] { 1, 2, 3 }, "audio.ogg", "audio/ogg", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Relato transcrito.", result.Text);
        Assert.Equal("multipart/form-data", mediaType);
        Assert.Contains("gpt-audio-test", body);
        Assert.Contains("audio.ogg", body);
    }

    [Theory]
    [InlineData(false, "key", "disabled")]
    [InlineData(true, null, "not_configured")]
    public async Task Disabled_or_unconfigured_does_not_call_provider(
        bool enabled, string? apiKey, string expected)
    {
        var called = false;
        var service = Service((_, _) =>
        {
            called = true;
            return Task.FromResult(JsonResponse("{}"));
        }, enabled: enabled, apiKey: apiKey);

        var result = await service.TranscribeAsync(
            new byte[] { 1 }, "audio.ogg", "audio/ogg", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Code);
        Assert.False(called);
    }

    [Fact]
    public async Task Timeout_returns_safe_failure()
    {
        var service = Service(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return JsonResponse("{}");
        }, timeoutSeconds: 1);

        var result = await service.TranscribeAsync(
            new byte[] { 1 }, "audio.ogg", "audio/ogg", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("timeout", result.Code);
    }

    [Fact]
    public async Task Logs_only_technical_metadata()
    {
        var logger = new RecordingLogger<OpenAiAudioTranscriptionService>();
        var service = Service((_, _) => Task.FromResult(new HttpResponseMessage(
            HttpStatusCode.BadRequest)
        {
            Content = new StringContent("audio-secret transcription-secret")
        }), logger: logger, baseUrl: "https://api.openai.com/v1/?secret=query");

        await service.TranscribeAsync(
            Encoding.UTF8.GetBytes("audio-secret"), "secret-name.ogg", "audio/ogg",
            CancellationToken.None);

        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("400", logs);
        Assert.Contains("gpt-audio-test", logs);
        Assert.DoesNotContain("audio-secret", logs);
        Assert.DoesNotContain("transcription-secret", logs);
        Assert.DoesNotContain("secret-name", logs);
        Assert.DoesNotContain("secret=query", logs);
        Assert.DoesNotContain("test-key", logs);
    }

    private static OpenAiAudioTranscriptionService Service(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        bool enabled = true, string? apiKey = "test-key", int timeoutSeconds = 15,
        ILogger<OpenAiAudioTranscriptionService>? logger = null,
        string baseUrl = "https://api.openai.com/v1/")
    {
        var client = new HttpClient(new DelegateHandler(send))
        {
            BaseAddress = new Uri(baseUrl)
        };
        return new OpenAiAudioTranscriptionService(client,
            Options.Create(new RequestDraftAiAudioOptions
            {
                Enabled = enabled,
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                Model = "gpt-audio-test",
                TimeoutSeconds = timeoutSeconds
            }), logger ?? NullLogger<OpenAiAudioTranscriptionService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
