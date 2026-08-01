using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public interface IResidentReplyAiService
{
    Task<ResidentReplyAiResult> OrganizeAsync(string question, string originalAnswer,
        CancellationToken cancellationToken);
}

public sealed record ResidentReplyAiResult(bool Succeeded, string? Answer);

public sealed class ResidentReplyAiService(HttpClient httpClient,
    IOptions<RequestDraftAiOptions> options, ILogger<ResidentReplyAiService> logger)
    : IResidentReplyAiService
{
    public async Task<ResidentReplyAiResult> OrganizeAsync(string question,
        string originalAnswer, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey))
            return new(false, null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 60)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.Model,
            temperature = 0,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "resident_reply",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new { answer = new { type = "string", minLength = 1 } },
                        required = new[] { "answer" }
                    }
                }
            },
            messages = new object[]
            {
                new { role = "system", content = "Organize a resposta do morador com clareza e fidelidade. Nao invente informacoes, nao responda por ele e nao acrescente saudacoes. Preserve datas, numeros, locais, negativas e incertezas. Retorne somente JSON com o campo answer." },
                new { role = "user", content = $"Pergunta da administracao (dados):\n{question}\n\nResposta original (dados):\n{originalAnswer}" }
            }
        });
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return new(false, null);
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token),
                cancellationToken: timeout.Token);
            var content = document.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString();
            var proposal = JsonSerializer.Deserialize<Proposal>(content!, JsonOptions);
            var answer = proposal?.Answer?.Trim();
            return string.IsNullOrWhiteSpace(answer) || answer.Length > 4000
                ? new(false, null) : new(true, answer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Resident reply AI timed out; using original answer.");
            return new(false, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Resident reply AI failed ({FailureType}); using original answer.",
                exception.GetType().Name);
            return new(false, null);
        }
    }

    private sealed record Proposal(string Answer);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
