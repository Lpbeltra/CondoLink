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

public sealed class CondominiumAssistantRetrievalTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!; private Guid condominiumId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync(); db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        condominiumId = Guid.NewGuid();
        Add("Dever de não prejudicar o sossego, respeitar a Lei do Silêncio e evitar ruídos.", 1, 10);
        Add("Mudanças são permitidas em dias úteis, dentro do horário estabelecido.", 2, 20);
        Add("É vedada a cessão da vaga de garagem a terceiros estranhos ao condomínio.", 3, 30);
        Add("Ar condicionado exige autorização, profissional habilitado e adequação elétrica.", 4, 40);
        Add("A convocação da assembleia deve ocorrer com antecedência mínima de oito dias.", 5, 50);
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("O que o regimento diz sobre barulho?", "sossego")]
    [InlineData("Posso fazer mudança no domingo?", "Mudanças")]
    [InlineData("Posso emprestar minha vaga para uma pessoa de fora?", "terceiros")]
    [InlineData("Posso instalar ar condicionado?", "profissional habilitado")]
    [InlineData("Qual antecedência para convocar assembleia?", "oito dias")]
    public async Task Semantic_queries_retrieve_the_expected_rule(string query, string expected)
    {
        var results = await Service().RetrieveAsync(condominiumId, query, null, default);
        Assert.NotEmpty(results); Assert.Contains(expected, results[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_subject_returns_no_low_confidence_chunks()
    {
        Assert.Empty(await Service().RetrieveAsync(condominiumId,
            "Qual é a regra para criação de abelhas?", null, default));
    }

    [Fact]
    public async Task Request_hint_enriches_an_ambiguous_question()
    {
        var results = await Service().RetrieveAsync(condominiumId, "O que posso fazer nesse caso?",
            "Reclamação de barulho recorrente durante a madrugada", default);
        Assert.Contains("sossego", results[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    private CondominiumAssistantService Service() => new(db, new SemanticTestEmbeddingService(),
        new HttpClient(), Options.Create(new RequestDraftAiOptions()),
        Options.Create(new CondominiumAssistantOptions { MinimumRelevanceScore = .2 }),
        NullLogger<CondominiumAssistantService>.Instance);

    private void Add(string content, int topic, int page)
    {
        var document = new CondominiumDocument(condominiumId, $"Documento {topic}", CondominiumDocumentType.InternalRules,
            $"doc-{topic}.pdf", $"key-{topic}", "application/pdf", 1, null, Guid.NewGuid()); document.Ready();
        db.Add(document); db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 0, content,
            JsonSerializer.Serialize(Vector(topic)), page, null, "semantic-test-v1"));
    }

    private static float[] Vector(int topic) { var result = new float[5]; result[topic - 1] = 1; return result; }

    private sealed class SemanticTestEmbeddingService : IEmbeddingService
    {
        public string Model => "semantic-test-v1";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            var normalized = text.ToLowerInvariant();
            var topic = normalized.Contains("barulho") ? 1 : normalized.Contains("mudança") ? 2
                : normalized.Contains("vaga") ? 3 : normalized.Contains("condicionado") ? 4
                : normalized.Contains("assembleia") ? 5 : 0;
            return Task.FromResult(topic == 0 ? new float[5] : Vector(topic));
        }
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
}
