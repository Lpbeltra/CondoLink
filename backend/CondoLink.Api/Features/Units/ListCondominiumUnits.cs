using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class ListCondominiumUnits
{
    public static IEndpointRouteBuilder MapListCondominiumUnits(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/units", HandleAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var manager = Guid.TryParse(value, out var userId) && await dbContext.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(cancellationToken);
        if (!manager) return Results.Forbid();
        var condominiumExists = await dbContext.Condominiums
            .AnyAsync(
                condominium => condominium.Id == condominiumId,
                cancellationToken);

        if (!condominiumExists)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var units = await (from unit in dbContext.Units.AsNoTracking()
            join block in dbContext.CondominiumBlocks.AsNoTracking() on unit.BlockId equals block.Id into blocks
            from block in blocks.DefaultIfEmpty()
            where unit.CondominiumId == condominiumId
            select new { unit, block })
            .AsNoTracking()
            .Select(item => new Response(item.unit.Id, item.unit.CondominiumId, item.unit.Identifier, item.unit.BlockId, item.block == null ? null : item.block.Identifier, item.unit.Floor, item.unit.Description, item.unit.IsActive,
                dbContext.UnitMemberships.Count(link => link.UnitId == item.unit.Id), item.unit.CreatedAt, item.unit.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(units);
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
        int PeopleCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
