using System.Net.Http.Json;
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
        var token = options.Value.BotToken;
        if (!options.Value.IsConfigured || string.IsNullOrWhiteSpace(token)) return;
        using var response = await http.PostAsJsonAsync($"bot{token}/{method}", body, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Telegram Bot API request failed.", null, response.StatusCode);
    }
}
