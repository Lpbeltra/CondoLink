using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    private readonly FakeAudioTranscriptionService _transcription = new();
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
            builder.Services.AddSingleton<IWhatsAppAudioTranscriptionService>(_transcription);
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
            manager.Update("Manager", "+55 (44) 99999-9999");
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
    public async Task Valid_start_waits_for_own_matching_contact_then_links_once()
    {
        const string code = "ABC234XY";
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramLinkCode(_managerId, TelegramAssistantEndpoints.HashCode(code),
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10))); await db.SaveChangesAsync(); });
        await ProcessTextAsync(61, 601, $"/start {code}");
        Assert.Contains("Confirmar meu telefone", Assert.Single(_bot.Messages));
        Assert.Equal(TelegramReplyMarkup.RequestContact, Assert.Single(_bot.Markups));
        await _host.WithDbAsync(async db =>
        { Assert.Empty(await db.TelegramUserLinks.ToArrayAsync());
          Assert.Null((await db.TelegramLinkCodes.SingleAsync()).UsedAt); });
        _bot.Messages.Clear(); _bot.Markups.Clear();
        await ProcessContactAsync(62, 601, "+55 44 99999-9999", 601);
        Assert.Contains("Telegram vinculado", Assert.Single(_bot.Messages));
        Assert.Equal(TelegramReplyMarkup.RemoveKeyboard, Assert.Single(_bot.Markups));
        await _host.WithDbAsync(async db =>
        { Assert.True((await db.TelegramUserLinks.SingleAsync()).IsActive);
          Assert.NotNull((await db.TelegramLinkCodes.SingleAsync()).UsedAt); });
        _bot.Messages.Clear();
        await ProcessTextAsync(63, 602, $"/start {code}");
        Assert.Contains("inválido ou expirado", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Single(await db.TelegramUserLinks.ToArrayAsync()));
    }

    [Fact]
    public async Task Third_party_and_mismatched_contacts_never_create_link()
    {
        const string code = "OWN234XY";
        await AddCodeAsync(_managerId, code);
        await ProcessTextAsync(71, 701, $"/start {code}");
        _bot.Messages.Clear(); _bot.Markups.Clear();

        await ProcessTextAsync(711, 702, $"/start {code}");
        Assert.Contains("inválido ou expirado", Assert.Single(_bot.Messages));
        _bot.Messages.Clear(); _bot.Markups.Clear();

        await ProcessContactAsync(72, 701, "+5544999999999", 999);
        Assert.Contains("não pertence", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db => Assert.Empty(await db.TelegramUserLinks.ToArrayAsync()));

        _bot.Messages.Clear(); _bot.Markups.Clear();
        await ProcessContactAsync(73, 701, "+5544888888888", 701);
        Assert.Contains("não corresponde", Assert.Single(_bot.Messages));
        await _host.WithDbAsync(async db =>
        { Assert.Empty(await db.TelegramUserLinks.ToArrayAsync()); Assert.Null((await db.TelegramLinkCodes.SingleAsync()).UsedAt); });
    }

    [Fact]
    public async Task Account_without_phone_cannot_begin_phone_verification()
    {
        const string code = "NOP234XY";
        await AddCodeAsync(_allowedSubManagerId, code);
        await ProcessTextAsync(74, 704, $"/start {code}");
        Assert.Contains("cadastre/atualize seu telefone", _bot.Messages.Last());
        await _host.WithDbAsync(async db =>
        { Assert.Empty(await db.TelegramUserLinks.ToArrayAsync()); Assert.Null((await db.TelegramLinkCodes.SingleAsync()).PendingTelegramUserId); });
    }

    [Fact]
    public async Task Webhook_persists_contact_voice_and_audio_without_content_logging()
    {
        var client = _host.AnonymousClient();
        client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "test-secret");
        await client.PostAsJsonAsync("/integrations/telegram/webhook", new
        { update_id = 801L, message = new { from = new { id = 801L }, chat = new { id = 801L, type = "private" }, contact = new { phone_number = "+5544999999999", user_id = 801L } } });
        await client.PostAsJsonAsync("/integrations/telegram/webhook", new
        { update_id = 802L, message = new { from = new { id = 801L }, chat = new { id = 801L, type = "private" }, voice = new { file_id = "voice-file", file_size = 100L, duration = 4 } } });
        await client.PostAsJsonAsync("/integrations/telegram/webhook", new
        { update_id = 803L, message = new { from = new { id = 801L }, chat = new { id = 801L, type = "private" }, audio = new { file_id = "audio-file", file_size = 200L, duration = 5, mime_type = "audio/mpeg", file_name = "question.mp3" } } });
        await _host.WithDbAsync(async db => Assert.Equal(
            [TelegramInboundKind.Contact, TelegramInboundKind.Voice, TelegramInboundKind.Audio],
            await db.TelegramInboundUpdates.OrderBy(x => x.UpdateId).Select(x => x.Kind).ToArrayAsync()));
    }

    [Fact]
    public async Task Voice_is_downloaded_transcribed_and_uses_text_pipeline_with_typing_and_metrics()
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramUserLink(_managerId, 805, 805, DateTime.UtcNow));
          db.Add(new TelegramInboundUpdate(805, 805, 805, TelegramInboundKind.Voice,
              DateTime.UtcNow, fileId: "voice-file", fileSize: 3, durationSeconds: 2)); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
        Assert.Equal(1, _bot.DownloadCount); Assert.Equal(1, _transcription.Calls); Assert.True(_bot.TypingCount >= 1);
        await _host.WithDbAsync(async db =>
        {
            var update = await db.TelegramInboundUpdates.SingleAsync(x => x.UpdateId == 805);
            Assert.Equal("Quais documentos estão cadastrados?", update.Text);
            Assert.NotNull(update.AssistantExecutionId); Assert.NotNull(update.QueueDurationMs);
            Assert.NotNull(update.AudioDownloadDurationMs); Assert.NotNull(update.TranscriptionDurationMs);
            Assert.NotNull(update.AssistantDurationMs); Assert.NotNull(update.DeliveryDurationMs); Assert.NotNull(update.TotalDurationMs);
            Assert.Contains(await db.CondominiumAssistantMessages.ToArrayAsync(),
                x => x.Role == CondominiumAssistantRole.User && x.Content == update.Text);
        });
        await ProcessTextAsync(8051, 805, "Quais documentos estão cadastrados?");
        await _host.WithDbAsync(async db =>
        { Assert.Single(await db.CondominiumAssistantConversations.ToArrayAsync());
          Assert.Equal(2, await db.CondominiumAssistantMessages.CountAsync(x => x.Role == CondominiumAssistantRole.User)); });
    }

    [Fact]
    public async Task Audio_followup_after_text_reuses_same_conversation()
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramUserLink(_managerId, 809, 809, DateTime.UtcNow)); await db.SaveChangesAsync(); });
        await ProcessTextAsync(8090, 809, "Quais documentos estão cadastrados?");
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramInboundUpdate(8091, 809, 809, TelegramInboundKind.Audio,
              DateTime.UtcNow, fileId: "audio-file", fileSize: 3, durationSeconds: 2,
              fileName: "question.mp3", mimeType: "audio/mpeg")); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
        Assert.Equal(1, _bot.DownloadCount); Assert.Equal(1, _transcription.Calls);
        await _host.WithDbAsync(async db =>
        { Assert.Single(await db.CondominiumAssistantConversations.ToArrayAsync());
          Assert.Equal(2, await db.CondominiumAssistantMessages.CountAsync(x => x.Role == CondominiumAssistantRole.User)); });
    }

    [Fact]
    public async Task Invalid_large_and_failed_audio_return_safe_feedback()
    {
        await _host.WithDbAsync(async db =>
        {
            db.Add(new TelegramUserLink(_managerId, 806, 806, DateTime.UtcNow));
            db.Add(new TelegramInboundUpdate(806, 806, 806, TelegramInboundKind.Audio,
                DateTime.UtcNow, fileId: "large", fileSize: 20 * 1024 * 1024, durationSeconds: 2,
                mimeType: "audio/mpeg")); await db.SaveChangesAsync();
        });
        await ProcessPendingAsync();
        Assert.Contains("Não consegui entender", _bot.Messages.Last()); Assert.Equal(0, _bot.DownloadCount);

        _bot.Messages.Clear(); _transcription.Result = new(false, null, "provider_error");
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramInboundUpdate(807, 806, 806, TelegramInboundKind.Audio,
              DateTime.UtcNow, fileId: "audio", fileSize: 3, durationSeconds: 2,
              mimeType: "audio/mpeg")); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
        Assert.Contains("Não consegui entender", Assert.Single(_bot.Messages));

        _bot.Messages.Clear(); _bot.DownloadFailure = new HttpRequestException("download failed");
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramInboundUpdate(8071, 806, 806, TelegramInboundKind.Voice,
              DateTime.UtcNow, fileId: "voice", fileSize: 3, durationSeconds: 2)); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
        Assert.Contains("Não consegui entender", Assert.Single(_bot.Messages));
    }

    [Fact]
    public async Task Source_question_uses_previous_persisted_sources_without_new_rag()
    {
        var source = new AssistantSource(Guid.NewGuid(), "Ata da AGO — 09/04/2026", 3,
            null, "trecho", "[S1]", Guid.NewGuid(), "secret-storage-name.pdf");
        await _host.WithDbAsync(async db =>
        {
            var link = new TelegramUserLink(_managerId, 808, 808, DateTime.UtcNow); db.Add(link);
            var condominiumId = await db.Condominiums.Select(x => x.Id).SingleAsync(); link.SelectCondominium(condominiumId, DateTime.UtcNow);
            var conversation = new CondominiumAssistantConversation(condominiumId, _managerId, null,
                "Telegram", CondominiumAssistantChannel.Telegram); db.Add(conversation);
            db.Add(new CondominiumAssistantMessage(conversation.Id, CondominiumAssistantRole.Assistant,
                "Resposta anterior", JsonSerializer.Serialize(new[] { source }, CondominiumAssistantEndpoints.AssistantJsonOptions)));
            await db.SaveChangesAsync();
        });
        await ProcessTextAsync(808, 808, "qual a fonte?");
        var response = Assert.Single(_bot.Messages);
        Assert.Contains("Ata da AGO", response); Assert.Contains("página 3", response);
        Assert.DoesNotContain(source.DocumentId.ToString(), response); Assert.DoesNotContain(source.ChunkId!.Value.ToString(), response);
        Assert.DoesNotContain("secret-storage", response);
    }

    [Fact]
    public void Source_question_detection_and_empty_source_response_are_deterministic()
    {
        Assert.True(TelegramAssistantWorker.IsSourceQuestion("De onde você tirou isso?"));
        Assert.True(TelegramAssistantWorker.IsSourceQuestion("Onde isso está escrito?"));
        Assert.False(TelegramAssistantWorker.IsSourceQuestion("Qual foi a última assembleia?"));
        Assert.Equal("Essa resposta não possui uma fonte documental associada.",
            TelegramAssistantWorker.FormatSources([]));
    }

    [Fact]
    public async Task Typing_renews_without_messages_and_stops_on_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var task = TelegramAssistantWorker.TypingAsync(_bot, 1, cancellation.Token,
            TimeSpan.FromMilliseconds(10));
        await Task.Delay(35); cancellation.Cancel(); await task;
        var count = _bot.TypingCount;
        Assert.InRange(count, 2, 5); Assert.Empty(_bot.Messages);
        await Task.Delay(20); Assert.Equal(count, _bot.TypingCount);
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
        var answer = new AssistantAnswer("Resposta [S1]", [
            new(documentId, "Ata AGO", 3, null, "x", "[F1]"),
            new(documentId, "Ata AGO", 4, null, "y", "[F2]")], "test");
        var formatted = TelegramAssistantWorker.FormatAnswer(answer);
        Assert.Equal("Resposta", formatted); Assert.DoesNotContain("Fontes:", formatted);
        var sources = TelegramAssistantWorker.FormatSources(answer.Sources);
        Assert.Contains("Ata AGO", sources); Assert.Contains("3, 4", sources); Assert.DoesNotContain(documentId.ToString(), sources);
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
    private async Task ProcessContactAsync(long updateId, long telegramId, string phone, long? contactUserId)
    {
        await _host.WithDbAsync(async db =>
        { db.Add(new TelegramInboundUpdate(updateId, telegramId, telegramId, TelegramInboundKind.Contact,
              DateTime.UtcNow, phone, contactUserId)); await db.SaveChangesAsync(); });
        await ProcessPendingAsync();
    }
    private Task AddCodeAsync(Guid userId, string code) => _host.WithDbAsync(async db =>
    { db.Add(new TelegramLinkCode(userId, TelegramAssistantEndpoints.HashCode(code),
          DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10))); await db.SaveChangesAsync(); });
    private Task ProcessPendingAsync() => _host.WithServicesAsync(async services =>
    {
        var worker = ActivatorUtilities.CreateInstance<TelegramAssistantWorker>(services);
        Assert.True(await worker.ProcessOneAsync(default));
    });
    private sealed class FakeTelegramBotClient : ITelegramBotClient
    {
        public List<string> Messages { get; } = [];
        public List<TelegramReplyMarkup> Markups { get; } = [];
        public int TypingCount { get; private set; }
        public int DownloadCount { get; private set; }
        public bool FailSends { get; set; }
        public Exception? Failure { get; set; }
        public Exception? DownloadFailure { get; set; }
        public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
        { if (Failure is not null) throw Failure; if (FailSends) throw new HttpRequestException("failed"); Messages.Add(text); Markups.Add(TelegramReplyMarkup.None); return Task.CompletedTask; }
        public Task SendMessageAsync(long chatId, string text, TelegramReplyMarkup markup, CancellationToken ct)
        { if (Failure is not null) throw Failure; if (FailSends) throw new HttpRequestException("failed"); Messages.Add(text); Markups.Add(markup); return Task.CompletedTask; }
        public Task SendTypingAsync(long chatId, CancellationToken ct) { TypingCount++; return Task.CompletedTask; }
        public Task<byte[]> DownloadFileAsync(string fileId, long maximumBytes, CancellationToken ct)
        { DownloadCount++; return DownloadFailure is null ? Task.FromResult(new byte[] { 1, 2, 3 })
              : Task.FromException<byte[]>(DownloadFailure); }
    }

    private sealed class FakeAudioTranscriptionService : IWhatsAppAudioTranscriptionService
    {
        public AudioTranscriptionResult Result { get; set; } =
            new(true, "Quais documentos estão cadastrados?", "succeeded");
        public int Calls { get; private set; }
        public Task<AudioTranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> audio,
            string fileName, string contentType, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Result); }
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
