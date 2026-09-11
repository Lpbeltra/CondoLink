using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class CondominiumModuleEndpoints
{
    public static IEndpointRouteBuilder MapCondominiumModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/condominiums/{condominiumId:guid}/modules", GetAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapPut("/overwatch/condominiums/{condominiumId:guid}/modules", PutAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }
    private static async Task<IResult> GetAsync(Guid condominiumId, AppDbContext db,
        ICondominiumModuleService modules, CancellationToken ct)
    {
        if (!await db.Condominiums.AnyAsync(x => x.Id == condominiumId, ct)) return Results.NotFound();
        return Results.Ok((await modules.GetModulesAsync(condominiumId, ct)).Select(x => new
        { module = x.Module.ToString(), enabled = x.IsEnabled, managementCompanyAccessEnabled = x.ManagementCompanyAccessEnabled, x.SupportsManagementCompanyAccess }));
    }
    private static async Task<IResult> PutAsync(Guid condominiumId, UpdateModulesRequest request,
        AppDbContext db, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!await db.Condominiums.AnyAsync(x => x.Id == condominiumId, ct)) return Results.NotFound();
        if (request.Modules.Any(x => !Enum.TryParse<CondominiumModuleType>(x.Module, true, out _)))
            return Results.BadRequest(new { message = "Unknown module." });
        var now = DateTime.UtcNow;
        var logger = loggerFactory.CreateLogger("CondominiumModuleAudit");
        var rows = await db.CondominiumModules.Where(x => x.CondominiumId == condominiumId).ToListAsync(ct);
        foreach (var update in request.Modules)
        {
            var type = Enum.Parse<CondominiumModuleType>(update.Module, true);
            var row = rows.SingleOrDefault(x => x.Module == type);
            var previousEnabled = row?.IsEnabled ?? type != CondominiumModuleType.EmployeeManagement;
            var previousDelegation = row?.ManagementCompanyAccessEnabled ?? false;
            if (row is null) { row = new CondominiumModule(condominiumId, type, update.Enabled, false, now); db.CondominiumModules.Add(row); }
            row.Set(update.Enabled, CondominiumModuleCatalog.SupportsManagementCompanyAccess(type) && update.ManagementCompanyAccessEnabled, now);
            logger.LogInformation("Condominium module changed. CondominiumId: {CondominiumId}; Module: {Module}; IsEnabledBefore: {IsEnabledBefore}; IsEnabledAfter: {IsEnabledAfter}; ManagementCompanyAccessBefore: {ManagementCompanyAccessBefore}; ManagementCompanyAccessAfter: {ManagementCompanyAccessAfter}; PlatformAdmin: {PlatformAdmin}; Timestamp: {Timestamp}", condominiumId, type, previousEnabled, row.IsEnabled, previousDelegation, row.ManagementCompanyAccessEnabled, context.User.Identity?.Name ?? "unknown", now);
        }
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    public sealed record ModuleUpdate(string Module, bool Enabled, bool ManagementCompanyAccessEnabled);
    public sealed record UpdateModulesRequest(IReadOnlyList<ModuleUpdate> Modules);
}
