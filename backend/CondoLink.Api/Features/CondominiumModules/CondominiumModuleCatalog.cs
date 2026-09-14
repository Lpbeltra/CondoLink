using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.CondominiumModules;

public sealed record CondominiumModuleDefinition(CondominiumModuleType Module, bool SupportsManagementCompanyAccess);

public static class CondominiumModuleCatalog
{
    public static readonly IReadOnlyList<CondominiumModuleDefinition> All =
    [
        new(CondominiumModuleType.Assistant, false),
        new(CondominiumModuleType.Documents, false),
        new(CondominiumModuleType.Providers, false),
        new(CondominiumModuleType.ManagementCompanyRequests, false)
    ];
    public static bool SupportsManagementCompanyAccess(CondominiumModuleType module) =>
        All.SingleOrDefault(x => x.Module == module)?.SupportsManagementCompanyAccess ?? false;
}
