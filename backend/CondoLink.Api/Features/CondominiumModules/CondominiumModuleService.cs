using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumModules;

public interface ICondominiumModuleService
{
    Task<bool> IsEnabledAsync(Guid condominiumId, CondominiumModuleType module, CancellationToken ct);
    Task<bool> CanManagementCompanyAccessAsync(Guid condominiumId, CondominiumModuleType module, CancellationToken ct);
    Task<bool> CanManagementCompanyAccessAsync(Guid condominiumId, Guid managementCompanyId,
        CondominiumModuleType module, CancellationToken ct);
    Task<IReadOnlyList<CondominiumModuleState>> GetModulesAsync(Guid condominiumId, CancellationToken ct);
}

public sealed record CondominiumModuleState(CondominiumModuleType Module, bool IsEnabled,
    bool ManagementCompanyAccessEnabled, bool SupportsManagementCompanyAccess);

public sealed class CondominiumModuleService(AppDbContext db) : ICondominiumModuleService
{
    public async Task<bool> IsEnabledAsync(Guid condominiumId, CondominiumModuleType module, CancellationToken ct) =>
        (await GetModulesAsync(condominiumId, ct)).Single(x => x.Module == module).IsEnabled;

    public async Task<bool> CanManagementCompanyAccessAsync(Guid condominiumId, CondominiumModuleType module, CancellationToken ct)
    {
        var state = (await GetModulesAsync(condominiumId, ct)).Single(x => x.Module == module);
        return state.IsEnabled && state.SupportsManagementCompanyAccess && state.ManagementCompanyAccessEnabled;
    }

    public async Task<bool> CanManagementCompanyAccessAsync(Guid condominiumId, Guid managementCompanyId,
        CondominiumModuleType module, CancellationToken ct)
    {
        if (!await CanManagementCompanyAccessAsync(condominiumId, module, ct)) return false;

        // Delegation belongs to the condominium. Resolve the company from the current
        // relationship each time, so a former administrator never retains access.
        return await db.Condominiums.AsNoTracking().AnyAsync(
            x => x.Id == condominiumId && x.ManagementCompanyId == managementCompanyId, ct);
    }

    public async Task<IReadOnlyList<CondominiumModuleState>> GetModulesAsync(Guid condominiumId, CancellationToken ct)
    {
        var rows = await db.CondominiumModules.AsNoTracking().Where(x => x.CondominiumId == condominiumId).ToArrayAsync(ct);
        return CondominiumModuleCatalog.All.Select(definition =>
        {
            var row = rows.SingleOrDefault(x => x.Module == definition.Module);
            // Migration bootstraps rows. This fallback keeps a mixed rollout from hiding current features.
            var enabled = row?.IsEnabled ?? definition.Module != CondominiumModuleType.EmployeeManagement;
            return new CondominiumModuleState(definition.Module, enabled,
                enabled && definition.SupportsManagementCompanyAccess && (row?.ManagementCompanyAccessEnabled ?? false),
                definition.SupportsManagementCompanyAccess);
        }).ToArray();
    }

    public static void AddDefaults(AppDbContext db, Guid condominiumId, DateTime now)
    {
        foreach (var definition in CondominiumModuleCatalog.All)
            db.CondominiumModules.Add(new CondominiumModule(condominiumId, definition.Module,
                definition.Module != CondominiumModuleType.EmployeeManagement, false, now));
    }
}
