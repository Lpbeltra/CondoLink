using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.TelegramAssistant;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class TelegramBotClientTests
{
    [Fact]
    public async Task SendMessage_uses_https_bot_api_path_and_accepts_ok_response()
    {
        HttpRequestMessage? captured = null;
        var client = Client((request, _) =>
        {
            captured = request;
            return Task.FromResult(Response(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":1}}"""));
        });

        await client.SendMessageAsync(123456, "safe test message", default);

        Assert.NotNull(captured);
        Assert.Equal("https://api.telegram.org/bot123456:TEST_TOKEN/sendMessage", captured.RequestUri!.AbsoluteUri);
        Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
        var body = JsonDocument.Parse(await captured.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(123456, body.GetProperty("chat_id").GetInt64());
        Assert.Equal("safe test message", body.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    public async Task SendMessage_exposes_safe_telegram_error_details(HttpStatusCode status,
        int errorCode)
    {
        var client = Client((_, _) => Task.FromResult(Response(status,
            $$"""{"ok":false,"error_code":{{errorCode}},"description":"safe Telegram description"}""")));

        var exception = await Assert.ThrowsAsync<TelegramBotApiException>(() =>
            client.SendMessageAsync(123456, "private assistant response", default));

        Assert.Equal("sendMessage", exception.Operation);
        Assert.Equal(status, exception.HttpStatus);
        Assert.Equal(errorCode, exception.TelegramErrorCode);
        Assert.Equal("safe Telegram description", exception.TelegramDescription);
        Assert.Equal("http_error", exception.Category);
        Assert.DoesNotContain("123456:TEST_TOKEN", exception.ToString());
        Assert.DoesNotContain("private assistant response", exception.ToString());
        Assert.DoesNotContain("test-secret", exception.ToString());
    }

    [Fact]
    public async Task SendMessage_rejects_http_200_with_ok_false()
    {
        var client = Client((_, _) => Task.FromResult(Response(HttpStatusCode.OK,
            """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""")));

        var exception = await Assert.ThrowsAsync<TelegramBotApiException>(() =>
            client.SendMessageAsync(123456, "safe test message", default));

        Assert.Equal(HttpStatusCode.OK, exception.HttpStatus);
        Assert.Equal(400, exception.TelegramErrorCode);
        Assert.Equal("api_rejected", exception.Category);
    }

    [Fact]
    public async Task SendMessage_classifies_timeout_without_exposing_payload()
    {
        var client = Client((_, _) => Task.FromException<HttpResponseMessage>(
            new TaskCanceledException("simulated timeout")));

        var exception = await Assert.ThrowsAsync<TelegramBotApiException>(() =>
            client.SendMessageAsync(123456, "private assistant response", default));

        Assert.Equal("timeout", exception.Category);
        Assert.Equal("TaskCanceledException", exception.ExceptionType);
        Assert.Null(exception.HttpStatus);
        Assert.DoesNotContain("private assistant response", exception.ToString());
    }

    [Fact]
    public async Task SendMessage_classifies_network_failure_without_exposing_token()
    {
        var client = Client((_, _) => Task.FromException<HttpResponseMessage>(
            new HttpRequestException("simulated network failure")));

        var exception = await Assert.ThrowsAsync<TelegramBotApiException>(() =>
            client.SendMessageAsync(123456, "safe test message", default));

        Assert.Equal("network", exception.Category);
        Assert.Equal("HttpRequestException", exception.ExceptionType);
        Assert.Null(exception.HttpStatus);
        Assert.DoesNotContain("123456:TEST_TOKEN", exception.ToString());
    }

    private static TelegramBotClient Client(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new HttpClient(new Handler(send)) { BaseAddress = new("https://api.telegram.org/") },
            Options.Create(new TelegramAssistantOptions
            {
                Enabled = true,
                BotToken = "123456:TEST_TOKEN",
                WebhookSecret = "test-secret"
            }));

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
