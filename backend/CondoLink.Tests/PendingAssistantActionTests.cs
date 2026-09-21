using System.Text.Json;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Migrations;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>Integration coverage for the persisted confirmation boundary used by the assistant.</summary>
public sealed class PendingAssistantActionTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId, _otherCondominiumId, _managerId, _assistantOnlyId, _unitId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(_ => { });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var other = new Condominium("Residencial Beta", null, null);
            var manager = CoreTestSeed.User("Síndico", "manager@example.com");
            var assistantOnly = CoreTestSeed.User("Assistente", "assistant@example.com");
            var blockA = new CondominiumBlock(condominium.Id, "A");
            var blockB = new CondominiumBlock(condominium.Id, "B");
            var unit = new Unit(condominium.Id, "101", blockA.Id, null, null);
            var duplicate = new Unit(condominium.Id, "101", blockB.Id, null, null);
            var foreign = new Unit(other.Id, "101", null, null, null);
            db.AddRange(condominium, other, manager, assistantOnly, blockA, blockB, unit, duplicate, foreign);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            var assistantMembership = CoreTestSeed.AddMember(db, assistantOnly.Id, condominium.Id, CondominiumRole.SubManager);
            db.SubManagerModulePermissions.Add(new SubManagerModulePermission(assistantMembership.Id, SubManagerModule.Assistant, manager.Id));
            await db.SaveChangesAsync();
            _condominiumId = condominium.Id; _otherCondominiumId = other.Id; _managerId = manager.Id;
            _assistantOnlyId = assistantOnly.Id; _unitId = unit.Id;
        });
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public void Aggregate_allows_only_the_expected_state_transitions()
    {
        var now = DateTime.UtcNow;
        var action = NewAction(now);
        Assert.True(action.TryBeginExecution(now));
        action.Complete(now, "{}");
        Assert.False(action.TryBeginExecution(now));
        Assert.False(action.TryCancel());

        var cancelled = NewAction(now);
        Assert.True(cancelled.TryCancel());
        Assert.False(cancelled.TryCancel()); // the service makes this terminal state idempotent for callers
        Assert.False(cancelled.TryBeginExecution(now));

        var expired = NewAction(now.AddMinutes(-16));
        Assert.True(expired.TryExpire(now));
        Assert.False(expired.TryBeginExecution(now));

        var failed = NewAction(now);
        Assert.True(failed.TryBeginExecution(now));
        failed.Fail(now, "{\"Code\":\"Failure\"}");
        Assert.False(failed.TryBeginExecution(now));
    }

    [Fact]
    public async Task Prepare_persists_a_server_resolved_pending_payload_and_never_onboards()
    {
        var prepared = await PrepareAsync(Block: "A");
        Assert.True(prepared.ReadyToPreview);
        Assert.NotNull(prepared.ActionId);
        Assert.Equal("Maria", prepared.Preview!.FullName);
        Assert.Equal("Bloco A — 101", prepared.Preview.Unit);
        Assert.Equal("+5511999990001", prepared.Preview.PhoneNumber);

        var action = await ActionAsync(prepared.ActionId!.Value);
        Assert.Equal(PendingAssistantActionStatus.Pending, action.Status);
        Assert.Equal(PendingAssistantActionType.ResidentRegistration, action.ActionType);
        Assert.InRange(action.ExpiresAt - action.CreatedAt, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));
        var payload = JsonSerializer.Deserialize<ResidentRegistrationPayload>(action.PayloadJson);
        Assert.NotNull(payload);
        Assert.Equal(_unitId, payload!.UnitId);
        Assert.DoesNotContain("password", action.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", action.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "maria@example.com")));
    }

    [Fact]
    public async Task Prepare_rejects_missing_fields_ambiguous_or_foreign_units_and_assistant_only_access()
    {
        var missing = await PrepareAsync(FullName: null);
        Assert.Contains("FullName", missing.MissingFields!);
        var ambiguous = await PrepareAsync(Block: null);
        Assert.Equal("UnitAmbiguous", ambiguous.Error);
        var foreign = await PrepareAsync(CondominiumId: _otherCondominiumId, Block: null);
        Assert.Equal("Forbidden", foreign.Error);
        var assistantOnly = await PrepareAsync(ActorId: _assistantOnlyId, Block: "A");
        Assert.Equal("Forbidden", assistantOnly.Error);
        Assert.Equal(0, await _host.WithDbAsync(db => db.PendingAssistantActions.CountAsync()));
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Tenant")]
    [InlineData("AuthorizedOccupant")]
    public async Task Prepare_accepts_each_explicit_relationship(string relationship)
    {
        var result = await PrepareAsync(Relationship: relationship, Key: Guid.NewGuid().ToString("N"), Block: "A");
        Assert.True(result.ReadyToPreview);
        Assert.Equal(relationship, result.Preview!.RelationshipType);
    }

    [Fact]
    public async Task New_prepare_replaces_only_its_own_pending_context()
    {
        var first = await PrepareAsync(Block: "A", Key: "first");
        var second = await PrepareAsync(Block: "A", Key: "second");
        Assert.Equal(PendingAssistantActionStatus.Cancelled, (await ActionAsync(first.ActionId!.Value)).Status);
        Assert.Equal(PendingAssistantActionStatus.Pending, (await ActionAsync(second.ActionId!.Value)).Status);
    }

    [Fact]
    public async Task Execute_creates_once_and_returns_the_persisted_terminal_result()
    {
        var prepared = await PrepareAsync(Block: "A");
        var first = await ExecuteAsync(prepared.ActionId!.Value);
        var second = await ExecuteAsync(prepared.ActionId.Value);
        Assert.Equal("Executed", first.Status);
        Assert.Equal("Executed", second.Status);
        var action = await ActionAsync(prepared.ActionId.Value);
        Assert.Equal(PendingAssistantActionStatus.Executed, action.Status);
        Assert.NotNull(action.ExecutedAt);
        Assert.NotNull(action.ResultJson);
        Assert.Equal(1, await _host.WithDbAsync(db => db.CondominiumMemberships.CountAsync(x => x.CondominiumId == _condominiumId && x.UserId != _managerId && x.UserId != _assistantOnlyId)));
    }

    [Fact]
    public async Task Execute_reauthorizes_and_expiry_or_cancel_prevents_onboarding()
    {
        var prepared = await PrepareAsync(Block: "A");
        await _host.WithDbAsync(async db =>
        {
            var membership = await db.CondominiumMemberships.SingleAsync(x => x.UserId == _managerId && x.CondominiumId == _condominiumId);
            membership.Deactivate(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        Assert.Equal("Failed", (await ExecuteAsync(prepared.ActionId!.Value)).Status);
        Assert.Equal(PendingAssistantActionStatus.Failed, (await ActionAsync(prepared.ActionId.Value)).Status);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "maria@example.com")));

        var expired = NewAction(DateTime.UtcNow.AddMinutes(-16));
        await _host.WithDbAsync(async db => { db.PendingAssistantActions.Add(expired); await db.SaveChangesAsync(); });
        Assert.Equal("Expired", (await ExecuteAsync(expired.Id)).Status);
        Assert.Equal(PendingAssistantActionStatus.Expired, (await ActionAsync(expired.Id)).Status);
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_cannot_cancel_an_executed_action()
    {
        var prepared = await PrepareAsync(Block: "A");
        Assert.True(await CancelAsync(prepared.ActionId!.Value));
        Assert.True(await CancelAsync(prepared.ActionId.Value));
        Assert.Equal("Cancelled", (await ExecuteAsync(prepared.ActionId.Value)).Status);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Users.CountAsync(x => x.Email == "maria@example.com")));
    }

    [Fact]
    public async Task Model_exposes_the_migration_and_concurrency_schema_contract()
    {
        await _host.WithDbAsync(db =>
        {
            Assert.Contains("20260918180631_AddPendingAssistantActions", db.Database.GetMigrations());
            var type = db.Model.FindEntityType(typeof(PendingAssistantAction))!;
            Assert.True(type.FindProperty(nameof(PendingAssistantAction.Version))!.IsConcurrencyToken);
            Assert.Contains(type.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(PendingAssistantAction.IdempotencyKey));
            Assert.Contains(type.GetForeignKeys(), x => x.Properties.Single().Name == nameof(PendingAssistantAction.ActorUserId));
            return Task.CompletedTask;
        });
    }

    private PendingAssistantAction NewAction(DateTime created) => new(PendingAssistantActionType.ResidentRegistration, _managerId, _condominiumId, CondominiumAssistantChannel.Portal, "test", "{}", Guid.NewGuid().ToString("N"), created, created.AddMinutes(15));
    private Task<PendingAssistantAction> ActionAsync(Guid id) => _host.WithDbAsync(db => db.PendingAssistantActions.AsNoTracking().SingleAsync(x => x.Id == id));
    private Task<AssistantPrepareResult> PrepareAsync(Guid? ActorId = null, Guid? CondominiumId = null, string? FullName = "Maria", string? Relationship = "Owner", string? Block = "A", string? Key = null) => _host.WithServicesAsync(s => s.GetRequiredService<AssistantResidentRegistrationService>().PrepareAsync(new(ActorId ?? _managerId, CondominiumId ?? _condominiumId, CondominiumAssistantChannel.Portal, "thread-1", Key ?? Guid.NewGuid().ToString("N"), FullName, "maria@example.com", "(11) 99999-0001", "101", Block, Relationship, true), default));
    private Task<AssistantActionExecutionResult> ExecuteAsync(Guid id) => _host.WithServicesAsync(s => s.GetRequiredService<AssistantResidentRegistrationService>().ExecuteAsync(id, _managerId, _condominiumId, default));
    private Task<bool> CancelAsync(Guid id) => _host.WithServicesAsync(s => s.GetRequiredService<AssistantResidentRegistrationService>().CancelAsync(id, _managerId, _condominiumId, default));
}

public sealed class PendingAssistantActionMigrationTests
{
    [Fact]
    public void Migration_creates_only_the_pending_action_table_with_expected_constraints_and_indexes()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildUp(builder);

        var table = Assert.Single(builder.Operations.OfType<CreateTableOperation>());
        Assert.Equal("pending_assistant_actions", table.Name);
        Assert.Equal("jsonb", table.Columns.Single(x => x.Name == "payload_json").ColumnType);
        Assert.False(table.Columns.Single(x => x.Name == "payload_json").IsNullable);
        Assert.True(table.Columns.Single(x => x.Name == "result_json").IsNullable);
        Assert.Equal(2, table.ForeignKeys.Count);
        Assert.All(table.ForeignKeys, fk => Assert.Equal(ReferentialAction.Restrict, fk.OnDelete));

        var indexes = builder.Operations.OfType<CreateIndexOperation>().ToArray();
        Assert.Contains(indexes, x => x.Name == "ux_pending_assistant_actions_idempotency_key" && x.IsUnique);
        Assert.Contains(indexes, x => x.Name == "ix_pending_assistant_actions_active_context" && x.Columns!.Length == 6);
        Assert.Contains(indexes, x => x.Name == "ix_pending_assistant_actions_expiration" && x.Columns!.SequenceEqual(["status", "expires_at"]));
    }

    private sealed class TestableMigration : AddPendingAssistantActions
    {
        public void BuildUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
