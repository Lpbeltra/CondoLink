using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class AdministrativeResidentExtractionServiceTests
{
    [Fact]
    public async Task Valid_structured_response_is_deserialized_without_free_text_parsing()
    {
        var payload = """
            {"intent":"register_resident","fullName":"João da Silva","phone":"47999998888","email":"joao@example.com","condominium":"Monticello","block":"B","unit":"302","relationship":"Owner","isResident":true,"isPrimaryResidence":true}
            """;
        var service = Create(Envelope(payload));

        var result = await service.ExtractAsync("Cadastre João", null, default);

        Assert.True(result.Succeeded);
        Assert.Equal("302", result.Data!.Unit);
        Assert.Equal("Owner", result.Data.Relationship);
    }

    [Fact]
    public async Task Invalid_ai_payload_is_rejected_as_untrusted_data()
    {
        var service = Create(Envelope("{\"intent\":\"register_resident\",\"unexpected\":true}"));

        var result = await service.ExtractAsync("Cadastrar morador", null, default);

        Assert.False(result.Succeeded);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Prompt_extracts_only_delta_and_leaves_authorization_to_backend()
    {
        string? requestBody = null;
        var payload = """
            {"intent":"register_resident","fullName":null,"phone":"44988887777","email":null,"condominium":null,"block":null,"unit":null,"relationship":"Tenant","isResident":null,"isPrimaryResidence":null}
            """;
        var service = Create(Envelope(payload), body => requestBody = body);
        var current = new AdministrativeResidentExtraction("register_resident",
            "Zemilto Custódio", "44999999999", "zemilto@example.com",
            null, "B", "301", "Owner", true, false);

        var result = await service.ExtractAsync(
            "Telefone: 44988887777; relação: inquilino", current, default);

        Assert.True(result.Succeeded);
        Assert.Null(result.Data!.FullName);
        Assert.Equal("44988887777", result.Data.Phone);
        using var requestJson = JsonDocument.Parse(requestBody!);
        var prompt = requestJson.RootElement.GetProperty("messages")[0]
            .GetProperty("content").GetString();
        Assert.Contains("Retorne null para todo campo não mencionado", prompt);
        Assert.Contains("nem valide autorização", prompt);
    }

    private static AdministrativeResidentExtractionService Create(string response,
        Action<string>? capture = null)
    {
        var client = new HttpClient(new Handler(response, capture))
        { BaseAddress = new Uri("https://api.openai.test/") };
        return new(client, Options.Create(new RequestDraftAiOptions
        { Enabled = true, ApiKey = "test", Model = "test" }),
            NullLogger<AdministrativeResidentExtractionService>.Instance);
    }

    private static string Envelope(string content) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        });

    private sealed class Handler(string response, Action<string>? capture = null) : HttpMessageHandler
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
