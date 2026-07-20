using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class ManagementMultiCondominiumTests
{
    [Fact]
    public async Task Manager_receives_each_request_once_from_all_and_only_managed_condominiums()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var manager = User("Manager", "manager@example.com");
        var outsider = User("Outsider", "outsider@example.com");
        var author = User("Resident", "resident@example.com");
        var first = new Condominium("Alfa", null, null);
        var second = new Condominium("Beta", null, null);
        var forbidden = new Condominium("Gama", null, null);
        db.Users.AddRange(manager, outsider, author);
        db.Condominiums.AddRange(first, second, forbidden);

        AddManager(db, manager.Id, first.Id);
        AddManager(db, manager.Id, second.Id);
        AddManager(db, outsider.Id, forbidden.Id);
        var firstCategory = new Category(first.Id, "Manutenção", null);
        var secondCategory = new Category(second.Id, "Portaria", null);
        var forbiddenCategory = new Category(forbidden.Id, "Interno", null);
        db.Categories.AddRange(firstCategory, secondCategory, forbiddenCategory);
        db.Requests.AddRange(
            new DomainRequest(first.Id, author.Id, null, firstCategory.Id, "Primeira", "Descrição"),
            new DomainRequest(second.Id, author.Id, null, secondCategory.Id, "Segunda", "Descrição"),
            new DomainRequest(forbidden.Id, author.Id, null, forbiddenCategory.Id, "Proibida", "Descrição"));
        await db.SaveChangesAsync();

        var visible = await ListCondominiumRequests.AuthorizedRequests(db, manager.Id).ToListAsync();
        Assert.Equal(2, visible.Count);
        Assert.Equal(2, visible.Select(item => item.Id).Distinct().Count());
        Assert.Contains(visible, item => item.CondominiumId == first.Id);
        Assert.Contains(visible, item => item.CondominiumId == second.Id);
        Assert.DoesNotContain(visible, item => item.CondominiumId == forbidden.Id);
        Assert.Empty(await ListCondominiumRequests.AuthorizedRequests(db, author.Id).ToListAsync());
    }

    [Fact]
    public async Task Request_keeps_the_unit_and_condominium_captured_when_it_was_opened()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var author = User("Resident", "unit@example.com");
        var condominium = new Condominium("Alfa", null, null);
        var unit = new Unit(condominium.Id, "101", null, null, null);
        var category = new Category(condominium.Id, "Manutenção", null);
        var request = new DomainRequest(condominium.Id, author.Id, unit.Id, category.Id, "Vazamento", "Descrição");
        var unitMembership = new UnitMembership(author.Id, unit.Id, UnitRelationshipType.Owner, true, true);
        db.AddRange(author, condominium, unit, category, request, unitMembership);
        await db.SaveChangesAsync();

        var inferredUnits = await CreateRequest.ActiveUnitIdsAsync(db, author.Id, condominium.Id);
        var saved = await db.Requests.SingleAsync();
        Assert.Equal([unit.Id], inferredUnits);
        Assert.Equal(unit.Id, saved.TargetUnitId);
        Assert.Equal(condominium.Id, saved.CondominiumId);
    }

    private static void AddManager(AppDbContext db, Guid userId, Guid condominiumId)
    {
        var membership = new CondominiumMembership(userId, condominiumId);
        db.CondominiumMemberships.Add(membership);
        db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Manager));
        db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));
    }

    private static ApplicationUser User(string name, string email)
    {
        var user = new ApplicationUser(name, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        return user;
    }
}
