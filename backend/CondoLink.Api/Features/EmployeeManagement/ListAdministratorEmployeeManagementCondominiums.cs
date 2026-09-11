using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

/// <summary>
/// The administradora has no existing "select a condominium to operate" context
/// (unlike the síndico's own ActiveManagementCondominiumId, which is a distinct
/// concept scoped to CondominiumMemberships). Rather than build new stateful
/// context for it, this endpoint returns exactly the condominiums the caller may
/// operate Employee Management for; the frontend lets the user pick one from this
/// list and every subsequent request still carries that condominium id explicitly
/// in the URL, re-validated by <see cref="EmployeeManagementAccessService"/>.
/// </summary>
public static class ListAdministratorEmployeeManagementCondominiums
{
    public static IEndpointRouteBuilder MapListAdministratorEmployeeManagementCondominiums(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/administrator/employee-management/condominiums", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("List condominiums the current management company employee may operate Employee Management for");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal, AppDbContext db, ICondominiumModuleService modules, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId)) throw new UnauthorizedAppException("Usuário autenticado inválido.");

        var employee = await db.ManagementCompanyEmployees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct);
        if (employee is null) return Results.Ok(Array.Empty<CondominiumOption>());

        var hasPermission = await db.ManagementCompanyEmployeeModulePermissions.AsNoTracking()
            .AnyAsync(x => x.ManagementCompanyEmployeeId == employee.Id
                && x.Module == CondominiumModuleType.EmployeeManagement
                && x.IsAllowed && x.RevokedAt == null, ct);
        if (!hasPermission) return Results.Ok(Array.Empty<CondominiumOption>());

        var candidates = await db.Condominiums.AsNoTracking()
            .Where(x => x.ManagementCompanyId == employee.ManagementCompanyId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        var results = new List<CondominiumOption>();
        foreach (var candidate in candidates)
        {
            if (await modules.CanManagementCompanyAccessAsync(
                    candidate.Id, employee.ManagementCompanyId, CondominiumModuleType.EmployeeManagement, ct))
                results.Add(new CondominiumOption(candidate.Id, candidate.Name));
        }
        return Results.Ok(results);
    }

    public sealed record CondominiumOption(Guid CondominiumId, string Name);
}
