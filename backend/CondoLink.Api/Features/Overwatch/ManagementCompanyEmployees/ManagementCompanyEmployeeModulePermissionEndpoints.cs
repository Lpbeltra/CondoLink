using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

/// <summary>
/// Grants/revokes a management company employee's (or department's) access to
/// delegable modules — e.g. "Departamento Pessoal" gets EmployeeManagement,
/// "Jurídico" does not — independent of "employee is active" or "company is
/// linked". Platform-admin only, mirroring SubManagerEndpoints' permission shape.
/// </summary>
public static class ManagementCompanyEmployeeModulePermissionEndpoints
{
    public static IEndpointRouteBuilder MapManagementCompanyEmployeeModulePermissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/management-companies/employees/{employeeId:guid}/module-permissions", ListAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("List management company employee module permissions");
        endpoints.MapPut("/overwatch/management-companies/employees/{employeeId:guid}/module-permissions", UpdateAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Update management company employee module permissions");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid employeeId, AppDbContext db, CancellationToken ct)
    {
        var exists = await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == employeeId, ct);
        if (!exists) return Results.NotFound(new { message = "Management company employee not found." });

        var granted = await db.ManagementCompanyEmployeeModulePermissions.AsNoTracking()
            .Where(x => x.ManagementCompanyEmployeeId == employeeId && x.IsAllowed && x.RevokedAt == null)
            .Select(x => x.Module).ToListAsync(ct);
        var rows = CondominiumModuleCatalog.All.Where(x => x.SupportsManagementCompanyAccess)
            .Select(x => new { module = x.Module.ToString(), allowed = granted.Contains(x.Module) });
        return Results.Ok(rows);
    }

    private static async Task<IResult> UpdateAsync(Guid employeeId, Request request,
        ClaimsPrincipal principal, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var employeeExists = await db.ManagementCompanyEmployees.AnyAsync(x => x.Id == employeeId, ct);
        if (!employeeExists) return Results.NotFound(new { message = "Management company employee not found." });

        var delegable = CondominiumModuleCatalog.All.Where(x => x.SupportsManagementCompanyAccess)
            .Select(x => x.Module).ToHashSet();
        if (request.Permissions is null || request.Permissions.Count != delegable.Count
            || request.Permissions.Any(x => !Enum.TryParse<CondominiumModuleType>(x.Module, true, out var module)
                || !delegable.Contains(module)))
            return Results.BadRequest(new { message = "Informe exatamente uma permissão por módulo delegável." });

        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var actorId)) return Results.Unauthorized();

        foreach (var item in request.Permissions)
        {
            var module = Enum.Parse<CondominiumModuleType>(item.Module, true);
            var permission = await db.ManagementCompanyEmployeeModulePermissions.SingleOrDefaultAsync(
                x => x.ManagementCompanyEmployeeId == employeeId && x.Module == module, ct);
            if (permission is null)
            {
                permission = new ManagementCompanyEmployeeModulePermission(employeeId, module, actorId);
                db.Add(permission);
            }
            permission.SetAllowed(item.Allowed, actorId);
        }
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("ManagementCompanyEmployeeModulePermissionAudit").LogInformation(
            "Management company employee module permissions updated. EmployeeId: {EmployeeId}; Modules: {Modules}; ActorUserId: {ActorUserId}",
            employeeId, string.Join(',', request.Permissions.Select(x => x.Module)), actorId);
        return Results.NoContent();
    }

    public sealed record PermissionItem(string Module, bool Allowed);
    public sealed record Request(IReadOnlyList<PermissionItem>? Permissions);
}
