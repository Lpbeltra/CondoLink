using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.TelegramAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class TelegramActionCallbacksTests
{
    [Theory]
    [InlineData("c")]
    [InlineData("x")]
    public void Callback_round_trips_within_telegram_limit(string suffix)
    {
        var decision = suffix == "c" ? TelegramActionDecision.Confirm : TelegramActionDecision.Cancel;
        var id = Guid.NewGuid();
        var value = TelegramActionCallbacks.Format(id, decision);
        Assert.EndsWith($":{suffix}", value);
        Assert.InRange(Encoding.UTF8.GetByteCount(value), 1, 64);
        Assert.True(TelegramActionCallbacks.TryParse(value, out var parsed));
        Assert.Equal(id, parsed.ActionId); Assert.Equal(decision, parsed.Decision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ca:v2:00000000000000000000000000000000:c")]
    [InlineData("ca:v1:not-a-guid:c")]
    [InlineData("ca:v1:00000000000000000000000000000000:c")]
    [InlineData("ca:v1:00000000000000000000000000000000:y")]
    [InlineData("ca:v1:00000000000000000000000000000000:c:extra")]
    public void Callback_parser_rejects_untrusted_formats(string value) =>
        Assert.False(TelegramActionCallbacks.TryParse(value, out _));

    [Fact]
    public void Keyboard_contains_only_the_same_opaque_action_id_and_two_decisions()
    {
        var id = Guid.NewGuid(); var keyboard = TelegramActionCallbacks.Keyboard(id);
        Assert.Equal(["Confirmar", "Cancelar"], keyboard.Buttons.Select(x => x.Text));
        Assert.All(keyboard.Buttons, x => Assert.DoesNotContain(id.ToString(), x.CallbackData));
        Assert.All(keyboard.Buttons, x => Assert.True(TelegramActionCallbacks.TryParse(x.CallbackData, out _)));
        Assert.All(keyboard.Buttons, x => { TelegramActionCallbacks.TryParse(x.CallbackData, out var parsed); Assert.Equal(id, parsed.ActionId); });
    }

    [Fact]
    public void Persisted_response_round_trips_and_never_leaks_the_marker_to_visible_text()
    {
        var id = Guid.NewGuid(); var persisted = new TelegramAssistantResponse("Preview seguro", id).Persist();
        var parsed = TelegramAssistantResponse.Read(persisted);
        Assert.Equal("Preview seguro", parsed.Text); Assert.Equal(id, parsed.PendingActionId);
        Assert.DoesNotContain("\u001faction:", parsed.Text);
        Assert.Null(TelegramAssistantResponse.Read("Resposta comum").PendingActionId);
    }

    [Fact]
    public async Task Client_serializes_inline_keyboard_and_answers_callback()
    {
        var requests = new List<HttpRequestMessage>();
        var client = new TelegramBotClient(new HttpClient(new Handler((request, _) =>
        { requests.Add(request); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
          { Content = new StringContent("{\"ok\":true,\"result\":{}}") }); })) { BaseAddress = new("https://api.telegram.org/") },
            Options.Create(new TelegramAssistantOptions { Enabled = true, BotToken = "1:test", WebhookSecret = "test" }));
        await client.SendInlineMessageAsync(9, "Preview", TelegramActionCallbacks.Keyboard(Guid.NewGuid()), default);
        await client.AnswerCallbackAsync("callback-id", null, default);
        var send = JsonDocument.Parse(await requests[0].Content!.ReadAsStringAsync()).RootElement;
        Assert.Equal(9, send.GetProperty("chat_id").GetInt64());
        Assert.Equal(2, send.GetProperty("reply_markup").GetProperty("inline_keyboard")[0].GetArrayLength());
        Assert.EndsWith("/answerCallbackQuery", requests[1].RequestUri!.AbsolutePath);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}

public sealed class TelegramActionResolverTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!; private Guid _actor, _condo; private const long Chat = 991;
    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(_ => { });
        await _host.WithDbAsync(async db => { var user = CoreTestSeed.User("Actor", "actor@telegram.test"); var condo = new Condominium("Telegram", null, null); db.AddRange(user, condo); await db.SaveChangesAsync(); _actor = user.Id; _condo = condo.Id; });
    }
    public Task DisposeAsync() => _host.DisposeAsync().AsTask();
    [Fact]
    public async Task Resolver_accepts_pending_confirm_and_cancel_without_transition()
    {
        var id = await AddAsync(CondominiumAssistantChannel.Telegram);
        foreach (var decision in new[] { TelegramActionDecision.Confirm, TelegramActionDecision.Cancel })
        { var result = await _host.WithDbAsync(db => TelegramActionCallbacks.ResolveAsync(db, new(id, decision), _actor, Chat, _condo, default)); Assert.True(result.Accepted); Assert.Equal(decision, result.Callback!.Decision); }
        Assert.Equal(PendingAssistantActionStatus.Pending, await _host.WithDbAsync(db => db.PendingAssistantActions.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
    }
    [Fact]
    public async Task Resolver_rejects_wrong_actor_chat_channel_condominium_and_missing_action()
    {
        var id = await AddAsync(CondominiumAssistantChannel.Telegram);
        Assert.False((await _host.WithDbAsync(db => TelegramActionCallbacks.ResolveAsync(db, new(id, TelegramActionDecision.Confirm), Guid.NewGuid(), Chat, _condo, default))).Accepted);
        Assert.False((await _host.WithDbAsync(db => TelegramActionCallbacks.ResolveAsync(db, new(id, TelegramActionDecision.Confirm), _actor, Chat + 1, _condo, default))).Accepted);
        Assert.False((await _host.WithDbAsync(db => TelegramActionCallbacks.ResolveAsync(db, new(Guid.NewGuid(), TelegramActionDecision.Confirm), _actor, Chat, _condo, default))).Accepted);
    }
    private Task<Guid> AddAsync(CondominiumAssistantChannel channel) => _host.WithDbAsync(async db => { var a = new PendingAssistantAction(PendingAssistantActionType.ResidentRegistration, _actor, _condo, channel, TelegramActionCallbacks.ChatContext(Chat), "{}", Guid.NewGuid().ToString("N"), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15)); db.Add(a); await db.SaveChangesAsync(); return a.Id; });
}
