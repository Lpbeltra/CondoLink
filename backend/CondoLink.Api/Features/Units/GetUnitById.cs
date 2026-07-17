using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class GetUnitById
{
    public static IEndpointRouteBuilder MapGetUnitById(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/units/{id:guid}", HandleAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var unit = await (from item in dbContext.Units.AsNoTracking()
            join block in dbContext.CondominiumBlocks.AsNoTracking() on item.BlockId equals block.Id into blocks
            from block in blocks.DefaultIfEmpty()
            where item.Id == id
            select new Response(item.Id, item.CondominiumId, item.Identifier, item.BlockId, block == null ? null : block.Identifier, item.Floor, item.Description, item.IsActive, item.CreatedAt, item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null) return Results.NotFound(new { error = "Unit not found." });
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var manager = Guid.TryParse(value, out var userId) && await dbContext.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == unit.CondominiumId && x.IsActive && x.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(cancellationToken);
        return manager ? Results.Ok(unit) : Results.Forbid();
    }

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        string Identifier,
        Guid? BlockId,
        string? Block,
        string? Floor,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
