using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class ManageUnit
{
    public static IEndpointRouteBuilder MapManageUnit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/condominiums/{condominiumId:guid}/units/{unitId:guid}", UpdateAsync).RequireAuthorization();
        endpoints.MapDelete("/condominiums/{condominiumId:guid}/units/{unitId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(Guid condominiumId, Guid unitId, Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var unit = await db.Units.SingleOrDefaultAsync(x => x.Id == unitId && x.CondominiumId == condominiumId, ct);
        if (unit is null) return Results.NotFound(new { error = "Unit not found." });
        var identifier = request.Identifier?.Trim();
        if (string.IsNullOrEmpty(identifier)) return Results.BadRequest(new { error = "Identifier is required." });
        if (identifier.Length > 50 || request.Description?.Trim().Length > 500) return Results.BadRequest(new { error = "Invalid unit data." });
        var blocksExist = await db.CondominiumBlocks.AnyAsync(x => x.CondominiumId == condominiumId, ct);
        if (blocksExist && !request.BlockId.HasValue) return Results.BadRequest(new { error = "Block is required when the condominium has registered blocks." });
        if (request.BlockId.HasValue && !await db.CondominiumBlocks.AnyAsync(x => x.Id == request.BlockId && x.CondominiumId == condominiumId, ct)) return Results.BadRequest(new { error = "Block does not belong to this condominium." });
        if (await db.Units.AnyAsync(x => x.Id != unitId && x.CondominiumId == condominiumId && x.BlockId == request.BlockId && x.Identifier == identifier, ct)) return Results.Conflict(new { error = "Já existe uma unidade com esta identificação no mesmo bloco." });
        unit.Update(identifier, request.BlockId, request.Description); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid condominiumId, Guid unitId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var unit = await db.Units.SingleOrDefaultAsync(x => x.Id == unitId && x.CondominiumId == condominiumId, ct);
        if (unit is null) return Results.NotFound(new { error = "Unit not found." });
        if (await db.UnitMemberships.AnyAsync(x => x.UnitId == unitId, ct) || await db.Requests.AnyAsync(x => x.TargetUnitId == unitId, ct))
            return Results.Conflict(new { error = "Não é possível excluir esta unidade porque ela possui pessoas ou registros vinculados." });
        db.Units.Remove(unit); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<bool> IsManager(ClaimsPrincipal principal, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
    }

    public sealed record Request(string? Identifier, Guid? BlockId, string? Description);
}
