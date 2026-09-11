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
            Assert.Equal(5, modules.Count);
            Assert.All(modules.Where(x => x.Module != CondominiumModuleType.EmployeeManagement), x => Assert.True(x.IsEnabled));
            Assert.False(modules.Single(x => x.Module == CondominiumModuleType.EmployeeManagement).IsEnabled);
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
    public async Task Delegation_is_scoped_to_condominium_and_current_management_company()
    {
        var companyX = new ManagementCompany("Administradora X", null, null, null, null);
        var companyY = new ManagementCompany("Administradora Y", null, null, null, null);
        var condominiumA = Condominium("A");
        var condominiumB = Condominium("B");
        condominiumA.SetManagementCompany(companyX.Id);
        condominiumB.SetManagementCompany(companyX.Id);
        db.AddRange(companyX, companyY, condominiumA, condominiumB);
        CondominiumModuleService.AddDefaults(db, condominiumA.Id, DateTime.UtcNow);
        CondominiumModuleService.AddDefaults(db, condominiumB.Id, DateTime.UtcNow);
        var employeeA = db.CondominiumModules.Local.Single(x => x.CondominiumId == condominiumA.Id && x.Module == CondominiumModuleType.EmployeeManagement);
        employeeA.Set(true, true, DateTime.UtcNow);
        await db.SaveChangesAsync();

        var service = new CondominiumModuleService(db);
        Assert.True(await service.CanManagementCompanyAccessAsync(condominiumA.Id, companyX.Id, CondominiumModuleType.EmployeeManagement, CancellationToken.None));
        Assert.False(await service.CanManagementCompanyAccessAsync(condominiumB.Id, companyX.Id, CondominiumModuleType.EmployeeManagement, CancellationToken.None));

        condominiumA.SetManagementCompany(companyY.Id);
        await db.SaveChangesAsync();
        Assert.False(await service.CanManagementCompanyAccessAsync(condominiumA.Id, companyX.Id, CondominiumModuleType.EmployeeManagement, CancellationToken.None));
        Assert.True(await service.CanManagementCompanyAccessAsync(condominiumA.Id, companyY.Id, CondominiumModuleType.EmployeeManagement, CancellationToken.None));
    }

    private static Condominium Condominium(string name) => new(name, null, null);
}
