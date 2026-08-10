using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class AdministrativeResidentLookupExtractionServiceTests
{
    [Theory]
    [InlineData("resident_lookup")]
    [InlineData("unit_residents_lookup")]
    public async Task Structured_lookup_intents_are_accepted(string intent)
    {
        var payload = JsonSerializer.Serialize(new
        {
            intent,
            residentName = intent == "resident_lookup" ? "João da Silva" : null,
            condominium = (string?)null,
            block = "B",
            unit = "302",
            requestedFields = new[] { "phone", "email" }
        });
        var service = Create(Envelope(payload));

        var result = await service.ExtractAsync(
            "Preciso dos dados do João do bloco B apto 302", null, default);

        Assert.True(result.Succeeded);
        Assert.Equal(intent, result.Data!.Intent);
        Assert.Equal("B", result.Data.Block);
        Assert.Equal("302", result.Data.Unit);
        Assert.Equal(["phone", "email"], result.Data.RequestedFields);
    }

    [Fact]
    public async Task Invalid_fields_are_rejected_without_query_execution()
    {
        var payload = """
            {"intent":"resident_lookup","residentName":"João","condominium":null,"block":null,"unit":null,"requestedFields":["password"]}
            """;
        var service = Create(Envelope(payload));

        var result = await service.ExtractAsync("Dados do João", null, default);

        Assert.False(result.Succeeded);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Prompt_forbids_sql_authorization_and_entity_resolution()
    {
        string? body = null;
        var payload = """
            {"intent":"unit_residents_lookup","residentName":null,"condominium":null,"block":null,"unit":"502","requestedFields":["phone"]}
            """;
        var service = Create(Envelope(payload), value => body = value);

        await service.ExtractAsync("Moradores do 502", null, default);

        using var json = JsonDocument.Parse(body!);
        var prompt = json.RootElement.GetProperty("messages")[0]
            .GetProperty("content").GetString();
        Assert.Contains("Nunca gere SQL", prompt);
        Assert.Contains("valide autorização", prompt);
        Assert.Contains("escolha usuários", prompt);
    }

    private static AdministrativeResidentLookupExtractionService Create(
        string response, Action<string>? capture = null)
    {
        var client = new HttpClient(new Handler(response, capture))
        { BaseAddress = new Uri("https://api.openai.test/") };
        return new(client, Options.Create(new RequestDraftAiOptions
        { Enabled = true, ApiKey = "test", Model = "test" }),
            NullLogger<AdministrativeResidentLookupExtractionService>.Instance);
    }

    private static string Envelope(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { content } } }
    });

    private sealed class Handler(string response, Action<string>? capture = null)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (capture is not null)
                capture(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
