using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.UnitMemberships;

public static class ManageUnitMembership
{
    public static IEndpointRouteBuilder MapManageUnitMembership(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/units/{unitId:guid}/memberships/{membershipId:guid}", UpdateAsync).RequireAuthorization();
        endpoints.MapDelete("/units/{unitId:guid}/memberships/{membershipId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(Guid unitId, Guid membershipId, Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var membership = await db.UnitMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.UnitId == unitId, ct);
        if (membership is null) return Results.NotFound(new { error = "Unit membership not found." });
        var condominiumId = await db.Units.Where(x => x.Id == unitId).Select(x => x.CondominiumId).SingleOrDefaultAsync(ct);
        if (condominiumId == Guid.Empty) return Results.NotFound(new { error = "Unit not found." });
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        if (!TryParse(request.RelationshipType, out var relationshipType)) return Results.BadRequest(new { error = "Relationship type must be Owner, Tenant or AuthorizedOccupant." });
        if (request.IsPrimaryResidence && !request.IsResident) return Results.BadRequest(new { error = "Primary residence requires the user to be a resident." });
        var duplicate = await db.UnitMemberships.AnyAsync(x => x.Id != membershipId && x.UserId == membership.UserId && x.UnitId == unitId && x.RelationshipType == relationshipType, ct);
        if (duplicate) return Results.Conflict(new { error = "This unit relationship is already associated with the user." });
        try { membership.Update(relationshipType, request.IsResident, request.IsPrimaryResidence); }
        catch (InvalidOperationException) { return Results.Conflict(new { error = "Inactive unit membership cannot be edited." }); }
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid unitId, Guid membershipId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var membership = await db.UnitMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.UnitId == unitId, ct);
        if (membership is null) return Results.NotFound(new { error = "Unit membership not found." });
        var condominiumId = await db.Units.Where(x => x.Id == unitId).Select(x => x.CondominiumId).SingleOrDefaultAsync(ct);
        if (condominiumId == Guid.Empty) return Results.NotFound(new { error = "Unit not found." });
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        try { membership.End(DateTime.UtcNow); }
        catch (InvalidOperationException) { return Results.Conflict(new { error = "Unit membership is already inactive." }); }
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static bool TryParse(string? value, out UnitRelationshipType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _) && Enum.TryParse(value, true, out type) && Enum.IsDefined(type);
    }
    private static async Task<bool> IsManager(ClaimsPrincipal principal, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
    }
    public sealed record Request(string? RelationshipType, bool IsResident, bool IsPrimaryResidence);
}
