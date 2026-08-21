using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class RequestDraftAiServiceTests
{
    [Fact]
    public async Task Resident_status_prompt_preserves_the_decision_and_forbids_unfounded_courtesy()
    {
        string? requestBody = null;
        var service = Service(async (request, ct) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(ct);
            return Response("""{"message":"*Seu atendimento foi finalizado.*\\n\\nO reparo foi concluído."}""");
        });

        var result = await service.SynthesizeResidentStatusAsync(
            "Vazamento", "Resolvida", "O reparo foi concluído", CancellationToken.None);

        Assert.True(result.Succeeded);
        using var payload = JsonDocument.Parse(requestBody!);
        var prompt = payload.RootElement.GetProperty("messages")[0]
            .GetProperty("content").GetString();
        Assert.Contains("Preserve integralmente o sentido", prompt);
        Assert.Contains("decisão administrativa", prompt);
        Assert.Contains("Retorne apenas uma sugestão", prompt);
        Assert.Contains("agradecimentos", prompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cortesia sem valor informativo", prompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Não invente", prompt);
    }

    [Fact]
    public async Task Resident_status_rejects_courtesy_absent_from_source()
    {
        var service = Service((_, _) => Task.FromResult(Response(
            """{"message":"O reparo foi concluído. Agradecemos pela compreensão."}""")));

        var result = await service.SynthesizeResidentStatusAsync(
            "Vazamento", "Resolvida", "O reparo foi concluído", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_response", result.Outcome);
    }

    [Theory]
    [InlineData(false, "test-key")]
    [InlineData(true, null)]
    public async Task Disabled_or_missing_api_key_returns_failure_without_calling_provider(
        bool enabled, string? apiKey)
    {
        var called = false;
        var service = Service((_, _) =>
        {
            called = true;
            return Task.FromResult(Response("{}"));
        }, enabled: enabled, apiKey: apiKey);

        var result = await service.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Proposal);
        Assert.False(called);
        Assert.Equal(enabled
            ? RequestDraftAiOutcome.NotConfigured
            : RequestDraftAiOutcome.Disabled, result.Outcome);
    }

    [Fact]
    public async Task Sends_strict_json_schema_and_accepts_nullable_fields()
    {
        string? requestBody = null;
        var service = Service(async (request, ct) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(ct);
            return Response("""
                {"title":"Portão danificado","description":"O portão não fecha.","suggestedCategory":null,"missingInformation":[],"confidence":null}
                """);
        });

        var result = await service.ProposeAsync(
            "O portão não fecha.", ["Manutenção"], "Residencial Teste", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(RequestDraftAiOutcome.Succeeded, result.Outcome);
        Assert.Equal("gpt-4.1-mini", result.Model);
        Assert.Null(result.Proposal!.SuggestedCategory);
        Assert.Empty(result.Proposal.MissingInformation);
        Assert.Null(result.Proposal.Confidence);
        using var payload = JsonDocument.Parse(requestBody!);
        var format = payload.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var jsonSchema = format.GetProperty("json_schema");
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        var schema = jsonSchema.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(5, schema.GetProperty("required").GetArrayLength());
        Assert.Equal("array", schema.GetProperty("properties")
            .GetProperty("missingInformation").GetProperty("type").GetString());
        Assert.Contains("Nome do condomínio: Residencial Teste", payload.RootElement
            .GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Valid_structured_response_is_deserialized_and_normalized()
    {
        var service = Service((_, _) => Task.FromResult(Response("""
            {"title":"  Vazamento  ","description":"  Há água no corredor.  ","suggestedCategory":"Hidráulica","missingInformation":["  Informe o andar.  "],"confidence":0.8}
            """)));

        var result = await service.ProposeAsync(
            "Há água no corredor.", ["Hidráulica"], "Condomínio Teste", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Vazamento", result.Proposal!.Title);
        Assert.Equal("Há água no corredor.", result.Proposal.Description);
        Assert.Equal("Hidráulica", result.Proposal.SuggestedCategory);
        Assert.Equal(["Informe o andar."], result.Proposal.MissingInformation);
        Assert.Equal(0.8, result.Proposal.Confidence);

        var analysis = result.Proposal.ToAnalysis();
        Assert.Equal("Vazamento", analysis.Title);
        Assert.Equal("Há água no corredor.", analysis.Description);
        Assert.Equal("Hidráulica", analysis.SuggestedCategory);
        Assert.Equal(0.8, analysis.Confidence);
        Assert.Equal(["Informe o andar."], analysis.MissingInformation);
    }

    [Fact]
    public async Task Model_refusal_returns_failure_for_existing_fallback()
    {
        var service = Service((_, _) => Task.FromResult(JsonResponse("""
            {"choices":[{"message":{"refusal":"Não posso responder.","content":null}}]}
            """)));

        var result = await service.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Proposal);
        Assert.Equal(RequestDraftAiOutcome.Refusal, result.Outcome);
    }

    [Theory]
    [InlineData("{\"title\":\"T\",\"description\":\"D\",\"suggestedCategory\":null,\"missingInformation\":[],\"confidence\":null,\"extra\":true}")]
    [InlineData("{\"title\":\"\",\"description\":\"D\",\"suggestedCategory\":null,\"missingInformation\":[],\"confidence\":null}")]
    [InlineData("not-json")]
    public async Task Response_outside_schema_returns_safe_diagnostic_code(string content)
    {
        var service = Service((_, _) => Task.FromResult(Response(content)));

        var result = await service.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Proposal);
        Assert.Equal(content == "not-json"
            ? RequestDraftAiOutcome.InvalidJson
            : RequestDraftAiOutcome.SchemaValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task Missing_content_returns_failure_for_existing_fallback()
    {
        var service = Service((_, _) => Task.FromResult(JsonResponse("""
            {"choices":[{"message":{}}]}
            """)));

        var result = await service.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RequestDraftAiOutcome.EmptyResponse, result.Outcome);
    }

    [Fact]
    public async Task Http_error_and_timeout_return_failure()
    {
        var httpError = Service((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var timeout = Service(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeoutSeconds: 1);

        var httpResult = await httpError.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);
        var timeoutResult = await timeout.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.Equal(RequestDraftAiOutcome.HttpBadRequest, httpResult.Outcome);
        Assert.Equal(RequestDraftAiOutcome.Timeout, timeoutResult.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, RequestDraftAiOutcome.HttpUnauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, RequestDraftAiOutcome.HttpRateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, RequestDraftAiOutcome.ProviderError)]
    public async Task Http_failures_return_specific_safe_codes(
        HttpStatusCode status, RequestDraftAiOutcome expected)
    {
        var service = Service((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        var result = await service.ProposeAsync("Relato", [], "Condomínio Teste", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal("AI proposal unavailable.", result.Error);
    }

    [Fact]
    public async Task Operational_logs_include_safe_metadata_and_exclude_sensitive_content()
    {
        var logger = new RecordingLogger<RequestDraftAiService>();
        const string sensitiveMessage = "request contained morador-secret";
        var service = Service((_, _) => Task.FromResult(JsonResponse("""
            {"error":{"message":"request contained morador-secret","type":"invalid_request_error","code":"bad_parameter","param":"response_format"}}
            """, HttpStatusCode.BadRequest)), logger: logger,
            baseUrl: "https://api.openai.com/v1/?tenant=secret-query");

        await service.ProposeAsync("relato-morador-secret", ["categoria-secret"], "condominio-secret",
            CancellationToken.None);

        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("invalid_request_error", logs);
        Assert.Contains("bad_parameter", logs);
        Assert.Contains("response_format", logs);
        Assert.Contains("https://api.openai.com/v1/", logs);
        Assert.DoesNotContain("secret-query", logs);
        Assert.DoesNotContain(sensitiveMessage, logs);
        Assert.DoesNotContain("relato-morador-secret", logs);
        Assert.DoesNotContain("categoria-secret", logs);
        Assert.DoesNotContain("condominio-secret", logs);
        Assert.DoesNotContain("test-key", logs);
    }

    private static RequestDraftAiService Service(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        int timeoutSeconds = 15, bool enabled = true, string? apiKey = "test-key",
        ILogger<RequestDraftAiService>? logger = null,
        string baseUrl = "https://api.openai.com/v1/")
    {
        var client = new HttpClient(new DelegateHandler(send))
        {
            BaseAddress = new Uri(baseUrl)
        };
        return new RequestDraftAiService(client, Options.Create(new RequestDraftAiOptions
        {
            Enabled = enabled,
            ApiKey = apiKey,
            TimeoutSeconds = timeoutSeconds
        }), logger ?? NullLogger<RequestDraftAiService>.Instance);
    }

    private static HttpResponseMessage Response(string content) => JsonResponse(
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        }));

    private static HttpResponseMessage JsonResponse(
        string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
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
