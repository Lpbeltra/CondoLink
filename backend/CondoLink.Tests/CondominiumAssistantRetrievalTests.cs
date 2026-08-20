using System.Text.Json;
using System.Net;
using System.Text;
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
    public async Task Model_expansion_finds_semantically_equivalent_rule_in_third_document_and_reranks_it()
    {
        var investigativeCondominium = Guid.NewGuid();
        AddInvestigative(investigativeCondominium, "Manual da garagem",
            "As vagas devem permanecer sinalizadas.", new float[] { 0, 1, 0 });
        AddInvestigative(investigativeCondominium, "Convenção geral",
            "Os condôminos devem preservar o patrimônio.", new float[] { 0, 0, 1 });
        var expected = AddInvestigative(investigativeCondominium, "Regimento interno",
            "Animais devem ser conduzidos nas áreas comuns conforme as regras de circulação.",
            new float[] { 1, 0, 0 });
        await db.SaveChangesAsync();
        var handler = new InvestigativeChatHandler(
            ["animais nas áreas comuns", "circulação de pets", "uso do elevador por animais"]);
        var ai = new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" };

        var results = await Service(new InvestigativeEmbeddingService(),
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, ai)
            .RetrieveAsync(investigativeCondominium,
                "Meu cachorro pode andar no elevador?", null, default);

        Assert.NotEmpty(results);
        Assert.Equal(expected.Id, results[0].DocumentId);
        Assert.Contains("Animais", results[0].Content);
        Assert.Equal(1, handler.ExpansionCalls);
        Assert.True(handler.RerankCalls >= 1);
    }

    [Fact]
    public async Task Truly_absent_subject_stays_empty_after_expansion_and_fallback()
    {
        var investigativeCondominium = Guid.NewGuid();
        AddInvestigative(investigativeCondominium, "Regimento",
            "Mudanças ocorrem em dias úteis.", new float[] { 0, 1, 0 });
        AddInvestigative(investigativeCondominium, "Convenção",
            "Assembleias exigem convocação prévia.", new float[] { 0, 0, 1 });
        AddInvestigative(investigativeCondominium, "Manual",
            "Animais devem ser conduzidos nas áreas comuns.", new float[] { 1, 0, 0 });
        await db.SaveChangesAsync();
        var handler = new InvestigativeChatHandler(
            ["horta comunitária", "cultivo de abelhas", "apicultura"]);
        var ai = new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" };

        var results = await Service(new InvestigativeEmbeddingService(),
            new HttpClient(handler) { BaseAddress = new Uri("https://test/") }, ai)
            .RetrieveAsync(investigativeCondominium,
                "Posso manter colmeias no terraço?", null, default);

        Assert.Empty(results);
        Assert.Equal(1, handler.ExpansionCalls);
    }

    [Fact]
    public async Task Highly_relevant_chunk_includes_adjacent_context()
    {
        var document = Document("Regra contínua", CondominiumDocumentType.InternalRules);
        db.Add(document);
        db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 0,
            "É proibido aos ocupantes", JsonSerializer.Serialize(new float[5]),
            70, "Sossego", "semantic-test-v1"));
        db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 1,
            "produzir barulho após as 22h.", JsonSerializer.Serialize(Vector(1)),
            70, "Sossego", "semantic-test-v1"));
        await db.SaveChangesAsync();

        var results = await Service().RetrieveAsync(condominiumId,
            "Posso fazer barulho à noite?", null, default);

        Assert.Contains(results, x => x.Content == "produzir barulho após as 22h.");
        Assert.Contains(results, x => x.Content == "É proibido aos ocupantes");
    }

    [Fact]
    public async Task Request_hint_enriches_an_ambiguous_question()
    {
        var results = await Service().RetrieveAsync(condominiumId, "O que posso fazer nesse caso?",
            "Reclamação de barulho recorrente durante a madrugada", default);
        Assert.Contains("sossego", results[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Relevant_minutes_after_the_old_five_hundred_candidate_limit_is_retrieved()
    {
        var otherCondominium = Guid.NewGuid();
        for (var documentNumber = 0; documentNumber < 5; documentNumber++)
        {
            var minutes = Document($"Ata histórica {documentNumber}", CondominiumDocumentType.Minutes);
            db.Add(minutes);
            for (var chunk = 0; chunk < 101; chunk++)
                db.Add(new CondominiumDocumentChunk(minutes.Id, condominiumId, chunk,
                    $"Assunto administrativo ordinário número {documentNumber}-{chunk}.",
                    JsonSerializer.Serialize(new float[] { 0, 1 }), chunk + 1, "Pauta", "election-test-v1"));
        }
        var relevant = Document("Ata AGO 15-03-2025", CondominiumDocumentType.Minutes);
        db.Add(relevant);
        db.Add(new CondominiumDocumentChunk(relevant.Id, condominiumId, 0,
            "Aos quinze dias do mês de março de 2025, foi eleito Lisandro Beltrã para exercer o cargo de síndico.",
            JsonSerializer.Serialize(new float[] { 1, 0 }), 7, "Eleição de síndico", "election-test-v1"));
        var inactive = Document("Ata inativa", CondominiumDocumentType.Minutes); inactive.SetActive(false); db.Add(inactive);
        db.Add(new CondominiumDocumentChunk(inactive.Id, condominiumId, 0, "Lisandro Beltrã eleito em data errada.", JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "election-test-v1"));
        var failed = new CondominiumDocument(condominiumId, "Ata falha", CondominiumDocumentType.Minutes, "failed.pdf", "failed", "application/pdf", 1, null, Guid.NewGuid()); failed.Fail("failed"); db.Add(failed);
        db.Add(new CondominiumDocumentChunk(failed.Id, condominiumId, 0, "Lisandro Beltrã eleito em data errada.", JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "election-test-v1"));
        var incompatible = Document("Ata modelo antigo", CondominiumDocumentType.Minutes); db.Add(incompatible);
        db.Add(new CondominiumDocumentChunk(incompatible.Id, condominiumId, 0, "Lisandro Beltrã eleito em data errada.", JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "old-model"));
        var foreign = new CondominiumDocument(otherCondominium, "Ata outro condomínio", CondominiumDocumentType.Minutes, "foreign.pdf", "foreign", "application/pdf", 1, null, Guid.NewGuid()); foreign.Ready(); db.Add(foreign);
        db.Add(new CondominiumDocumentChunk(foreign.Id, otherCondominium, 0, "Lisandro Beltrã eleito em data errada.", JsonSerializer.Serialize(new float[] { 1, 0 }), 1, null, "election-test-v1"));
        await db.SaveChangesAsync();
        var embedding = new ElectionEmbeddingService();
        var results = await Service(embedding).RetrieveAsync(condominiumId,
            "Qual a data da assembleia em que fui eleito síndico?", null, "Lisandro Beltrã", default);
        Assert.Equal(relevant.Id, results[0].DocumentId);
        Assert.Contains("março de 2025", results[0].Content);
        Assert.Contains("Usuário atual: Lisandro Beltrã", embedding.LastText);
        Assert.DoesNotContain(results, x => x.DocumentId == inactive.Id || x.DocumentId == failed.Id
            || x.DocumentId == incompatible.Id || x.DocumentId == foreign.Id);
    }

    [Fact]
    public async Task General_question_does_not_receive_the_current_user_identity()
    {
        var embedding = new ElectionEmbeddingService();
        await Service(embedding).RetrieveAsync(condominiumId, "Qual o horário da piscina?", null, "Lisandro Beltrã", default);
        Assert.DoesNotContain("Lisandro", embedding.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_questions_use_active_database_rows_without_embeddings()
    {
        var active = Document("Ata atual", CondominiumDocumentType.Minutes); db.Add(active);
        var inactive = Document("Ata antiga", CondominiumDocumentType.Minutes); inactive.SetActive(false); db.Add(inactive);
        await db.SaveChangesAsync();
        var conversation = new CondominiumAssistantConversation(condominiumId, Guid.NewGuid(), null, "Catálogo");
        var answer = await Service(new ThrowingEmbeddingService()).AskAsync(conversation,
            "Quais documentos você possui?", default);
        Assert.Contains("Ata atual", answer.Answer); Assert.DoesNotContain("Ata antiga", answer.Answer);
        Assert.Empty(answer.Sources); Assert.Equal("structured-catalog", answer.Model);
    }

    [Fact]
    public void Normalization_and_chunking_preserve_dates_times_and_assembly_numbers()
    {
        const string text = "Ata nº 42 realizada em 15/03/2026, às 19:30. Mandato iniciado em 15 de março de 2026.";
        var normalized = CondominiumDocumentText.Normalize(text);
        var chunk = Assert.Single(CondominiumDocumentText.Chunks(normalized));
        Assert.Contains("15/03/2026", chunk); Assert.Contains("19:30", chunk);
        Assert.Contains("15 de março de 2026", chunk); Assert.Contains("nº 42", chunk);
    }

    private CondominiumAssistantService Service(IEmbeddingService? embedding = null,
        HttpClient? client = null, RequestDraftAiOptions? ai = null) => new(db, embedding ?? new SemanticTestEmbeddingService(),
        client ?? new HttpClient(), Options.Create(ai ?? new RequestDraftAiOptions()),
        Options.Create(new CondominiumAssistantOptions { MinimumRelevanceScore = .2 }),
        NullLogger<CondominiumAssistantService>.Instance);

    private CondominiumDocument Document(string name, CondominiumDocumentType type)
    { var document = new CondominiumDocument(condominiumId, name, type, $"{name}.pdf", name, "application/pdf", 1, null, Guid.NewGuid()); document.Ready(); return document; }

    private sealed class ElectionEmbeddingService : IEmbeddingService
    {
        public string Model => "election-test-v1";
        public string LastText { get; private set; } = "";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        { LastText = text; return Task.FromResult(text.Contains("eleit", StringComparison.OrdinalIgnoreCase) ? new float[] { 1, 0 } : new float[] { 0, 1 }); }
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public string Model => "must-not-run";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Catalog must not use retrieval.");
    }

    private void Add(string content, int topic, int page)
    {
        var document = new CondominiumDocument(condominiumId, $"Documento {topic}", CondominiumDocumentType.InternalRules,
            $"doc-{topic}.pdf", $"key-{topic}", "application/pdf", 1, null, Guid.NewGuid()); document.Ready();
        db.Add(document); db.Add(new CondominiumDocumentChunk(document.Id, condominiumId, 0, content,
            JsonSerializer.Serialize(Vector(topic)), page, null, "semantic-test-v1"));
    }

    private CondominiumDocument AddInvestigative(Guid targetCondominium,
        string name, string content, float[] vector)
    {
        var document = new CondominiumDocument(targetCondominium, name,
            CondominiumDocumentType.InternalRules, $"{name}.pdf", name,
            "application/pdf", 1, null, Guid.NewGuid());
        document.Ready();
        db.Add(document);
        db.Add(new CondominiumDocumentChunk(document.Id, targetCondominium, 0,
            content, JsonSerializer.Serialize(vector), 1, null, "investigative-v1"));
        return document;
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

    private sealed class InvestigativeEmbeddingService : IEmbeddingService
    {
        public string Model => "investigative-v1";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            var value = text.ToLowerInvariant();
            return Task.FromResult(value.Contains("animais") || value.Contains("pets")
                ? new float[] { 1, 0, 0 } : new float[3]);
        }
    }

    private sealed class InvestigativeChatHandler(string[] queries) : HttpMessageHandler
    {
        public int ExpansionCalls { get; private set; }
        public int RerankCalls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            string content;
            if (body.Contains("busca documental", StringComparison.Ordinal))
            {
                ExpansionCalls++;
                content = JsonSerializer.Serialize(new { queries });
            }
            else
            {
                RerankCalls++;
                using var requestJson = JsonDocument.Parse(body);
                var userContent = requestJson.RootElement.GetProperty("messages")[1]
                    .GetProperty("content").GetString()!;
                var candidateJson = userContent[(userContent.IndexOf("Candidatos: ",
                    StringComparison.Ordinal) + "Candidatos: ".Length)..];
                using var candidateDocument = JsonDocument.Parse(candidateJson);
                content = JsonSerializer.Serialize(new
                {
                    items = candidateDocument.RootElement.EnumerateArray().Select(item => new
                    {
                        id = item.GetProperty("id").GetGuid(),
                        relevance = item.GetProperty("text").GetString()!
                            .Contains("Animais", StringComparison.OrdinalIgnoreCase) ? .95 : .1
                    }).ToArray()
                });
            }
            var envelope = JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content } } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json")
            };
        }
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
}
