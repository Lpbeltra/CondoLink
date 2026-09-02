using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Domain.Enums;
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

    private static async Task<object> WithAdministratorEligibility(ManagementContextState context, AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var ids=context.ActiveManagementCondominiumId is Guid active?[active]:context.AvailableCondominiums.Select(x=>x.Id).ToArray();
        var has=await db.CondominiumManagementCompanyLinks.AsNoTracking().AnyAsync(x=>x.IsActive&&ids.Contains(x.CondominiumId),ct);
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var permissions = new List<string>();
        if (Guid.TryParse(value, out var userId) && context.ActiveManagementCondominiumId is Guid condominiumId)
        {
            var membershipId = await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
                .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.SubManager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (x, _) => x.Id).SingleOrDefaultAsync(ct);
            if (membershipId != Guid.Empty)
            {
                await SubManagerAccess.EnsureDefaultsAsync(db, membershipId, userId, ct);
                if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
                permissions = await db.SubManagerModulePermissions.AsNoTracking().Where(x => x.CondominiumMembershipId == membershipId && x.IsAllowed && x.RevokedAt == null).Select(x => x.Module.ToString()).ToListAsync(ct);
            }
        }
        return new{context.ActiveManagementCondominiumId,context.UsesConsolidatedManagementScope,context.CondominiumCount,context.ActiveCondominium,context.AvailableCondominiums,HasEligibleManagementCompany=has,SubManagerPermissions=permissions};
    }

    public sealed record ManagementContextRequest(Guid? CondominiumId);
}
