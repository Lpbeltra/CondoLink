using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.TelegramAssistant;

public interface ITelegramBotClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken ct);
    Task SendTypingAsync(long chatId, CancellationToken ct);
}

public sealed class TelegramBotClient(HttpClient http, IOptions<TelegramAssistantOptions> options)
    : ITelegramBotClient
{
    public Task SendMessageAsync(long chatId, string text, CancellationToken ct) =>
        PostAsync("sendMessage", new { chat_id = chatId, text }, ct);
    public Task SendTypingAsync(long chatId, CancellationToken ct) =>
        PostAsync("sendChatAction", new { chat_id = chatId, action = "typing" }, ct);

    private async Task PostAsync(string method, object body, CancellationToken ct)
    {
        var token = options.Value.BotToken?.Trim();
        if (!options.Value.IsConfigured || string.IsNullOrWhiteSpace(token)) return;
        HttpResponseMessage response;
        try
        {
            // The leading slash is required: a Telegram token contains ':', so without it
            // System.Uri interprets "bot<token>:..." as a non-HTTP URI scheme.
            response = await http.PostAsJsonAsync($"/bot{token}/{method}", body, ct);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw TelegramBotApiException.Transport(method, "timeout", exception);
        }
        catch (HttpRequestException exception)
        {
            throw TelegramBotApiException.Transport(method, "network", exception);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            throw TelegramBotApiException.Transport(method, "transport_configuration", exception);
        }

        using (response)
        {
            TelegramBotApiResponse? result = null;
            try
            {
                result = await response.Content.ReadFromJsonAsync<TelegramBotApiResponse>(cancellationToken: ct);
            }
            catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
            {
                throw TelegramBotApiException.Transport(method, "timeout", exception);
            }
            catch (HttpRequestException exception)
            {
                throw TelegramBotApiException.Transport(method, "network", exception);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                if (response.IsSuccessStatusCode)
                    throw TelegramBotApiException.InvalidResponse(method, response.StatusCode, exception);
            }

            if (response.IsSuccessStatusCode && result?.Ok == true) return;

            var category = response.IsSuccessStatusCode
                ? result?.Ok == false ? "api_rejected" : "invalid_response"
                : "http_error";
            throw TelegramBotApiException.ApiResponse(method, response.StatusCode,
                result?.ErrorCode, result?.Description, category);
        }
    }

    private sealed record TelegramBotApiResponse(bool? Ok,
        [property: JsonPropertyName("error_code")] int? ErrorCode, string? Description);
}

public sealed class TelegramBotApiException : Exception
{
    private TelegramBotApiException(string operation, string category, string exceptionType,
        System.Net.HttpStatusCode? httpStatus, int? telegramErrorCode, string? telegramDescription,
        Exception? inner = null)
        : base($"Telegram Bot API {operation} failed ({category}).", inner)
    {
        Operation = operation;
        Category = category;
        ExceptionType = exceptionType;
        HttpStatus = httpStatus;
        TelegramErrorCode = telegramErrorCode;
        TelegramDescription = Sanitize(telegramDescription);
    }

    public string Operation { get; }
    public string Category { get; }
    public string ExceptionType { get; }
    public System.Net.HttpStatusCode? HttpStatus { get; }
    public int? TelegramErrorCode { get; }
    public string? TelegramDescription { get; }

    internal static TelegramBotApiException Transport(string operation, string category, Exception exception) =>
        new(operation, category, exception.GetType().Name, null, null, null, exception);

    internal static TelegramBotApiException InvalidResponse(string operation,
        System.Net.HttpStatusCode status, Exception exception) =>
        new(operation, "invalid_response", exception.GetType().Name, status, null, null, exception);

    internal static TelegramBotApiException ApiResponse(string operation,
        System.Net.HttpStatusCode status, int? errorCode, string? description, string category) =>
        new(operation, category, "TelegramBotApiResponse", status, errorCode, description);

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 240 ? sanitized : sanitized[..240];
    }
}
