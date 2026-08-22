using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

/// <summary>
/// A scanned/photographed page has no text layer at all — the most common
/// real-world way a document "silently" never reaches the assistant despite
/// being uploaded successfully. These tests cover the OCR fallback in
/// isolation from the rest of the RAG pipeline: the HTTP call itself
/// (<see cref="OpenAiDocumentOcrService"/>) and the page-selection logic that
/// decides which pages are worth sending for OCR
/// (<see cref="CondominiumDocumentProcessor.OcrMissingPagesAsync"/>).
/// </summary>
public sealed class DocumentOcrTests
{
    [Fact]
    public async Task Disabled_service_never_calls_the_handler()
    {
        var handler = new CountingHandler("");
        var service = Service(handler, enabled: false, apiKey: "test");

        var result = await service.ExtractTextAsync([1, 2, 3], default);

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Missing_api_key_never_calls_the_handler()
    {
        var handler = new CountingHandler("");
        var service = Service(handler, enabled: true, apiKey: null);

        var result = await service.ExtractTextAsync([1, 2, 3], default);

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Successful_call_returns_the_transcribed_text()
    {
        var handler = new CountingHandler("Ata da assembleia de 15/03/2026.");
        var service = Service(handler, enabled: true, apiKey: "test");

        var result = await service.ExtractTextAsync([1, 2, 3], default);

        Assert.Equal("Ata da assembleia de 15/03/2026.", result);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("image_url", handler.LastRequestBody);
    }

    [Fact]
    public async Task Http_failure_returns_null_instead_of_throwing()
    {
        var handler = new CountingHandler("", HttpStatusCode.ServiceUnavailable);
        var service = Service(handler, enabled: true, apiKey: "test");

        var result = await service.ExtractTextAsync([1, 2, 3], default);

        Assert.Null(result);
    }

    [Fact]
    public async Task OcrMissingPages_leaves_pages_with_real_text_untouched()
    {
        var handler = new CountingHandler("texto ocr");
        var processor = Processor(handler, enabled: true);
        var pages = new[]
        {
            new CondominiumDocumentText.ExtractedPage(1, "Texto normal com bastante conteúdo já extraído.", [[1, 2, 3]]),
        };

        var result = await processor.OcrMissingPagesAsync(pages, default);

        Assert.Equal("Texto normal com bastante conteúdo já extraído.", result[0].Text);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OcrMissingPages_transcribes_pages_with_no_text_when_enabled()
    {
        var handler = new CountingHandler("Texto transcrito da imagem.");
        var processor = Processor(handler, enabled: true);
        var pages = new[] { new CondominiumDocumentText.ExtractedPage(1, "", [[1, 2, 3]]) };

        var result = await processor.OcrMissingPagesAsync(pages, default);

        Assert.Equal("Texto transcrito da imagem.", result[0].Text);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OcrMissingPages_leaves_pages_without_images_untouched_even_when_enabled()
    {
        var handler = new CountingHandler("não deveria ser chamado");
        var processor = Processor(handler, enabled: true);
        var pages = new[] { new CondominiumDocumentText.ExtractedPage(1, "", []) };

        var result = await processor.OcrMissingPagesAsync(pages, default);

        Assert.Equal("", result[0].Text);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OcrMissingPages_does_nothing_when_disabled()
    {
        var handler = new CountingHandler("não deveria ser chamado");
        var processor = Processor(handler, enabled: false);
        var pages = new[] { new CondominiumDocumentText.ExtractedPage(1, "", [[1, 2, 3]]) };

        var result = await processor.OcrMissingPagesAsync(pages, default);

        Assert.Equal("", result[0].Text);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OcrMissingPages_respects_the_per_document_page_budget()
    {
        var handler = new CountingHandler("texto");
        var processor = Processor(handler, enabled: true, maximumPagesPerDocument: 1);
        var pages = new[]
        {
            new CondominiumDocumentText.ExtractedPage(1, "", [[1]]),
            new CondominiumDocumentText.ExtractedPage(2, "", [[2]]),
        };

        var result = await processor.OcrMissingPagesAsync(pages, default);

        Assert.Equal(1, handler.Calls);
        Assert.Equal("texto", result[0].Text);
        Assert.Equal("", result[1].Text);
    }

    private static OpenAiDocumentOcrService Service(HttpMessageHandler handler, bool enabled, string? apiKey) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://test/") },
            Options.Create(new DocumentOcrOptions { Enabled = enabled, ApiKey = apiKey, Model = "gpt-4o-mini" }),
            NullLogger<OpenAiDocumentOcrService>.Instance);

    private static CondominiumDocumentProcessor Processor(HttpMessageHandler handler, bool enabled,
        int maximumPagesPerDocument = 30)
    {
        // OcrMissingPagesAsync never touches the database, so an unopened
        // connection is fine — nothing here ever issues a query against it.
        var db = new CondoLink.Infrastructure.Persistence.AppDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<CondoLink.Infrastructure.Persistence.AppDbContext>()
                .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")).Options);
        var ocr = Service(handler, enabled, "test");
        return new(db, new UnusedEmbeddingService(), ocr,
            Options.Create(new CondominiumAssistantOptions()),
            Options.Create(new DocumentOcrOptions { Enabled = enabled, MaximumPagesPerDocument = maximumPagesPerDocument }),
            NullLogger<CondominiumDocumentProcessor>.Instance);
    }

    private sealed class UnusedEmbeddingService : IEmbeddingService
    {
        public string Model => "unused";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Embeddings must not run in these tests.");
    }

    private sealed class CountingHandler(string answer, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string LastRequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (status != HttpStatusCode.OK) return new HttpResponseMessage(status);
            var envelope = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
        }
    }
}
