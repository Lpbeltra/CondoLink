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
/// A very common real-world failure looks identical to "the assistant doesn't
/// know" from the user's side: they upload the one document that would answer
/// their question, but it fails to process (most often a scanned/photographed
/// PDF with no extractable text), so retrieval finds nothing. These tests pin
/// that the assistant appends an actionable hint in exactly that situation, and
/// stays silent about it whenever there is nothing broken to report.
/// </summary>
public sealed class CondominiumAssistantUnprocessedDocumentsHintTests : IAsyncLifetime
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
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Appends_a_hint_when_no_evidence_was_found_and_a_document_failed_to_process()
    {
        var scanned = new CondominiumDocument(condominiumId, "Ata escaneada", CondominiumDocumentType.Minutes,
            "ata.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        scanned.Fail("Não foi possível extrair texto deste PDF.", unsupported: true);
        db.Add(scanned);
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Assembleia");

        var answer = await Service(new NoOpChatHandler("Não encontrei essa informação.")).AskAsync(
            conversation, "Quando foi a assembleia em que fui eleito síndico?", default);

        Assert.Contains("Não encontrei essa informação.", answer.Answer);
        Assert.Contains("1 documento que não pôde ser processado", answer.Answer);
        Assert.Contains("página Documentos", answer.Answer);
    }

    [Fact]
    public async Task Counts_multiple_failed_documents_with_plural_wording()
    {
        var first = new CondominiumDocument(condominiumId, "Ata 1", CondominiumDocumentType.Minutes,
            "a.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        first.Fail("sem texto", unsupported: true);
        var second = new CondominiumDocument(condominiumId, "Ata 2", CondominiumDocumentType.Minutes,
            "b.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        second.Fail("erro de leitura");
        db.AddRange(first, second);
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Assembleia");

        var answer = await Service(new NoOpChatHandler("Sem base suficiente.")).AskAsync(
            conversation, "Quando foi a assembleia?", default);

        Assert.Contains("2 documentos que não puderam ser processados", answer.Answer);
    }

    [Fact]
    public async Task Does_not_append_a_hint_when_nothing_failed_to_process()
    {
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Assembleia");

        var answer = await Service(new NoOpChatHandler("Não encontrei essa informação.")).AskAsync(
            conversation, "Quando foi a assembleia?", default);

        Assert.Equal("Não encontrei essa informação.", answer.Answer);
    }

    [Fact]
    public async Task Does_not_append_a_hint_when_evidence_was_actually_found()
    {
        var document = new CondominiumDocument(condominiumId, "Regimento", CondominiumDocumentType.InternalRules,
            "regimento.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        document.Ready();
        var failed = new CondominiumDocument(condominiumId, "Ata escaneada", CondominiumDocumentType.Minutes,
            "ata.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        failed.Fail("sem texto", unsupported: true);
        db.AddRange(document, failed);
        db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 0,
            "Dever de não prejudicar o sossego, respeitar a Lei do Silêncio e evitar ruídos.",
            JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "hint-test-v1"));
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Barulho");

        var answer = await Service(new NoOpChatHandler("O regimento proíbe barulho após as 22h."),
            new SemanticTestEmbeddingService()).AskAsync(
            conversation, "O que o regimento diz sobre barulho?", default);

        Assert.Equal("O regimento proíbe barulho após as 22h.", answer.Answer);
    }

    [Fact]
    public async Task Streams_the_hint_as_an_additional_token_after_the_answer()
    {
        var scanned = new CondominiumDocument(condominiumId, "Ata escaneada", CondominiumDocumentType.Minutes,
            "ata.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
        scanned.Fail("sem texto", unsupported: true);
        db.Add(scanned);
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Assembleia");
        var tokens = new List<string>();

        var answer = await Service(new NoOpChatHandler("Não encontrei essa informação.")).AskStreamAsync(
            conversation, "Quando foi a assembleia?",
            (_, _) => Task.CompletedTask,
            (delta, _) => { tokens.Add(delta); return Task.CompletedTask; },
            default);

        Assert.Contains("1 documento que não pôde ser processado", answer.Answer);
        Assert.Contains("1 documento que não pôde ser processado", string.Concat(tokens));
        Assert.Equal(answer.Answer, string.Concat(tokens));
    }

    private CondominiumAssistantService Service(HttpMessageHandler handler, CondoLink.Api.Features.CondominiumAssistant.IEmbeddingService? embedding = null) => new(db,
        embedding ?? new EmptyEmbeddingService(),
        new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
        Options.Create(new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" }),
        Options.Create(new CondominiumAssistantOptions { MinimumRelevanceScore = .2 }),
        NullLogger<CondominiumAssistantService>.Instance);

    private sealed class EmptyEmbeddingService : CondoLink.Api.Features.CondominiumAssistant.IEmbeddingService
    {
        public string Model => "hint-test-v1";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new float[2]);
    }

    private sealed class SemanticTestEmbeddingService : CondoLink.Api.Features.CondominiumAssistant.IEmbeddingService
    {
        public string Model => "hint-test-v1";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(text.ToLowerInvariant().Contains("barulho") ? new float[] { 1, 0 } : new float[2]);
    }

    /// <summary>Answers query-expansion calls with no extra queries and every chat/completions call (streaming or not) with a fixed answer.</summary>
    private sealed class NoOpChatHandler(string answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (body.Contains("busca documental", StringComparison.Ordinal))
            {
                var envelope = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"queries\":[]}" } } } });
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
            }
            if (body.Contains("\"stream\":true", StringComparison.Ordinal))
            {
                var sse = $"data: {{\"choices\":[{{\"delta\":{{\"content\":{JsonSerializer.Serialize(answer)}}}}}]}}\n\ndata: [DONE]\n\n";
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") };
            }
            var normal = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(normal, Encoding.UTF8, "application/json") };
        }
    }
}
