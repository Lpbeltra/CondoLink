using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class ManagementCompanyEmployeeModuleGrantEndpoints
{
    private const string Path = "/overwatch/management-companies/employees/{employeeId:guid}/employee-management-grant";

    public static IEndpointRouteBuilder MapManagementCompanyEmployeeModuleGrantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, GetAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapPut(Path, SetAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(Guid employeeId, AppDbContext db, CancellationToken ct)
    {
        var employee = await db.ManagementCompanyEmployees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null) return Results.NotFound();
        var moduleEnabled = await db.ManagementCompanyModules.AsNoTracking().AnyAsync(x =>
            x.ManagementCompanyId == employee.ManagementCompanyId
            && x.Module == ManagementCompanyModuleType.EmployeeManagement && x.IsEnabled, ct);
        var allowed = await db.ManagementCompanyEmployeeModuleGrants.AsNoTracking().AnyAsync(x =>
            x.ManagementCompanyEmployeeId == employeeId && x.Module == ManagementCompanyModuleType.EmployeeManagement
            && x.IsAllowed && x.RevokedAt == null, ct);
        return Results.Ok(new EmployeeManagementGrantResponse(employee.IsActive && moduleEnabled, allowed));
    }

    private static async Task<IResult> SetAsync(Guid employeeId, EmployeeManagementGrantRequest request,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var employee = await db.ManagementCompanyEmployees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null) return Results.NotFound();
        var actor = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(actor, out var actorId)) return Results.Unauthorized();
        var moduleEnabled = await db.ManagementCompanyModules.AsNoTracking().AnyAsync(x =>
            x.ManagementCompanyId == employee.ManagementCompanyId
            && x.Module == ManagementCompanyModuleType.EmployeeManagement && x.IsEnabled, ct);
        if (request.Allowed && (!employee.IsActive || !moduleEnabled)) return Results.StatusCode(403);

        var grant = await db.ManagementCompanyEmployeeModuleGrants.SingleOrDefaultAsync(x =>
            x.ManagementCompanyEmployeeId == employeeId && x.Module == ManagementCompanyModuleType.EmployeeManagement, ct);
        if (grant is null)
        {
            if (request.Allowed)
                db.ManagementCompanyEmployeeModuleGrants.Add(
                    new ManagementCompanyEmployeeModuleGrant(employeeId, ManagementCompanyModuleType.EmployeeManagement, actorId, DateTime.UtcNow));
        }
        else grant.SetAllowed(request.Allowed, actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public sealed record EmployeeManagementGrantResponse(bool Eligible, bool Allowed);
    public sealed record EmployeeManagementGrantRequest(bool Allowed);
}
