using System.Net;
using System.Net.Http.Json;
using System.Text;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.TelegramAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CondoLink.Api.Features.WhatsApp;

namespace CondoLink.Tests;

public sealed class TelegramAssistantFoundationTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private readonly FakeTelegramBotClient _bot = new();
    private readonly RecordingLogger<TelegramAssistantWorker> _workerLogger = new();
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
            builder.Services.AddSingleton<ILogger<TelegramAssistantWorker>>(_workerLogger);
            builder.Services.AddSingleton<IEmbeddingService, NoOpEmbeddingService>();
            builder.Services.AddScoped(services => new CondominiumAssistantService(
                services.GetRequiredService<CondoLink.Infrastructure.Persistence.AppDbContext>(),
                services.GetRequiredService<IEmbeddingService>(), new HttpClient(),
                Options.Create(new RequestDraftAiOptions()),
                Options.Create(new CondominiumAssistantOptions()),
                NullLogger<CondominiumAssistantService>.Instance));
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
        Assert.NotNull(body); Assert.Equal(8, body.Code.Length);
        Assert.Equal($"https://t.me/comvy_test_bot?start={body.Code}", body.DeepLink);
        await _host.WithDbAsync(async db =>
        { var stored = await db.TelegramLinkCodes.SingleAsync(); Assert.NotEqual(body.Code, stored.CodeHash);
          Assert.Equal(TelegramAssistantEndpoints.HashCode(body.Code), stored.CodeHash); Assert.True(stored.IsUsable(DateTime.UtcNow));
          stored.Use(DateTime.UtcNow); Assert.False(stored.IsUsable(DateTime.UtcNow)); });
    }

    [Fact]
    public async Task Start_without_code_does_not_link_and_returns_guidance()
    {
        await ProcessTextAsync(60, 600, "/start");
        Assert.Contains("primeiro faça a vinculação", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Empty(await db.TelegramUserLinks.ToArrayAsync()));
    }

    [Fact]
    public async Task Valid_start_links_once_and_consumes_code()
    {
        const string code = "ABC234XY";
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramLinkCode(_managerId, TelegramAssistantEndpoints.HashCode(code),
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10))); await db.SaveChangesAsync(); });
        await ProcessTextAsync(61, 601, $"/start {code}");
        Assert.Contains("Telegram vinculado", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db =>
        { Assert.True((await db.TelegramUserLinks.SingleAsync()).IsActive);
          Assert.NotNull((await db.TelegramLinkCodes.SingleAsync()).UsedAt); });
        _bot.Messages.Clear();
        await ProcessTextAsync(62, 602, $"/start {code}");
        Assert.Contains("inválido ou expirado", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Single(await db.TelegramUserLinks.ToArrayAsync()));
    }

    [Fact]
    public async Task Linked_user_receives_structured_assistant_response()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Add(new TelegramUserLink(_managerId, 608, 608, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });

        await ProcessTextAsync(69, 608, "Quais documentos estão cadastrados?");

        Assert.Contains("Não há documentos ativos cadastrados", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Equal(TelegramInboundStatus.Completed,
            (await db.TelegramInboundUpdates.SingleAsync(x => x.UpdateId == 69)).Status));
    }

    [Fact]
    public async Task Invalid_and_expired_codes_do_not_link()
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramLinkCode(_managerId, TelegramAssistantEndpoints.HashCode("EXP234XY"),
            DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-10))); await db.SaveChangesAsync(); });
        await ProcessTextAsync(63, 603, "/start BAD234XY");
        await ProcessTextAsync(64, 604, "/start EXP234XY");
        Assert.All(_bot.Messages, message => Assert.Contains("inválido ou expirado", message));
        await _host.WithDbAsync(async db => Assert.Empty(await db.TelegramUserLinks.ToArrayAsync()));
    }

    [Fact]
    public async Task Multiple_condominiums_require_selection_and_command_selects_context()
    {
        await _host.WithDbAsync(async db =>
        { var other = new Condominium("Outro", null, null); db.Add(other);
          CoreTestSeed.AddMember(db, _managerId, other.Id, CondominiumRole.Manager);
          db.Add(new TelegramUserLink(_managerId, 605, 605, DateTime.UtcNow)); await db.SaveChangesAsync(); });
        await ProcessTextAsync(65, 605, "Qual foi a última assembleia?");
        Assert.Contains("Escolha primeiro", Assert.Single(_bot.Messages));
        Assert.Contains("Monticello", _bot.Messages[0]); Assert.Contains("Outro", _bot.Messages[0]);
        _bot.Messages.Clear(); await ProcessTextAsync(66, 605, "/condominio 1");
        Assert.Contains("Contexto atual", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.NotNull((await db.TelegramUserLinks.SingleAsync()).ActiveCondominiumId));
    }

    [Fact]
    public async Task Assistant_failure_retries_then_returns_safe_feedback()
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramUserLink(_managerId, 606, 606, DateTime.UtcNow)); await db.SaveChangesAsync(); });
        await ProcessTextAsync(67, 606, "Pergunta normal");
        for (var attempt = 2; attempt <= 4; attempt++)
        {
            await _host.WithDbAsync(db => db.TelegramInboundUpdates.Where(x => x.UpdateId == 67)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1))));
            await ProcessPendingAsync();
        }
        Assert.Contains("Não consegui concluir", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Equal(TelegramInboundStatus.Completed,
            (await db.TelegramInboundUpdates.SingleAsync(x => x.UpdateId == 67)).Status));
    }

    [Fact]
    public async Task Telegram_delivery_failure_retries_without_losing_prepared_response()
    {
        _bot.FailSends = true;
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramUserLink(_managerId, 607, 607, DateTime.UtcNow)); await db.SaveChangesAsync(); });
        await ProcessTextAsync(68, 607, "/ajuda");
        for (var attempt = 2; attempt <= 4; attempt++)
        {
            await _host.WithDbAsync(db => db.TelegramInboundUpdates.Where(x => x.UpdateId == 68)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddSeconds(-1))));
            await ProcessPendingAsync();
        }
        await _host.WithDbAsync(async db =>
        { var update = await db.TelegramInboundUpdates.SingleAsync(x => x.UpdateId == 68);
          Assert.Equal(TelegramInboundStatus.Failed, update.Status); Assert.Equal(4, update.Attempts);
          Assert.NotNull(update.ResponseText); Assert.Equal("telegram_api", update.LastError); });
    }

    [Fact]
    public async Task Telegram_delivery_logs_safe_api_diagnostics_without_secrets_or_content()
    {
        const string privateContent = "PRIVATE_ASSISTANT_CONTENT_DO_NOT_LOG";
        _bot.Failure = TelegramBotApiException.ApiResponse("sendMessage", HttpStatusCode.Forbidden,
            403, "Forbidden: bot was blocked by the user", "http_error");
        await _host.WithDbAsync(async db =>
        {
            var update = new TelegramInboundUpdate(70, 609, 609, "PRIVATE_USER_TEXT_DO_NOT_LOG", DateTime.UtcNow);
            update.PrepareResponse(privateContent);
            db.Add(update);
            await db.SaveChangesAsync();
        });

        await ProcessPendingAsync();

        var logs = string.Join('\n', _workerLogger.Entries);
        Assert.Contains("Operation: sendMessage", logs);
        Assert.Contains("HTTPStatus: 403", logs);
        Assert.Contains("TelegramErrorCode: 403", logs);
        Assert.Contains("Forbidden: bot was blocked by the user", logs);
        Assert.DoesNotContain(privateContent, logs);
        Assert.DoesNotContain("PRIVATE_USER_TEXT_DO_NOT_LOG", logs);
        Assert.DoesNotContain("test-token", logs);
        Assert.DoesNotContain("test-secret", logs);
    }

    [Fact]
    public void Parses_start_payload_without_logging_or_depending_on_bot_suffix()
    {
        Assert.Equal(("/start", "ABC234XY"), TelegramAssistantWorker.ParseCommand("/start ABC234XY"));
        Assert.Equal(("/start", "ABC234XY"), TelegramAssistantWorker.ParseCommand("/start@comvy_bot ABC234XY"));
        Assert.Equal(("/start", (string?)null), TelegramAssistantWorker.ParseCommand("/start"));
        Assert.Equal("https://t.me/comvy_bot?start=ABC234XY",
            TelegramAssistantEndpoints.BuildDeepLink("@comvy_bot", "ABC234XY"));
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
    private async Task ProcessTextAsync(long updateId, long telegramId, string text)
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramInboundUpdate(updateId, telegramId, telegramId, text, DateTime.UtcNow)); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
    }
    private Task ProcessPendingAsync() => _host.WithServicesAsync(async services =>
    {
        var worker = ActivatorUtilities.CreateInstance<TelegramAssistantWorker>(services);
        Assert.True(await worker.ProcessOneAsync(default));
    });
    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        public List<string> Messages { get; } = [];
        public bool FailSends { get; set; }
        public Exception? Failure { get; set; }
        public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
        { if (Failure is not null) throw Failure; if (FailSends) throw new HttpRequestException("failed"); Messages.Add(text); return Task.CompletedTask; }
        public Task SendTypingAsync(long chatId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoOpEmbeddingService : IEmbeddingService
    {
        public string Model => "telegram-test";
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<float>());
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }
}
