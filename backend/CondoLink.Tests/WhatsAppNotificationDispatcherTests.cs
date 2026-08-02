using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class WhatsAppNotificationDispatcherTests
{
    [Fact]
    public async Task Enqueued_flow_logs_started_persisting_result_and_finished()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await DispatchAsync(host, requestId, logger);

        AssertLog(logger, "Started", "DispatcherEntered", 0);
        AssertLog(logger, "Persisting", "OutboundMessageCreated", 1);
        AssertLog(logger, "Enqueued", "Eligible", 1);
        AssertLog(logger, "Finished", "Completed", 1);
        Assert.All(logger.Entries, entry =>
            Assert.Equal(WhatsAppNotificationDispatcher.DiagnosticsVersion,
                entry.Value<int>("DiagnosticsVersion")));
    }

    [Fact]
    public async Task Skipped_flow_logs_final_reason_and_created_count()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.PreferenceDisabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await DispatchAsync(host, requestId, logger);

        AssertLog(logger, "Skipped", "UserPreferenceDisabled", 1);
        AssertLog(logger, "Finished", "Completed", 1);
    }

    [Fact]
    public async Task Missing_request_return_is_logged_with_zero_created_messages()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await DispatchAsync(host, Guid.NewGuid(), logger);

        AssertLog(logger, "Skipped", "RequestNotFound", 0);
    }

    [Fact]
    public async Task Duplicate_return_is_logged_with_zero_created_messages()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();
        const string key = "duplicate-diagnostic-key";

        await host.WithDbAsync(async db =>
        {
            var dispatcher = NewDispatcher(db, logger);
            await dispatcher.EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged, key, "safe", null,
                CancellationToken.None);
            await dispatcher.EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged, key, "safe", null,
                CancellationToken.None);
        });

        AssertLog(logger, "Skipped", "DuplicateIdempotencyKey", 0);
    }

    [Fact]
    public async Task Missing_condominium_return_is_logged_with_zero_created_messages()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await host.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var condominiumId = await db.Requests.Where(item => item.Id == requestId)
                .Select(item => item.CondominiumId).SingleAsync();
            await db.Condominiums.Where(item => item.Id == condominiumId)
                .ExecuteDeleteAsync();
            await NewDispatcher(db, logger).EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged, "missing-condominium", "safe",
                null, CancellationToken.None);
        });

        AssertLog(logger, "Skipped", "CondominiumNotFound", 0);
    }

    [Fact]
    public async Task Query_exception_is_logged_with_stage_and_rethrown()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.WithDbAsync(async db =>
        {
            var dispatcher = NewDispatcher(db, logger);
            await db.DisposeAsync();
            await dispatcher.EnqueueAsync(Guid.NewGuid(),
                WhatsAppNotificationType.StatusChanged, "query-failure", "safe", null,
                CancellationToken.None);
        }));

        AssertFailure(logger, "ObjectDisposedException", "loading_request", 0);
    }

    [Fact]
    public async Task Creation_exception_is_logged_and_rethrown_without_sensitive_data()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();
        const string sensitiveContent = "private-message-content";
        const string sensitiveKey = "private-idempotency-key";

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.WithDbAsync(async db =>
        {
            db.ChangeTracker.Tracked += (_, args) =>
            {
                if (args.Entry.Entity is WhatsAppOutboundMessage)
                    throw new InvalidOperationException("creation failed");
            };
            await NewDispatcher(db, logger).EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged, sensitiveKey, sensitiveContent,
                null, CancellationToken.None);
        }));

        AssertFailure(logger, "InvalidOperationException", "creating_outbound", 0);
        Assert.DoesNotContain(logger.Entries,
            entry => entry.Message.Contains(sensitiveContent, StringComparison.Ordinal)
                || entry.Message.Contains(sensitiveKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Save_exception_is_logged_and_rethrown_with_one_created_message()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);
        var logger = new RecordingLogger<WhatsAppNotificationDispatcher>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.WithDbAsync(async db =>
        {
            db.SavingChanges += (_, _) => throw new InvalidOperationException("save failed");
            await NewDispatcher(db, logger).EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged, $"save:{Guid.NewGuid():N}",
                "safe", null, CancellationToken.None);
        }));

        AssertFailure(logger, "InvalidOperationException", "saving_outbound", 1);
    }

    [Fact]
    public async Task Enabled_user_condominium_and_membership_enqueue_message()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);

        await DispatchAsync(host, requestId);

        var outbound = await host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.AsNoTracking().SingleAsync());
        Assert.Equal(WhatsAppOutboundStatus.Pending, outbound.Status);
        Assert.Null(outbound.LastErrorDescription);
    }

    [Theory]
    [InlineData(UserCondition.PreferenceDisabled, "Preferência desabilitada.")]
    [InlineData(UserCondition.MissingPhone, "Telefone inválido.")]
    [InlineData(UserCondition.Inactive, "Usuário inativo.")]
    [InlineData(UserCondition.MissingMembership, "Vínculo inativo.")]
    public async Task User_eligibility_failures_remain_blocked(
        UserCondition condition, string reason)
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, condition);

        await DispatchAsync(host, requestId);

        var outbound = await host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.AsNoTracking().SingleAsync());
        Assert.Equal(WhatsAppOutboundStatus.Skipped, outbound.Status);
        Assert.Equal(reason, outbound.LastErrorDescription);
    }

    private static Task DispatchAsync(CoreEndpointTestHost host, Guid requestId,
        ILogger<WhatsAppNotificationDispatcher>? logger = null) =>
        host.WithDbAsync(async db =>
        {
            var dispatcher = NewDispatcher(db, logger);
            await dispatcher.EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged,
                $"dispatcher-test:{Guid.NewGuid():N}", "status", null,
                CancellationToken.None);
        });

    private static WhatsAppNotificationDispatcher NewDispatcher(
        CondoLink.Infrastructure.Persistence.AppDbContext db,
        ILogger<WhatsAppNotificationDispatcher>? logger = null) =>
        new(db, Options.Create(new WhatsAppOptions { Enabled = true }),
            logger ?? NullLogger<WhatsAppNotificationDispatcher>.Instance);

    private static void AssertLog(RecordingLogger<WhatsAppNotificationDispatcher> logger,
        string decision, string reason, int messagesCreated) =>
        Assert.Contains(logger.Entries, entry =>
            entry.Value<string>("Decision") == decision
            && entry.Value<string>("Reason") == reason
            && entry.Value<int>("MessagesCreated") == messagesCreated);

    private static void AssertFailure(
        RecordingLogger<WhatsAppNotificationDispatcher> logger,
        string exceptionType, string stage, int messagesCreated) =>
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is not null
            && entry.Value<string>("Decision") == "Failed"
            && entry.Value<string>("Reason") == "DispatcherException"
            && entry.Value<string>("ExceptionType") == exceptionType
            && entry.Value<string>("Stage") == stage
            && entry.Value<int>("MessagesCreated") == messagesCreated);

    private static Task<Guid> SeedAsync(CoreEndpointTestHost host,
        UserCondition condition) => host.WithDbAsync(async db =>
    {
        var condominium = new Condominium("Residencial", null, null);
        var category = new Category(condominium.Id, "Outros", null);
        var phone = condition == UserCondition.MissingPhone
            ? null : "(11) 99999-0001";
        var user = new ApplicationUser("Morador", $"{Guid.NewGuid():N}@example.com", phone);
        user.NormalizedUserName = user.Email!.ToUpperInvariant();
        user.NormalizedEmail = user.NormalizedUserName;
        if (condition == UserCondition.PreferenceDisabled)
            user.SetReceiveWhatsAppUpdates(false);
        if (condition == UserCondition.Inactive) user.SetActiveStatus(false);
        var request = new CondoLink.Domain.Entities.Request(condominium.Id,
            user.Id, null, category.Id, "Solicitação", "Descrição");
        db.AddRange(condominium, category, user, request);
        if (condition == UserCondition.Enabled)
            db.Add(new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}",
                user.NormalizedPhoneNumber!, "text", "menu", DateTime.UtcNow));
        if (condition != UserCondition.MissingMembership)
            db.Add(new CondominiumMembership(user.Id, condominium.Id));
        await db.SaveChangesAsync();
        return request.Id;
    });

    public enum UserCondition
    {
        Enabled,
        PreferenceDisabled,
        MissingPhone,
        Inactive,
        MissingMembership
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception,
                values.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message,
        Exception? Exception, IReadOnlyDictionary<string, object?> Values)
    {
        public TValue? Value<TValue>(string key) =>
            Values.TryGetValue(key, out var value) ? (TValue?)value : default;
    }
}
