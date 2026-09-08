using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public interface IProviderContactAiService
{
    Task<ProviderContactAiResult> PrepareAsync(string context, CancellationToken cancellationToken);
}

public sealed record ProviderContactAiResult(bool Succeeded, string? Message, string? Error);

public sealed class ProviderContactAiService(HttpClient httpClient, IOptions<RequestDraftAiOptions> options, ILogger<ProviderContactAiService> logger) : IProviderContactAiService
{
    public async Task<ProviderContactAiResult> PrepareAsync(string context, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey)) return new(false, null, "AI indisponível.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 60)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.Model, temperature = 0,
            response_format = new { type = "json_schema", json_schema = new { name = "provider_contact_message", strict = true, schema = new { type = "object", additionalProperties = false, properties = new { message = new { type = "string", minLength = 1, maxLength = 1200 } }, required = new[] { "message" } } } },
            messages = new object[] { new { role = "system", content = "Escreva uma mensagem curta para WhatsApp em português brasileiro, cordial e profissional, destinada a um prestador. Use somente os dados fornecidos. Não invente diagnóstico, disponibilidade, valores, autorização ou pagamento. Não inclua notas internas nem dados pessoais desnecessários. Retorne somente JSON com message." }, new { role = "user", content = context } }
        });
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return new(false, null, "Não foi possível preparar a mensagem.");
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var payload = JsonSerializer.Deserialize<Payload>(content ?? "", JsonOptions);
            var message = payload?.Message?.Trim();
            return string.IsNullOrWhiteSpace(message) ? new(false, null, "A IA não retornou uma mensagem.") : new(true, message, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, null, "A preparação demorou mais que o esperado."); }
        catch (Exception exception) when (exception is not OperationCanceledException) { logger.LogWarning(exception, "Provider contact AI failed."); return new(false, null, "Não foi possível preparar a mensagem."); }
    }
    private sealed record Payload(string Message);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}
