using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CondoLink.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.TelegramAssistant;

public interface ITelegramBotClient
{
    Task SendMessageAsync(long chatId, string text, CancellationToken ct);
    Task SendMessageAsync(long chatId, string text, TelegramReplyMarkup replyMarkup, CancellationToken ct) =>
        SendMessageAsync(chatId, text, ct);
    Task SendTypingAsync(long chatId, CancellationToken ct);
    Task<byte[]> DownloadFileAsync(string fileId, long maximumBytes, CancellationToken ct) =>
        throw new NotSupportedException("Telegram file download is not available.");
}

public sealed class TelegramBotClient(HttpClient http, IOptions<TelegramAssistantOptions> options)
    : ITelegramBotClient
{
    public Task SendMessageAsync(long chatId, string text, CancellationToken ct) =>
        SendMessageAsync(chatId, text, TelegramReplyMarkup.None, ct);
    public async Task SendMessageAsync(long chatId, string text, TelegramReplyMarkup replyMarkup,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object> { ["chat_id"] = chatId, ["text"] = text };
        if (replyMarkup == TelegramReplyMarkup.RequestContact)
            body["reply_markup"] = new { keyboard = new[] { new[] { new
            { text = "Confirmar meu telefone", request_contact = true } } }, resize_keyboard = true, one_time_keyboard = true };
        else if (replyMarkup == TelegramReplyMarkup.RemoveKeyboard)
            body["reply_markup"] = new { remove_keyboard = true };
        await PostAsync("sendMessage", body, ct);
    }
    public async Task SendTypingAsync(long chatId, CancellationToken ct) =>
        _ = await PostAsync("sendChatAction", new { chat_id = chatId, action = "typing" }, ct);

    public async Task<byte[]> DownloadFileAsync(string fileId, long maximumBytes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Length > 256 || maximumBytes <= 0)
            throw new TelegramFileException("invalid_file_metadata");
        var file = await PostAsync("getFile", new { file_id = fileId }, ct);
        if (!file.TryGetProperty("file_path", out var pathNode))
            throw new TelegramFileException("invalid_file_response");
        var path = pathNode.GetString();
        if (!SafeFilePath(path)) throw new TelegramFileException("invalid_file_path");
        if (file.TryGetProperty("file_size", out var sizeNode) && sizeNode.TryGetInt64(out var size)
            && size > maximumBytes) throw new TelegramFileException("file_too_large");

        var token = options.Value.BotToken!.Trim();
        HttpResponseMessage response;
        try
        { response = await http.GetAsync($"/file/bot{token}/{path}", HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        { throw TelegramBotApiException.Transport("downloadFile", "timeout", exception); }
        catch (HttpRequestException exception)
        { throw TelegramBotApiException.Transport("downloadFile", "network", exception); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw TelegramBotApiException.ApiResponse("downloadFile", response.StatusCode, null, null, "http_error");
            if (response.Content.Headers.ContentLength > maximumBytes)
                throw new TelegramFileException("file_too_large");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, ct);
                if (read == 0) break;
                if (buffer.Length + read > maximumBytes) throw new TelegramFileException("file_too_large");
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            return buffer.ToArray();
        }
    }

    private async Task<JsonElement> PostAsync(string method, object body, CancellationToken ct)
    {
        var token = options.Value.BotToken?.Trim();
        if (!options.Value.IsConfigured || string.IsNullOrWhiteSpace(token)) return default;
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

            if (response.IsSuccessStatusCode && result?.Ok == true) return result.Result;

            var category = response.IsSuccessStatusCode
                ? result?.Ok == false ? "api_rejected" : "invalid_response"
                : "http_error";
            throw TelegramBotApiException.ApiResponse(method, response.StatusCode,
                result?.ErrorCode, result?.Description, category);
        }
    }

    private static bool SafeFilePath(string? path) => !string.IsNullOrWhiteSpace(path)
        && path.Length <= 512 && !path.StartsWith('/') && !path.Contains('\\')
        && path.Split('/').All(segment => segment.Length > 0 && segment != ".."
            && segment.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '_' or '-' or '.'));

    private sealed record TelegramBotApiResponse(bool? Ok,
        [property: JsonPropertyName("error_code")] int? ErrorCode, string? Description,
        JsonElement Result);
}

public sealed class TelegramFileException(string code) : Exception("Telegram file processing failed.")
{
    public string Code { get; } = code;
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
