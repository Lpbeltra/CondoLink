using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.Agenda;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CondoLink.Infrastructure.Identity;

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
        var manager = new ApplicationUser("Streaming Manager", $"streaming-{Guid.NewGuid():N}@test.local", null);
        manager.NormalizedUserName = manager.UserName!.ToUpperInvariant();
        manager.NormalizedEmail = manager.Email!.ToUpperInvariant();
        var condo = new Condominium("Streaming Condo", null, null);
        condominiumId = condo.Id;
        var company = new ManagementCompany("Administradora SSE", null, null, null, null);
        condo.SetManagementCompany(company.Id);
        var membership = new CondominiumMembership(manager.Id, condominiumId);
        db.AddRange(manager, condo, company, membership,
            new CondominiumMembershipRole(membership.Id, CondominiumRole.Manager));
        managerId = manager.Id;
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
        var active = new CondominiumDocument(condominiumId, "s3-68f8df5e47c9a", CondominiumDocumentType.Minutes,
            "Ata atual.pdf", "key", "application/pdf", 1, null, Guid.NewGuid());
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
        Assert.Contains("Ata atual.pdf", answer.Answer);
        Assert.DoesNotContain("s3-68f8df5e47c9a", answer.Answer);
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
        Assert.Equal("O sossego deve ser respeitado [S1].", string.Concat(tokens));
        Assert.Equal("O sossego deve ser respeitado [S1].", answer.Answer);
        Assert.Equal("test-chat", answer.Model);
    }

    [Fact]
    public async Task Streaming_function_call_accumulates_fragmented_arguments_and_hides_internal_frames()
    {
        var handler = new ToolStreamingChatHandler();
        var tools = new AssistantOperationalTools(db, NullLogger<AssistantOperationalTools>.Instance,
            Options.Create(new AgendaOptions { OperationalTimeZone = "America/Sao_Paulo" }));
        var ai = new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" };
        var conversation = new CondominiumAssistantConversation(condominiumId, managerId, null, "Tool SSE");
        var tokens = new List<string>();

        var answer = await Service(new SemanticTestEmbeddingService(), new HttpClient(handler)
            { BaseAddress = new Uri("https://test/") }, ai, tools)
            .AskStreamAsync(conversation, "Qual administradora atende este condomínio?",
                (_, _) => Task.CompletedTask,
                (delta, _) => { tokens.Add(delta); return Task.CompletedTask; }, default);

        Assert.Equal("A administradora é Administradora SSE.", string.Concat(tokens));
        Assert.Equal(answer.Answer, string.Concat(tokens));
        Assert.DoesNotContain("get_management_company", answer.Answer);
        Assert.DoesNotContain("arguments", answer.Answer);
        Assert.Contains(answer.OperationalReferences!, x => x.Type == "management_company");
        Assert.Equal(2, handler.StreamCalls);
        Assert.True(handler.SawTools);
    }

    [Fact]
    public async Task Streaming_function_call_loop_stops_at_six_tool_turns()
    {
        var handler = new ToolStreamingChatHandler { AlwaysRequestTool = true };
        var tools = new AssistantOperationalTools(db, NullLogger<AssistantOperationalTools>.Instance,
            Options.Create(new AgendaOptions { OperationalTimeZone = "America/Sao_Paulo" }));
        var ai = new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" };
        var conversation = new CondominiumAssistantConversation(condominiumId, managerId, null, "Tool limit");

        var answer = await Service(new SemanticTestEmbeddingService(), new HttpClient(handler)
            { BaseAddress = new Uri("https://test/") }, ai, tools)
            .AskStreamAsync(conversation, "Consulte a administradora repetidamente.",
                (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, default);

        Assert.Contains("limite", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, handler.StreamCalls);
    }

    [Fact]
    public async Task Streaming_orchestration_preserves_unit_context_when_model_selects_attendance_tool()
    {
        var resident = new ApplicationUser("Morador 1201", $"resident-{Guid.NewGuid():N}@test.local", "11999999999");
        resident.NormalizedUserName = resident.UserName!.ToUpperInvariant();
        resident.NormalizedEmail = resident.Email!.ToUpperInvariant();
        var unit = new Unit(condominiumId, "1201", null, null, null);
        var category = new Category(condominiumId, "Manutenção", null);
        var request = new Request(condominiumId, resident.Id, unit.Id, category.Id, "Vazamento", "Vazamento aberto");
        db.AddRange(resident, unit, category, request,
            new UnitMembership(resident.Id, unit.Id, UnitRelationshipType.Owner, true, true));
        await db.SaveChangesAsync();

        var handler = new ContextualAttendanceStreamingHandler();
        var tools = new AssistantOperationalTools(db, NullLogger<AssistantOperationalTools>.Instance,
            Options.Create(new AgendaOptions { OperationalTimeZone = "America/Sao_Paulo" }));
        var service = Service(new SemanticTestEmbeddingService(), new HttpClient(handler)
            { BaseAddress = new Uri("https://test/") }, new RequestDraftAiOptions { Enabled = true, ApiKey = "test", Model = "test-chat" }, tools);
        var conversation = new CondominiumAssistantConversation(condominiumId, managerId, null, "Contexto unitário");

        await AskAndPersistAsync(service, conversation, "Quais são os moradores do 1201?");
        await AskAndPersistAsync(service, conversation, "Qual o telefone deles?");
        var answer = await AskAndPersistAsync(service, conversation, "Essa unidade tem algum atendimento aberto?");

        Assert.Equal("Há um atendimento aberto para a unidade 1201.", answer.Answer);
        Assert.Contains(answer.OperationalReferences!, x => x.Type == "request" && x.Id == request.Id);
        Assert.True(handler.SawVerifiedUnitContext);
        Assert.Equal("1201", handler.AttendanceUnit);
        Assert.DoesNotContain("documentos disponíveis", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AssistantAnswer> AskAndPersistAsync(CondominiumAssistantService service,
        CondominiumAssistantConversation conversation, string question)
    {
        db.Add(new CondominiumAssistantMessage(conversation.Id, CondominiumAssistantRole.User, question));
        await db.SaveChangesAsync();
        var answer = await service.AskStreamAsync(conversation, question,
            (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, default);
        db.Add(new CondominiumAssistantMessage(conversation.Id, CondominiumAssistantRole.Assistant, answer.Answer,
            JsonSerializer.Serialize(new { sources = answer.Sources, operationalReferences = answer.OperationalReferences ?? [] })));
        await db.SaveChangesAsync();
        return answer;
    }

    private CondominiumAssistantService Service(IEmbeddingService? embedding = null,
        HttpClient? client = null, RequestDraftAiOptions? ai = null,
        AssistantOperationalTools? tools = null) => new(db,
        embedding ?? new SemanticTestEmbeddingService(), client ?? new HttpClient(),
        Options.Create(ai ?? new RequestDraftAiOptions()),
        Options.Create(new CondominiumAssistantOptions { MinimumRelevanceScore = .2 }),
        NullLogger<CondominiumAssistantService>.Instance, operationalTools: tools);

    private Guid managerId;

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
                data: {"choices":[{"delta":{"content":"O sossego deve "}}]}

                data: {"choices":[{"delta":{}}]}

                data: {"choices":[{"delta":{"content":"ser respeitado [S1]."}}]}

                data: [DONE]

                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            };
        }
    }

    private sealed class ToolStreamingChatHandler : HttpMessageHandler
    {
        public int StreamCalls { get; private set; }
        public bool SawTools { get; private set; }
        public bool AlwaysRequestTool { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("\"stream\":true", StringComparison.Ordinal))
                return new(HttpStatusCode.InternalServerError);
            SawTools |= body.Contains("get_management_company", StringComparison.Ordinal);
            StreamCalls++;
            var sse = AlwaysRequestTool || StreamCalls == 1
                ? "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-1\",\"function\":{\"name\":\"get_management_company\",\"arguments\":\"{\"}}]}}]}\n\n" +
                  "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"}\"}}]}}]}\n\n" +
                  "data: [DONE]\n\n"
                : "data: {\"choices\":[{\"delta\":{\"content\":\"A administradora é Administradora SSE.\"}}]}\n\ndata: [DONE]\n\n";
            return new(HttpStatusCode.OK) { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") };
        }
    }

    // Deterministic provider double: proves prompt/context, SSE tool JSON, tool execution and
    // grounding orchestration after a tool choice. It does not predict a production model choice.
    private sealed class ContextualAttendanceStreamingHandler : HttpMessageHandler
    {
        public bool SawVerifiedUnitContext { get; private set; }
        public string? AttendanceUnit { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var hasToolResult = body.Contains("\"tool_call_id\"", StringComparison.Ordinal);
            var question = body.Contains("Essa unidade tem algum atendimento aberto?", StringComparison.Ordinal)
                ? 3 : body.Contains("Qual o telefone deles?", StringComparison.Ordinal) ? 2 : 1;
            if (question == 3)
                SawVerifiedUnitContext |= body.Contains("CONTEXTO OPERACIONAL VERIFICADO NA CONVERSA", StringComparison.Ordinal)
                    && body.Contains("unidades 1201", StringComparison.Ordinal);
            if (hasToolResult)
            {
                var content = question == 3 ? "Há um atendimento aberto para a unidade 1201."
                    : question == 2 ? "O telefone é 11999999999." : "O morador é Morador 1201.";
                return Sse(content);
            }

            var name = question == 3 ? "search_requests" : question == 2 ? "search_residents" : "get_unit_residents";
            const string arguments = "{\"unit\":\"1201\"}";
            if (question == 3) AttendanceUnit = "1201";
            return ToolSse(name, arguments);
        }

        private static HttpResponseMessage Sse(string content) => Stream(new
        {
            choices = new[] { new { delta = new { content } } }
        });

        private static HttpResponseMessage ToolSse(string name, string arguments) => Stream(new
        {
            choices = new[] { new { delta = new { tool_calls = new[] { new { index = 0, id = "call-1", function = new { name, arguments } } } } } }
        });

        private static HttpResponseMessage Stream(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent($"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
        };
    }
}
