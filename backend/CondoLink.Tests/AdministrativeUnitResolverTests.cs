using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class AdministrativeUnitResolverTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private AdministrativeUnitResolver _resolver = null!;
    private Guid _condominiumId;
    private Guid _unit1201Id;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        var condominium = new Condominium("Teste", null, null);
        var block1 = new CondominiumBlock(condominium.Id, "Bloco 1");
        var block2 = new CondominiumBlock(condominium.Id, "Bloco 2");
        var unit1201 = new Unit(condominium.Id, "1201", block1.Id, null, null);
        _db.AddRange(condominium, block1, block2, unit1201,
            new Unit(condominium.Id, "1201", block2.Id, null, null),
            new Unit(condominium.Id, "101A", block1.Id, null, null),
            new Unit(condominium.Id, "01", block1.Id, null, null));
        await _db.SaveChangesAsync();
        _condominiumId = condominium.Id;
        _unit1201Id = unit1201.Id;
        _resolver = new AdministrativeUnitResolver(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData("1201/1", null)]
    [InlineData("1201", "1")]
    [InlineData("1201 bloco 1", null)]
    [InlineData("bloco 1 apto 1201", null)]
    [InlineData("apartamento 1201 do bloco 1", null)]
    [InlineData("unidade 1201 bloco 1", null)]
    public async Task Equivalent_forms_resolve_the_same_unit(string unit, string? block)
    {
        var result = await _resolver.ResolveAsync(
            _condominiumId, null, unit, block, default);

        Assert.Equal(_unit1201Id, Assert.Single(result).Id);
    }

    [Theory]
    [InlineData("101A")]
    [InlineData("01")]
    public async Task Significant_unit_identifiers_are_preserved(string identifier)
    {
        var result = await _resolver.ResolveAsync(
            _condominiumId, null, identifier, "Bloco 1", default);

        Assert.Single(result);
        Assert.EndsWith(identifier, result[0].Display);
    }

    [Fact]
    public async Task Unit_without_block_is_ambiguous_across_blocks()
    {
        var result = await _resolver.ResolveAsync(
            _condominiumId, null, "1201", null, default);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, x => x.Display == "Bloco 1 - 1201");
        Assert.Contains(result, x => x.Display == "Bloco 2 - 1201");
    }

    [Fact]
    public async Task Unknown_unit_returns_no_match()
    {
        Assert.Empty(await _resolver.ResolveAsync(
            _condominiumId, null, "9999", "1", default));
    }
}
