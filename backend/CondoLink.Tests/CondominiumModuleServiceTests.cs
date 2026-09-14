using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class CondominiumModuleServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Defaults_preserve_current_modules_and_keep_employee_management_disabled()
    {
        var existing = Condominium("Existente");
        var created = Condominium("Novo");
        db.Condominiums.AddRange(existing, created);
        CondominiumModuleService.AddDefaults(db, existing.Id, DateTime.UtcNow);
        CondominiumModuleService.AddDefaults(db, created.Id, DateTime.UtcNow);
        await db.SaveChangesAsync();

        var service = new CondominiumModuleService(db);
        foreach (var condominiumId in new[] { existing.Id, created.Id })
        {
            var modules = await service.GetModulesAsync(condominiumId, CancellationToken.None);
            Assert.Equal(4, modules.Count);
            Assert.All(modules, x => Assert.True(x.IsEnabled));
        }
    }

    [Fact]
    public async Task Unique_condominium_module_constraint_rejects_duplicates()
    {
        var condominium = Condominium("Único");
        db.Condominiums.Add(condominium);
        db.CondominiumModules.Add(new CondominiumModule(condominium.Id, CondominiumModuleType.Assistant, true, false, DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.CondominiumModules.Add(new CondominiumModule(condominium.Id, CondominiumModuleType.Assistant, true, false, DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Employee_management_is_not_controlled_by_condominium_modules()
    {
        var companyX = new ManagementCompany("Administradora X", null, null, null, null);
        var condominiumA = Condominium("A");
        condominiumA.SetManagementCompany(companyX.Id);
        db.AddRange(companyX, condominiumA);
        CondominiumModuleService.AddDefaults(db, condominiumA.Id, DateTime.UtcNow);
        await db.SaveChangesAsync();

        var service = new CondominiumModuleService(db);
        var modules = await service.GetModulesAsync(condominiumA.Id, CancellationToken.None);
        Assert.DoesNotContain(modules, x => x.Module == CondominiumModuleType.EmployeeManagement);
    }

    private static Condominium Condominium(string name) => new(name, null, null);
}
