using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class RequestDraftAiServiceTests
{
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
            "O portão não fecha.", ["Manutenção"], CancellationToken.None);

        Assert.True(result.Succeeded);
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
    }

    [Fact]
    public async Task Valid_structured_response_is_deserialized_and_normalized()
    {
        var service = Service((_, _) => Task.FromResult(Response("""
            {"title":"  Vazamento  ","description":"  Há água no corredor.  ","suggestedCategory":"Hidráulica","missingInformation":["  Informe o andar.  "],"confidence":0.8}
            """)));

        var result = await service.ProposeAsync(
            "Há água no corredor.", ["Hidráulica"], CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Vazamento", result.Proposal!.Title);
        Assert.Equal("Há água no corredor.", result.Proposal.Description);
        Assert.Equal("Hidráulica", result.Proposal.SuggestedCategory);
        Assert.Equal(["Informe o andar."], result.Proposal.MissingInformation);
        Assert.Equal(0.8, result.Proposal.Confidence);
    }

    [Fact]
    public async Task Model_refusal_returns_failure_for_existing_fallback()
    {
        var service = Service((_, _) => Task.FromResult(JsonResponse("""
            {"choices":[{"message":{"refusal":"Não posso responder.","content":null}}]}
            """)));

        var result = await service.ProposeAsync("Relato", [], CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Proposal);
    }

    [Theory]
    [InlineData("{\"title\":\"T\",\"description\":\"D\",\"suggestedCategory\":null,\"missingInformation\":[],\"confidence\":null,\"extra\":true}")]
    [InlineData("{\"title\":\"\",\"description\":\"D\",\"suggestedCategory\":null,\"missingInformation\":[],\"confidence\":null}")]
    [InlineData("not-json")]
    public async Task Response_outside_schema_returns_failure_for_existing_fallback(string content)
    {
        var service = Service((_, _) => Task.FromResult(Response(content)));

        var result = await service.ProposeAsync("Relato", [], CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Proposal);
    }

    [Fact]
    public async Task Missing_content_returns_failure_for_existing_fallback()
    {
        var service = Service((_, _) => Task.FromResult(JsonResponse("""
            {"choices":[{"message":{}}]}
            """)));

        var result = await service.ProposeAsync("Relato", [], CancellationToken.None);

        Assert.False(result.Succeeded);
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

        Assert.False((await httpError.ProposeAsync("Relato", [], CancellationToken.None)).Succeeded);
        Assert.False((await timeout.ProposeAsync("Relato", [], CancellationToken.None)).Succeeded);
    }

    private static RequestDraftAiService Service(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        int timeoutSeconds = 15)
    {
        var client = new HttpClient(new DelegateHandler(send))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        return new RequestDraftAiService(client, Options.Create(new RequestDraftAiOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            TimeoutSeconds = timeoutSeconds
        }), NullLogger<RequestDraftAiService>.Instance);
    }

    private static HttpResponseMessage Response(string content) => JsonResponse(
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        }));

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
}
