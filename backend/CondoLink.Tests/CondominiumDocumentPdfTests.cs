using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CondoLink.Tests;

public sealed class CondominiumDocumentPdfTests
{
    [Fact]
    public void Extracts_text_page_by_page_from_real_pdf_bytes()
    {
        using var stream = Pdf("Convencao do condominio e areas comuns.",
            "Regimento interno e horario da piscina.");
        Assert.Contains("/FlateDecode", Encoding.Latin1.GetString(stream.ToArray()));

        var pages = CondominiumDocumentText.ExtractPages(stream, ".pdf");
        var chunks = CondominiumDocumentText.Chunks(pages, 500, 80);

        Assert.Equal(2, pages.Count);
        Assert.Contains("Convencao", pages[0].Text);
        Assert.Contains("Regimento", pages[1].Text);
        Assert.Contains(chunks, chunk => chunk.PageNumber == 1 && chunk.Content.Contains("Convencao"));
        Assert.Contains(chunks, chunk => chunk.PageNumber == 2 && chunk.Content.Contains("Regimento"));
    }

    [Fact]
    public void Extracts_pdf_with_embedded_type1_font_and_custom_encoding()
    {
        var encoded = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "CondominiumAssistant", "embedded-type1.pdf.base64"));
        using var stream = new MemoryStream(Convert.FromBase64String(encoded));

        var pages = CondominiumDocumentText.ExtractPages(stream, ".pdf");

        Assert.Single(pages);
        Assert.False(string.IsNullOrWhiteSpace(pages[0].Text));
        Assert.Equal(1, pages[0].PageNumber);
    }

    [Fact]
    public async Task Textual_pdf_becomes_ready_with_page_aware_chunks()
    {
        await using var scope = await ProcessorScope.Create();
        using var stream = Pdf("Convencao com regras suficientes para indexacao e consulta pelos moradores.");

        await scope.Processor.ProcessAsync(scope.Document, stream, ".pdf", default);

        Assert.Equal(CondominiumDocumentProcessingStatus.Ready, scope.Document.ProcessingStatus);
        var chunks = await scope.Db.CondominiumDocumentChunks.ToArrayAsync();
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal(1, chunk.PageNumber));
    }

    [Fact]
    public async Task Pdf_without_extractable_text_is_unsupported_and_has_no_chunks()
    {
        await using var scope = await ProcessorScope.Create();
        using var stream = Pdf((string?)null);

        await scope.Processor.ProcessAsync(scope.Document, stream, ".pdf", default);

        Assert.Equal(CondominiumDocumentProcessingStatus.Unsupported, scope.Document.ProcessingStatus);
        Assert.Equal("Não foi possível extrair texto deste PDF. O documento pode ser digitalizado como imagem.",
            scope.Document.ProcessingError);
        Assert.Empty(await scope.Db.CondominiumDocumentChunks.ToArrayAsync());
    }

    [Fact]
    public async Task Corrupt_pdf_has_safe_error_and_no_chunks()
    {
        await using var scope = await ProcessorScope.Create();
        using var stream = new MemoryStream("%PDF-corrompido-binário-\\q"u8.ToArray());

        await scope.Processor.ProcessAsync(scope.Document, stream, ".pdf", default);

        Assert.Equal(CondominiumDocumentProcessingStatus.Failed, scope.Document.ProcessingStatus);
        Assert.Equal("Não foi possível processar este PDF.", scope.Document.ProcessingError);
        Assert.Empty(await scope.Db.CondominiumDocumentChunks.ToArrayAsync());
    }

    [Fact]
    public async Task Embedding_failure_does_not_persist_partial_chunks()
    {
        await using var scope = await ProcessorScope.Create(new FailingEmbeddingService());
        using var stream = Pdf(string.Join(' ', Enumerable.Repeat("regra condominial valida", 200)));

        await scope.Processor.ProcessAsync(scope.Document, stream, ".pdf", default);

        Assert.Equal(CondominiumDocumentProcessingStatus.Failed, scope.Document.ProcessingStatus);
        Assert.Equal("Não foi possível indexar o documento no momento. Tente reprocessá-lo mais tarde.", scope.Document.ProcessingError);
        Assert.Empty(await scope.Db.CondominiumDocumentChunks.ToArrayAsync());
    }

    private static MemoryStream Pdf(params string?[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pageTexts)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text is not null) page.AddText(text, 12, new PdfPoint(40, 780), font);
        }
        return new MemoryStream(builder.Build());
    }

    private sealed class FailingEmbeddingService : IEmbeddingService
    {
        private int calls;
        public string Model => "test";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            ++calls > 1 ? throw new InvalidOperationException("technical embedding failure") : Task.FromResult(new[] { 1f });
    }

    private sealed class ProcessorScope : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public CondominiumDocument Document { get; }
        public CondominiumDocumentProcessor Processor { get; }

        private ProcessorScope(SqliteConnection connection, AppDbContext db,
            CondominiumDocument document, IEmbeddingService embeddings)
        {
            this.connection = connection; Db = db; Document = document;
            Processor = new(db, embeddings, Options.Create(new CondominiumAssistantOptions()),
                NullLogger<CondominiumDocumentProcessor>.Instance);
        }

        public static async Task<ProcessorScope> Create(IEmbeddingService? embeddings = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var document = new CondominiumDocument(Guid.NewGuid(), "Convenção",
                CondominiumDocumentType.Convention, "convencao.pdf", "test.pdf",
                "application/pdf", 1, null, Guid.NewGuid());
            db.CondominiumDocuments.Add(document);
            await db.SaveChangesAsync();
            return new(connection, db, document, embeddings ?? new LocalEmbeddingService());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
