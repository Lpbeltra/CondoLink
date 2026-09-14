using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class ManagementCompanyEmployeeModulePermissionEndpoints
{
    private const string BasePath = "/overwatch/management-companies/employees/{employeeId:guid}/module-permissions";

    public static IEndpointRouteBuilder MapManagementCompanyEmployeeModulePermissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(BasePath, ListAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapPut(BasePath + "/{condominiumId:guid}", UpdateAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid employeeId, AppDbContext db, CancellationToken ct)
    {
        var employee = await db.ManagementCompanyEmployees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null) return Results.NotFound();

        var condominiums = await db.Condominiums.AsNoTracking()
            .Where(x => x.ManagementCompanyId == employee.ManagementCompanyId)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToArrayAsync(ct);
        var ids = condominiums.Select(x => x.Id).ToArray();
        var modules = await db.CondominiumModules.AsNoTracking()
            .Where(x => ids.Contains(x.CondominiumId) && x.Module == CondominiumModuleType.EmployeeManagement)
            .ToArrayAsync(ct);
        var allowedIds = await db.ManagementCompanyEmployeeModulePermissions.AsNoTracking()
            .Where(x => x.ManagementCompanyEmployeeId == employeeId
                && x.CondominiumId.HasValue && ids.Contains(x.CondominiumId.Value)
                && x.Module == CondominiumModuleType.EmployeeManagement && x.IsAllowed && x.RevokedAt == null)
            .Select(x => x.CondominiumId!.Value).ToArrayAsync(ct);
        var granted = allowedIds.ToHashSet();
        var rows = condominiums.Select(c =>
        {
            var module = modules.SingleOrDefault(m => m.CondominiumId == c.Id);
            var eligible = employee.IsActive && module is { IsEnabled: true, ManagementCompanyAccessEnabled: true };
            return new PermissionRow(c.Id, c.Name, eligible, eligible && granted.Contains(c.Id));
        }).ToArray();
        return Results.Ok(rows);
    }

    private static async Task<IResult> UpdateAsync(Guid employeeId, Guid condominiumId, Request request,
        ClaimsPrincipal principal, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var employee = await db.ManagementCompanyEmployees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null) return Results.NotFound();
        var linked = await db.Condominiums.AsNoTracking().AnyAsync(x =>
            x.Id == condominiumId && x.ManagementCompanyId == employee.ManagementCompanyId, ct);
        if (!linked) return Results.Forbid();
        if (request.Allowed)
        {
            var entitled = employee.IsActive && await db.CondominiumModules.AsNoTracking().AnyAsync(x =>
                x.CondominiumId == condominiumId && x.Module == CondominiumModuleType.EmployeeManagement
                && x.IsEnabled && x.ManagementCompanyAccessEnabled, ct);
            if (!entitled) return Results.Forbid();
        }

        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var actorId)) return Results.Unauthorized();

        var permission = await db.ManagementCompanyEmployeeModulePermissions.SingleOrDefaultAsync(x =>
            x.ManagementCompanyEmployeeId == employeeId && x.CondominiumId == condominiumId
            && x.Module == CondominiumModuleType.EmployeeManagement, ct);
        if (permission is null && request.Allowed)
        {
            permission = new ManagementCompanyEmployeeModulePermission(employeeId, condominiumId,
                CondominiumModuleType.EmployeeManagement, actorId);
            db.Add(permission);
        }
        else permission?.SetAllowed(request.Allowed, actorId);
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("ManagementCompanyEmployeeModulePermissionAudit").LogInformation(
            "Employee Management permission updated. EmployeeId: {EmployeeId}; CondominiumId: {CondominiumId}; Allowed: {Allowed}; ActorUserId: {ActorUserId}",
            employeeId, condominiumId, request.Allowed, actorId);
        return Results.NoContent();
    }

    public sealed record PermissionRow(Guid CondominiumId, string CondominiumName, bool Eligible, bool Allowed);
    public sealed record Request(bool Allowed);
}
