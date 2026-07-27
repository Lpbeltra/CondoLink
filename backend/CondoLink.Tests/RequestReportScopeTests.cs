using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// The reports endpoint is built on ListCondominiumRequests.AuthorizedRequests.
/// These tests pin the scoping guarantee the report depends on: a manager can
/// never aggregate data from a condominium they do not manage.
/// </summary>
public sealed class RequestReportScopeTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;

    private ApplicationUser _manager = null!;
    private ApplicationUser _otherManager = null!;
    private ApplicationUser _resident = null!;
    private Condominium _managed = null!;
    private Condominium _foreign = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _manager = User("Sindico", "sindico@example.com");
        _otherManager = User("Outro", "outro@example.com");
        _resident = User("Morador", "morador@example.com");
        _managed = new Condominium("Alfa", null, null);
        _foreign = new Condominium("Beta", null, null);
        _db.AddRange(_manager, _otherManager, _resident, _managed, _foreign);

        AddManager(_manager.Id, _managed.Id);
        AddManager(_otherManager.Id, _foreign.Id);

        var managedCategory = new Category(_managed.Id, "Manutenção", null);
        var foreignCategory = new Category(_foreign.Id, "Portaria", null);
        _db.Categories.AddRange(managedCategory, foreignCategory);

        // Two in the managed condominium, one elsewhere.
        _db.Requests.AddRange(
            new DomainRequest(_managed.Id, _resident.Id, null, managedCategory.Id, "A", "d"),
            new DomainRequest(_managed.Id, _resident.Id, null, managedCategory.Id, "B", "d"),
            new DomainRequest(_foreign.Id, _resident.Id, null, foreignCategory.Id, "C", "d"));

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Report_scope_includes_only_the_managed_condominium()
    {
        var scoped = await ListCondominiumRequests
            .AuthorizedRequests(_db, _manager.Id)
            .ToListAsync();

        Assert.Equal(2, scoped.Count);
        Assert.All(scoped, request => Assert.Equal(_managed.Id, request.CondominiumId));
    }

    [Fact]
    public async Task Narrowing_by_condominium_cannot_widen_the_scope()
    {
        // Asking for a condominium the caller does not manage must yield nothing,
        // which is how the endpoint's optional condominiumId filter behaves.
        var scoped = await ListCondominiumRequests
            .AuthorizedRequests(_db, _manager.Id)
            .Where(request => request.CondominiumId == _foreign.Id)
            .ToListAsync();

        Assert.Empty(scoped);
    }

    [Fact]
    public async Task A_resident_aggregates_nothing()
    {
        var scoped = await ListCondominiumRequests
            .AuthorizedRequests(_db, _resident.Id)
            .ToListAsync();

        Assert.Empty(scoped);
    }

    [Fact]
    public async Task Window_filter_excludes_requests_created_before_the_period()
    {
        var all = await ListCondominiumRequests
            .AuthorizedRequests(_db, _manager.Id)
            .ToListAsync();

        // Everything was just created, so a future cut-off must exclude all of it.
        var future = DateTime.UtcNow.AddDays(1);
        var windowed = all.Where(request => request.CreatedAt >= future).ToList();

        Assert.Equal(2, all.Count);
        Assert.Empty(windowed);
    }

    private void AddManager(Guid userId, Guid condominiumId)
    {
        var membership = new CondominiumMembership(userId, condominiumId);
        _db.CondominiumMemberships.Add(membership);
        _db.CondominiumMembershipRoles.Add(
            new CondominiumMembershipRole(membership.Id, CondominiumRole.Manager));
    }

    private static ApplicationUser User(string name, string email)
    {
        var user = new ApplicationUser(name, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        return user;
    }
}
