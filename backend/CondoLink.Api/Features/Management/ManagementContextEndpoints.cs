using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Domain.Enums;
using CondoLink.Api.Features.CondominiumModules;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Management;

public static class ManagementContextEndpoints
{
    public static IEndpointRouteBuilder MapManagementContext(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/management/context", HandleGetAsync)
            .RequireAuthorization();
        endpoints.MapPut("/management/context", HandlePutAsync)
            .RequireAuthorization();
        endpoints.MapGet("/management/modules", HandleModulesAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleGetAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(principal, dbContext, cancellationToken);
        if (user is null)
        {
            return AuthenticationFailed();
        }

        var context = await ManagementContextReconciler.ReconcileAsync(
            user, dbContext, cancellationToken);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(await WithAdministratorEligibility(context, dbContext, principal, cancellationToken));
    }

    private static async Task<IResult> HandlePutAsync(
        ManagementContextRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(principal, dbContext, cancellationToken);
        if (user is null)
        {
            return AuthenticationFailed();
        }

        var available = await ManagementContextReconciler
            .GetAvailableCondominiumsAsync(
                user.Id, dbContext, cancellationToken);
        if (request.CondominiumId is Guid requestedId
            && requestedId != Guid.Empty
            && !available.Any(item => item.Id == requestedId))
        {
            return Results.Forbid();
        }

        var context = await ManagementContextReconciler.SelectAsync(
            user, request.CondominiumId, dbContext, cancellationToken);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(await WithAdministratorEligibility(context, dbContext, principal, cancellationToken));
    }

    private static async Task<ApplicationUser?> GetActiveUserAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            return null;
        }

        return await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.IsActive,
            cancellationToken);
    }

    private static IResult AuthenticationFailed() =>
        Results.Json(
            new { error = "Authenticated user was not found or is inactive." },
            statusCode: StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleModulesAsync(ClaimsPrincipal principal, AppDbContext dbContext,
        ICondominiumModuleService modules, CancellationToken ct)
    {
        var user = await GetActiveUserAsync(principal, dbContext, ct);
        if (user is null) return AuthenticationFailed();
        var context = await ManagementContextReconciler.ReconcileAsync(user, dbContext, ct);
        if (dbContext.ChangeTracker.HasChanges()) await dbContext.SaveChangesAsync(ct);
        if (context.ActiveManagementCondominiumId is not Guid condominiumId)
            return Results.Ok(new { modules = Array.Empty<object>() });
        return Results.Ok(new { modules = (await modules.GetModulesAsync(condominiumId, ct)).Select(x => new
        { module = x.Module.ToString(), enabled = x.IsEnabled }) });
    }

    private static async Task<object> WithAdministratorEligibility(ManagementContextState context, AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var ids=context.ActiveManagementCondominiumId is Guid active?[active]:context.AvailableCondominiums.Select(x=>x.Id).ToArray();
        var has=await db.CondominiumManagementCompanyLinks.AsNoTracking().AnyAsync(x=>x.IsActive&&ids.Contains(x.CondominiumId),ct);
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var permissions = new List<string>();
        var managementRoles = new List<string>();
        if (Guid.TryParse(value, out var userId) && ids.Length > 0)
        {
            var memberships = await db.CondominiumMemberships.AsNoTracking()
                .Where(x => x.UserId == userId && ids.Contains(x.CondominiumId) && x.IsActive && x.EndedAt == null)
                .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.IsActive && x.RevokedAt == null && (x.Role == CondominiumRole.Manager || x.Role == CondominiumRole.SubManager)), x => x.Id, x => x.CondominiumMembershipId, (membership, role) => new { membership.Id, role.Role })
                .ToListAsync(ct);
            if (memberships.Any(x => x.Role == CondominiumRole.Manager)) managementRoles.Add(nameof(CondominiumRole.Manager));
            if (memberships.Any(x => x.Role == CondominiumRole.SubManager)) managementRoles.Add(nameof(CondominiumRole.SubManager));
            var subManagerMembershipIds = memberships.Where(x => x.Role == CondominiumRole.SubManager).Select(x => x.Id).Distinct().ToArray();
            foreach (var membershipId in subManagerMembershipIds)
            {
                await SubManagerAccess.EnsureDefaultsAsync(db, membershipId, userId, ct);
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
            permissions = await db.SubManagerModulePermissions.AsNoTracking()
                .Where(x => subManagerMembershipIds.Contains(x.CondominiumMembershipId) && x.IsAllowed && x.RevokedAt == null)
                .Select(x => x.Module.ToString()).Distinct().ToListAsync(ct);
        }
        return new{context.ActiveManagementCondominiumId,context.UsesConsolidatedManagementScope,context.CondominiumCount,context.ActiveCondominium,context.AvailableCondominiums,HasEligibleManagementCompany=has,ManagementRoles=managementRoles,SubManagerPermissions=permissions};
    }

    public sealed record ManagementContextRequest(Guid? CondominiumId);
}
