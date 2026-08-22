using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

/// <summary>
/// <see cref="CondominiumAssistantService.AskStreamAsync"/> reuses the exact
/// retrieval/prompt pipeline <c>AskAsync</c> already has coverage for elsewhere;
/// these tests focus on what is actually new — the sources/token callbacks and
/// the OpenAI SSE parsing in the private streaming chat call.
/// </summary>
public sealed class CondominiumAssistantStreamingTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private Guid condominiumId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        condominiumId = Guid.NewGuid();
        var document = new CondominiumDocument(condominiumId, "Regimento", CondominiumDocumentType.InternalRules,
            "regimento.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        document.Ready();
        db.Add(document);
        db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 0,
            "Dever de não prejudicar o sossego, respeitar a Lei do Silêncio e evitar ruídos.",
            JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "streaming-test-v1"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Catalog_question_reports_empty_sources_and_a_single_token_before_the_final_answer()
    {
        var active = new CondominiumDocument(condominiumId, "Ata atual", CondominiumDocumentType.Minutes,
            "ata.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        active.Ready();
        db.Add(active);
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Catálogo");
        var reportedSources = new List<IReadOnlyList<AssistantSource>>();
        var tokens = new List<string>();

        var answer = await Service().AskStreamAsync(conversation, "Quais documentos você possui?",
            (sources, _) => { reportedSources.Add(sources); return Task.CompletedTask; },
            (delta, _) => { tokens.Add(delta); return Task.CompletedTask; },
            default);

        Assert.Equal("structured-catalog", answer.Model);
        Assert.Empty(answer.Sources);
        var sourcesEvent = Assert.Single(reportedSources);
        Assert.Empty(sourcesEvent);
        var token = Assert.Single(tokens);
        Assert.Equal(answer.Answer, token);
        Assert.Contains("Ata atual", answer.Answer);
    }

    [Fact]
    public async Task Streamed_chat_completion_accumulates_deltas_and_reports_sources_before_the_first_token()
    {
        var handler = new StreamingChatHandler();
        var ai = new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" };
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Barulho");
        var tokens = new List<string>();
        var sourcesReceivedBeforeFirstToken = false;
        var sourcesSeen = false;

        var answer = await Service(new SemanticTestEmbeddingService(), new HttpClient(handler)
            { BaseAddress = new Uri("https://test/") }, ai)
            .AskStreamAsync(conversation, "O que o regimento diz sobre barulho?",
                (sources, _) =>
                {
                    sourcesSeen = true;
                    sourcesReceivedBeforeFirstToken = tokens.Count == 0;
                    Assert.NotEmpty(sources);
                    return Task.CompletedTask;
                },
                (delta, _) => { tokens.Add(delta); return Task.CompletedTask; },
                default);

        Assert.True(sourcesSeen);
        Assert.True(sourcesReceivedBeforeFirstToken);
        Assert.Equal("Olá mundo!", string.Concat(tokens));
        Assert.Equal("Olá mundo!", answer.Answer);
        Assert.Equal("test-chat", answer.Model);
    }

    private CondominiumAssistantService Service(IEmbeddingService? embedding = null,
        HttpClient? client = null, RequestDraftAiOptions? ai = null) => new(db,
        embedding ?? new SemanticTestEmbeddingService(), client ?? new HttpClient(),
        Options.Create(ai ?? new RequestDraftAiOptions()),
        Options.Create(new CondominiumAssistantOptions { MinimumRelevanceScore = .2 }),
        NullLogger<CondominiumAssistantService>.Instance);

    private sealed class SemanticTestEmbeddingService : IEmbeddingService
    {
        public string Model => "streaming-test-v1";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(text.ToLowerInvariant().Contains("barulho")
                ? new float[] { 1, 0 } : new float[2]);
    }

    /// <summary>
    /// Fails query expansion and rerank calls on purpose (forcing the heuristic
    /// fallback already covered elsewhere) and only answers the final streaming
    /// chat completion, with a body shaped like a real OpenAI SSE response
    /// including an empty line, a non-JSON keep-alive-like line and [DONE].
    /// </summary>
    private sealed class StreamingChatHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("\"stream\":true", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            const string sse = """
                data: {"choices":[{"delta":{"content":"Olá "}}]}

                data: {"choices":[{"delta":{}}]}

                data: {"choices":[{"delta":{"content":"mundo!"}}]}

                data: [DONE]

                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            };
        }
    }
}
