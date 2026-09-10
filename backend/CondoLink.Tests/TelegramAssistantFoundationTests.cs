using System.Net;
using System.Net.Http.Json;
using System.Text;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.TelegramAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class TelegramAssistantFoundationTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private readonly FakeTelegramBotClient _bot = new();
    private Guid _managerId;
    private Guid _residentId;
    private Guid _allowedSubManagerId;
    private Guid _deniedSubManagerId;
    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app => app.MapTelegramAssistant(), builder =>
        {
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<ITelegramBotClient>(_bot);
            builder.Services.Configure<TelegramAssistantOptions>(x =>
            { x.Enabled = true; x.BotToken = "test-token"; x.WebhookSecret = "test-secret"; x.BotUsername = "comvy_test_bot"; });
        });
        await _host.WithDbAsync(async db =>
        {
            var manager = CoreTestSeed.User("Manager", "telegram.manager@example.com");
            var resident = CoreTestSeed.User("Resident", "telegram.resident@example.com");
            var allowedSubManager = CoreTestSeed.User("Allowed", "telegram.allowed@example.com");
            var deniedSubManager = CoreTestSeed.User("Denied", "telegram.denied@example.com");
            var condominium = new Condominium("Monticello", null, null);
            db.AddRange(manager, resident, allowedSubManager, deniedSubManager, condominium);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id, CondominiumRole.Resident);
            var allowedMembership = CoreTestSeed.AddMember(db, allowedSubManager.Id, condominium.Id, CondominiumRole.SubManager);
            db.SubManagerModulePermissions.Add(new(allowedMembership.Id, SubManagerModule.Assistant, manager.Id));
            var deniedMembership = CoreTestSeed.AddMember(db, deniedSubManager.Id, condominium.Id, CondominiumRole.SubManager);
            var denied = new SubManagerModulePermission(deniedMembership.Id, SubManagerModule.Assistant, manager.Id);
            denied.SetAllowed(false, manager.Id); db.SubManagerModulePermissions.Add(denied);
            await db.SaveChangesAsync(); _managerId = manager.Id; _residentId = resident.Id;
            _allowedSubManagerId = allowedSubManager.Id; _deniedSubManagerId = deniedSubManager.Id;
        });
    }

    [Fact]
    public async Task Access_allows_manager_and_permitted_submanager_only()
    {
        await _host.WithDbAsync(async db =>
        {
            Assert.Single(await TelegramAssistantAccess.CondominiumsAsync(db, _managerId, default));
            Assert.Single(await TelegramAssistantAccess.CondominiumsAsync(db, _allowedSubManagerId, default));
            Assert.Empty(await TelegramAssistantAccess.CondominiumsAsync(db, _deniedSubManagerId, default));
            Assert.Empty(await TelegramAssistantAccess.CondominiumsAsync(db, _residentId, default));
        });
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _host.ClientFor(_residentId).PostAsync("/users/me/telegram/link-code", null)).StatusCode);
    }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Webhook_requires_secret_and_deduplicates_update()
    {
        var rejected = await _host.AnonymousClient().PostAsJsonAsync("/integrations/telegram/webhook", Payload(10));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        var client = _host.AnonymousClient(); client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "test-secret");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/integrations/telegram/webhook", Payload(10))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/integrations/telegram/webhook", Payload(10))).StatusCode);
        await _host.WithDbAsync(async db => Assert.Single(await db.TelegramInboundUpdates.ToArrayAsync()));
    }

    [Fact]
    public async Task Webhook_ignores_groups_and_invalid_json_safely()
    {
        var client = _host.AnonymousClient(); client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "test-secret");
        await client.PostAsJsonAsync("/integrations/telegram/webhook", Payload(11, "group"));
        using var invalid = new StringContent("{", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/integrations/telegram/webhook", invalid)).StatusCode);
        await _host.WithDbAsync(async db => Assert.Empty(await db.TelegramInboundUpdates.ToArrayAsync()));
    }

    [Fact]
    public async Task Manager_generates_hashed_single_use_code_and_deep_link()
    {
        var response = await _host.ClientFor(_managerId).PostAsync("/users/me/telegram/link-code", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LinkCodeResponse>();
        Assert.NotNull(body); Assert.Equal(8, body.Code.Length); Assert.Contains("t.me/comvy_test_bot", body.DeepLink);
        await _host.WithDbAsync(async db =>
        { var stored = await db.TelegramLinkCodes.SingleAsync(); Assert.NotEqual(body.Code, stored.CodeHash);
          Assert.Equal(TelegramAssistantEndpoints.HashCode(body.Code), stored.CodeHash); Assert.True(stored.IsUsable(DateTime.UtcNow));
          stored.Use(DateTime.UtcNow); Assert.False(stored.IsUsable(DateTime.UtcNow)); });
    }

    [Fact]
    public async Task Worker_consumes_persistent_inbox_once()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Add(new TelegramUserLink(_managerId, 555, 555, DateTime.UtcNow));
            db.Add(new TelegramInboundUpdate(55, 555, 555, "/ajuda", DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
        await _host.WithServicesAsync(async services =>
        {
            var worker = ActivatorUtilities.CreateInstance<TelegramAssistantWorker>(services);
            Assert.True(await worker.ProcessOneAsync(default));
            Assert.False(await worker.ProcessOneAsync(default));
        });
        Assert.Single(_bot.Messages);
        Assert.Contains("não executa alterações", _bot.Messages[0]);
        await _host.WithDbAsync(async db => Assert.Equal(TelegramInboundStatus.Completed,
            (await db.TelegramInboundUpdates.SingleAsync()).Status));
    }

    [Fact]
    public void Formats_compact_sources_and_splits_long_answers()
    {
        var documentId = Guid.NewGuid();
        var answer = new AssistantAnswer("Resposta", [
            new(documentId, "Ata AGO", 3, null, "x", "[F1]"),
            new(documentId, "Ata AGO", 4, null, "y", "[F2]")], "test");
        var formatted = TelegramAssistantWorker.FormatAnswer(answer);
        Assert.Contains("• Ata AGO — pág. 3, 4", formatted); Assert.DoesNotContain("storage", formatted);
        Assert.All(TelegramAssistantWorker.SplitMessage(new string('a', 9000)), part => Assert.InRange(part.Length, 1, 3800));
    }

    [Fact]
    public void Inbox_preserves_prepared_delivery_progress_and_conversation_channel()
    {
        var now = DateTime.UtcNow;
        var update = new TelegramInboundUpdate(99, 123, 123, "Pergunta", now);
        update.PrepareResponse("Resposta"); update.PartSent();
        update.Retry(now, TimeSpan.FromSeconds(5), "telegram_api", 1);
        Assert.Equal(TelegramInboundStatus.Pending, update.Status);
        Assert.Equal(1, update.SentPartCount);
        Assert.Equal(CondominiumAssistantChannel.Portal,
            new CondominiumAssistantConversation(Guid.NewGuid(), Guid.NewGuid(), null, "Portal").Channel);
        Assert.Equal(CondominiumAssistantChannel.Telegram,
            new CondominiumAssistantConversation(Guid.NewGuid(), Guid.NewGuid(), null, "Telegram",
                CondominiumAssistantChannel.Telegram).Channel);
    }

    private static object Payload(long updateId, string type = "private") => new
    { update_id = updateId, message = new { from = new { id = 123L }, chat = new { id = 123L, type }, text = "Olá" } };
    private sealed record LinkCodeResponse(string Code, string? DeepLink, DateTime ExpiresAt);
    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        public List<string> Messages { get; } = [];
        public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
        { Messages.Add(text); return Task.CompletedTask; }
        public Task SendTypingAsync(long chatId, CancellationToken ct) => Task.CompletedTask;
    }
}
