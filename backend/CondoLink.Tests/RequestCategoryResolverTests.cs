using CondoLink.Api.Features.Categories;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class RequestCategoryResolverTests
{
    [Fact]
    public async Task Concurrent_fallback_resolution_creates_only_one_others_category()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var condominium = new Condominium("Concorrência", null, null);
        db.Condominiums.Add(condominium);
        await db.SaveChangesAsync();
        var resolver = new RequestCategoryResolver(db);

        var categories = await Task.WhenAll(
            resolver.GetOrCreateOtherAsync(condominium.Id, default),
            resolver.GetOrCreateOtherAsync(condominium.Id, default));

        Assert.Equal(categories[0].Id, categories[1].Id);
        Assert.Equal(1, await db.Categories.CountAsync(category =>
            category.CondominiumId == condominium.Id
            && category.NormalizedName == "OUTROS"));
    }

    [Fact]
    public async Task Valid_active_suggestion_is_preserved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var condominium = new Condominium("Correspondência", null, null);
        var category = new Category(condominium.Id, "Hidráulica", null);
        db.AddRange(condominium, category);
        await db.SaveChangesAsync();
        var resolver = new RequestCategoryResolver(db);

        var resolved = await resolver.ResolveForClassificationAsync(
            condominium.Id, "hidráulica", default);

        Assert.Equal(category.Id, resolved.Id);
        Assert.DoesNotContain(await db.Categories.ToArrayAsync(), item =>
            item.NormalizedName == "OUTROS");
    }
}
