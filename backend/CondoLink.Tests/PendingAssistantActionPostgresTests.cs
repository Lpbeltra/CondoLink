using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CondoLink.Tests;

/// <summary>Provider-specific proof for the persisted confirmation claim. Requires COMVY_TEST_POSTGRES.</summary>
public sealed class PendingAssistantActionPostgresTests
{
    [Fact]
    public async Task Migration_applies_to_a_clean_postgres_database_and_creates_the_expected_schema()
    {
        if (!PostgresPendingActionDatabase.IsConfigured) return;
        await using var database = await PostgresPendingActionDatabase.CreateAsync();
        await using var db = database.CreateContext();

        Assert.True(await db.Database.CanConnectAsync());
        var table = await db.Database.SqlQueryRaw<string>("SELECT to_regclass('public.pending_assistant_actions')::text AS \"Value\"").SingleAsync();
        Assert.Equal("pending_assistant_actions", table);
        var columns = await db.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'pending_assistant_actions'").ToArrayAsync();
        Assert.Contains("payload_json", columns);
        Assert.Contains("version", columns);
        var jsonColumns = await db.Database.SqlQueryRaw<int>("SELECT count(*) AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'pending_assistant_actions' AND column_name IN ('payload_json', 'result_json') AND udt_name = 'jsonb'").SingleAsync();
        var requiredColumns = await db.Database.SqlQueryRaw<int>("SELECT count(*) AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'pending_assistant_actions' AND column_name IN ('action_type', 'status', 'actor_user_id', 'condominium_id', 'channel', 'payload_json', 'idempotency_key', 'created_at', 'expires_at', 'version') AND is_nullable = 'NO'").SingleAsync();
        var foreignKeys = await db.Database.SqlQueryRaw<int>("SELECT count(*) AS \"Value\" FROM information_schema.table_constraints WHERE table_schema = 'public' AND table_name = 'pending_assistant_actions' AND constraint_type = 'FOREIGN KEY'").SingleAsync();
        Assert.Equal(2, jsonColumns);
        Assert.Equal(10, requiredColumns);
        Assert.Equal(2, foreignKeys);
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'pending_assistant_actions'").ToArrayAsync();
        Assert.Contains("ux_pending_assistant_actions_idempotency_key", indexes);
        Assert.Contains("ix_pending_assistant_actions_active_context", indexes);
        Assert.Contains("ix_pending_assistant_actions_expiration", indexes);
    }

    [Fact]
    public async Task Concurrent_execute_has_one_claim_one_onboarding_and_a_safe_retry()
    {
        if (!PostgresPendingActionDatabase.IsConfigured) return;
        await using var database = await PostgresPendingActionDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var prepared = await PrepareAsync(database, seed);
        var actionId = prepared.ActionId ?? throw new InvalidOperationException("Prepare must create an action.");
        var results = await RunTogether(
            () => ExecuteAsync(database, actionId, seed),
            () => ExecuteAsync(database, actionId, seed));

        var statuses = new[] { results.First.Status, results.Second.Status };
        Assert.Equal(1, statuses.Count(x => x == "Executed"));
        Assert.Contains(statuses, x => x is "Executing" or "Executed");
        await using var verify = database.CreateContext();
        var action = await verify.PendingAssistantActions.SingleAsync(x => x.Id == actionId);
        Assert.Equal(PendingAssistantActionStatus.Executed, action.Status);
        Assert.Equal(1, await verify.Users.CountAsync(x => x.Email == seed.Email));
        Assert.Equal(1, await verify.CondominiumMemberships.CountAsync(x => x.CondominiumId == seed.CondominiumId && x.UserId != seed.ManagerId));

        var retry = await ExecuteAsync(database, actionId, seed);
        Assert.Equal("Executed", retry.Status);
        await using var afterRetry = database.CreateContext();
        Assert.Equal(1, await afterRetry.Users.CountAsync(x => x.Email == seed.Email));
    }

    [Fact]
    public async Task Concurrent_cancel_and_execute_leave_one_valid_terminal_state()
    {
        if (!PostgresPendingActionDatabase.IsConfigured) return;
        await using var database = await PostgresPendingActionDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var prepared = await PrepareAsync(database, seed);
        var actionId = prepared.ActionId ?? throw new InvalidOperationException("Prepare must create an action.");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execute = Task.Run(async () => { await start.Task; return await ExecuteAsync(database, actionId, seed); });
        var cancel = Task.Run(async () => { await start.Task; return await CancelAsync(database, actionId, seed); });
        start.TrySetResult();
        await Task.WhenAll((Task)execute, cancel).WaitAsync(TimeSpan.FromSeconds(30));

        await using var verify = database.CreateContext();
        var action = await verify.PendingAssistantActions.SingleAsync(x => x.Id == actionId);
        Assert.True(action.Status is PendingAssistantActionStatus.Cancelled or PendingAssistantActionStatus.Executed);
        var created = await verify.Users.CountAsync(x => x.Email == seed.Email);
        Assert.Equal(action.Status == PendingAssistantActionStatus.Executed ? 1 : 0, created);
        Assert.NotNull(await execute);
    }

    private static async Task<Seed> SeedAsync(PostgresPendingActionDatabase database)
    {
        await using var db = database.CreateContext();
        var condominium = new Condominium("Pending race", null, null);
        var manager = CoreTestSeed.User("Pending manager", $"manager-{Guid.NewGuid():N}@test.local");
        var unit = new Unit(condominium.Id, "101", null, null, null);
        db.AddRange(condominium, manager, unit);
        CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
        await db.SaveChangesAsync();
        return new Seed(condominium.Id, manager.Id, unit.Id, $"resident-{Guid.NewGuid():N}@test.local");
    }

    private static Task<AssistantPrepareResult> PrepareAsync(PostgresPendingActionDatabase database, Seed seed) =>
        WithServiceAsync(database, service => service.PrepareAsync(new(seed.ManagerId, seed.CondominiumId,
            CondominiumAssistantChannel.Portal, "postgres-race", Guid.NewGuid().ToString("N"), "Race resident",
            seed.Email, "+5511999990001", "101", null, "Owner", true), default));

    private static Task<AssistantActionExecutionResult> ExecuteAsync(PostgresPendingActionDatabase database, Guid actionId, Seed seed) =>
        WithServiceAsync(database, service => service.ExecuteAsync(actionId, seed.ManagerId, seed.CondominiumId, default));

    private static Task<bool> CancelAsync(PostgresPendingActionDatabase database, Guid actionId, Seed seed) =>
        WithServiceAsync(database, service => service.CancelAsync(actionId, seed.ManagerId, seed.CondominiumId, default));

    private static async Task<T> WithServiceAsync<T>(PostgresPendingActionDatabase database, Func<AssistantResidentRegistrationService, Task<T>> action)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<ResidentOnboardingService>();
        services.AddScoped<AssistantResidentRegistrationService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AssistantResidentRegistrationService>());
    }

    private static async Task<(T First, T Second)> RunTogether<T>(Func<Task<T>> first, Func<Task<T>> second)
    {
        var ready = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<T> Run(Func<Task<T>> operation)
        {
            if (Interlocked.Increment(ref ready) == 2) start.TrySetResult();
            await start.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await operation();
        }
        var tasks = await Task.WhenAll(Run(first), Run(second)).WaitAsync(TimeSpan.FromSeconds(30));
        return (tasks[0], tasks[1]);
    }

    private sealed record Seed(Guid CondominiumId, Guid ManagerId, Guid UnitId, string Email);
}

internal sealed class PostgresPendingActionDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private PostgresPendingActionDatabase(string connectionString, string databaseName) { ConnectionString = connectionString; _databaseName = databaseName; }
    public string ConnectionString { get; }
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES"));

    public static async Task<PostgresPendingActionDatabase> CreateAsync()
    {
        var configured = Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES")!;
        var baseBuilder = new NpgsqlConnectionStringBuilder(configured);
        if (string.IsNullOrWhiteSpace(baseBuilder.Database) || !baseBuilder.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("COMVY_TEST_POSTGRES must target a dedicated database whose name ends in '_test'.");
        var databaseName = $"{baseBuilder.Database}_pending_{Guid.NewGuid():N}";
        var maintenance = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName };
        var database = new PostgresPendingActionDatabase(builder.ConnectionString, databaseName);
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
        return database;
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
