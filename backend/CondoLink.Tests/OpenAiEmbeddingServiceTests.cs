using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class OpenAiEmbeddingServiceTests
{
    [Fact]
    public async Task Batch_uses_configured_model_and_returns_vectors_in_input_order()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://api.openai.com/v1/") };
        var service = new OpenAiEmbeddingService(client,
            Options.Create(new RequestDraftAiOptions { Enabled = true, ApiKey = "test-key" }),
            Options.Create(new CondominiumAssistantOptions { EmbeddingModel = "text-embedding-3-small" }),
            NullLogger<OpenAiEmbeddingService>.Instance);

        var vectors = await service.EmbedBatchAsync(["primeiro", "segundo"], default);

        Assert.Equal([1f, 0f], vectors[0]);
        Assert.Equal([0f, 1f], vectors[1]);
        Assert.Equal("Bearer test-key", handler.Authorization);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("text-embedding-3-small", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("input").GetArrayLength());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string response = """{"data":[{"index":1,"embedding":[0,1]},{"index":0,"embedding":[1,0]}],"usage":{"total_tokens":4}}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
