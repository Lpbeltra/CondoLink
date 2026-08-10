using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class AdministrativeResidentMutationExtractionServiceTests
{
    [Theory]
    [InlineData("resident_membership_deactivate")]
    [InlineData("resident_membership_move")]
    public async Task Accepts_structured_membership_mutation(string intent)
    {
        var content = JsonSerializer.Serialize(new
        {
            intent, residentName = "Ciclano", condominium = (string?)null,
            sourceBlock = "1", sourceUnit = "105",
            destinationBlock = intent.EndsWith("move") ? "2" : null,
            destinationUnit = intent.EndsWith("move") ? "405" : null,
            relationship = intent.EndsWith("move") ? "Tenant" : null
        });
        var service = Create(Envelope(content));

        var result = await service.ExtractAsync(
            "Mude o Ciclano do 105/1 para o 405/2", null, default);

        Assert.True(result.Succeeded);
        Assert.Equal(intent, result.Data!.Intent);
        Assert.Equal("105", result.Data.SourceUnit);
        if (intent.EndsWith("move")) Assert.Equal("405", result.Data.DestinationUnit);
    }

    [Fact]
    public async Task Prompt_limits_ai_to_extraction_without_mutation_authority()
    {
        string? body = null;
        var content = """
            {"intent":"resident_membership_deactivate","residentName":"João","condominium":null,"sourceBlock":"1","sourceUnit":"105","destinationBlock":null,"destinationUnit":null,"relationship":null}
            """;
        var service = Create(Envelope(content), value => body = value);

        await service.ExtractAsync("Retire João do 105/1", null, default);

        using var json = JsonDocument.Parse(body!);
        var prompt = json.RootElement.GetProperty("messages")[0]
            .GetProperty("content").GetString();
        Assert.Contains("nunca autoriza", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("executa mutação", prompt, StringComparison.OrdinalIgnoreCase);
    }

    private static AdministrativeResidentMutationExtractionService Create(
        string response, Action<string>? capture = null)
    {
        var client = new HttpClient(new Handler(response, capture))
        { BaseAddress = new Uri("https://api.openai.test/") };
        return new(client, Options.Create(new RequestDraftAiOptions
        { Enabled = true, ApiKey = "test", Model = "test" }),
            NullLogger<AdministrativeResidentMutationExtractionService>.Instance);
    }
    private static string Envelope(string content) => JsonSerializer.Serialize(new
    { choices = new[] { new { message = new { content } } } });
    private sealed class Handler(string response, Action<string>? capture)
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
