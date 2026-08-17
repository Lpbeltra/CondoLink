using System.Security.Claims;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CondoLink.Tests;

public sealed class CondominiumDocumentDeleteTests
{
    [Theory]
    [InlineData(CondominiumDocumentProcessingStatus.Ready)]
    [InlineData(CondominiumDocumentProcessingStatus.Failed)]
    [InlineData(CondominiumDocumentProcessingStatus.Unsupported)]
    public async Task Authorized_manager_deletes_document_and_chunks(CondominiumDocumentProcessingStatus status)
    {
        await using var scope = await Scope.Create(status);

        var result = await scope.Delete(scope.Condominium.Id, scope.Document.Id);

        Assert.Equal(StatusCodes.Status204NoContent, Status(result));
        Assert.False(await scope.Db.CondominiumDocuments.AnyAsync());
        Assert.False(await scope.Db.CondominiumDocumentChunks.AnyAsync());
        Assert.Equal((scope.Condominium.Id, scope.Document.Id, scope.Document.StorageKey), scope.Storage.Deleted);
    }

    [Fact]
    public async Task Manager_cannot_delete_document_from_another_condominium()
    {
        await using var scope = await Scope.Create();
        var result = await scope.Delete(Guid.NewGuid(), scope.Document.Id);
        Assert.IsType<ForbidHttpResult>(result);
        Assert.True(await scope.Db.CondominiumDocuments.AnyAsync());
    }

    [Fact]
    public async Task User_without_manager_role_is_forbidden()
    {
        await using var scope = await Scope.Create(addManager: false);
        var result = await scope.Delete(scope.Condominium.Id, scope.Document.Id);
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Platform_admin_can_delete_without_condominium_membership()
    {
        await using var scope = await Scope.Create(addManager: false);
        var result = await scope.Delete(scope.Condominium.Id, scope.Document.Id, platformAdmin: true);
        Assert.Equal(StatusCodes.Status204NoContent, Status(result));
    }

    [Fact]
    public async Task Missing_document_returns_not_found()
    {
        await using var scope = await Scope.Create();
        var result = await scope.Delete(scope.Condominium.Id, Guid.NewGuid());
        Assert.Equal(StatusCodes.Status404NotFound, Status(result));
    }

    [Fact]
    public async Task Processing_document_cannot_be_deleted()
    {
        await using var scope = await Scope.Create(CondominiumDocumentProcessingStatus.Processing);
        var result = await scope.Delete(scope.Condominium.Id, scope.Document.Id);
        Assert.Equal(StatusCodes.Status409Conflict, Status(result));
        Assert.True(await scope.Db.CondominiumDocuments.AnyAsync());
    }

    [Fact]
    public async Task Storage_failure_rolls_back_database_delete()
    {
        await using var scope = await Scope.Create(); scope.Storage.Throw = true;
        var result = await scope.Delete(scope.Condominium.Id, scope.Document.Id);
        scope.Db.ChangeTracker.Clear();
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Status(result));
        Assert.True(await scope.Db.CondominiumDocuments.AnyAsync());
        Assert.True(await scope.Db.CondominiumDocumentChunks.AnyAsync());
    }

    private static int? Status(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private sealed class FakeStorage : ICondominiumDocumentStorage
    {
        public bool Throw { get; set; }
        public (Guid, Guid, string)? Deleted { get; private set; }
        public void DeleteCondominiumDocument(Guid condominiumId, Guid documentId, string storageKey)
        {
            if (Throw) throw new IOException("disk failure");
            Deleted = (condominiumId, documentId, storageKey);
        }
    }

    private sealed class Scope : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public Condominium Condominium { get; }
        public CondominiumDocument Document { get; }
        public FakeStorage Storage { get; } = new();
        private readonly Guid userId;

        private Scope(SqliteConnection connection, AppDbContext db, Condominium condominium,
            CondominiumDocument document, Guid userId)
        { this.connection = connection; Db = db; Condominium = condominium; Document = document; this.userId = userId; }

        public static async Task<Scope> Create(CondominiumDocumentProcessingStatus status = CondominiumDocumentProcessingStatus.Ready,
            bool addManager = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var user = CoreTestSeed.User("Manager", $"manager-{Guid.NewGuid():N}@test.local");
            var condominium = new Condominium("Condomínio", null, null); db.AddRange(user, condominium);
            if (addManager) CoreTestSeed.AddMember(db, user.Id, condominium.Id, CondominiumRole.Manager);
            var document = new CondominiumDocument(condominium.Id, "Convenção", CondominiumDocumentType.Convention,
                "rules.pdf", $"condominium-documents/{condominium.Id}/pending/original.pdf", "application/pdf", 1, null, user.Id);
            if (status == CondominiumDocumentProcessingStatus.Processing) document.Processing();
            else if (status == CondominiumDocumentProcessingStatus.Ready) document.Ready();
            else document.Fail("erro", status == CondominiumDocumentProcessingStatus.Unsupported);
            typeof(CondominiumDocument).GetProperty(nameof(CondominiumDocument.StorageKey))!.SetValue(document,
                $"condominium-documents/{condominium.Id}/{document.Id}/original.pdf");
            db.Add(document); db.Add(new CondominiumDocumentChunk(document.Id, condominium.Id, 0, "regra", "[1]", 1, null));
            await db.SaveChangesAsync(); return new(connection, db, condominium, document, user.Id);
        }

        public Task<IResult> Delete(Guid condominiumId, Guid documentId, bool platformAdmin = false) =>
            CondominiumAssistantEndpoints.DeleteDocument(condominiumId, documentId,
                new ClaimsPrincipal(new ClaimsIdentity(
                    platformAdmin
                        ? [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, CondoLink.Infrastructure.DependencyInjection.PlatformAdminRole)]
                        : [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")),
                Db, Storage, NullLogger<CondominiumDocumentProcessor>.Instance, default);

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
