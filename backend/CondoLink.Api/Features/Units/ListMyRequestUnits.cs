using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class ListMyRequestUnits
{
    public static IEndpointRouteBuilder MapListMyRequestUnits(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/condominiums/{condominiumId:guid}/units/mine",
                HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId))
            return Results.Unauthorized();
        var userActive = await db.Set<ApplicationUser>().AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive,
                cancellationToken);
        if (!userActive) return Results.Unauthorized();
        var member = await db.CondominiumMemberships.AsNoTracking()
            .AnyAsync(item =>
                item.UserId == userId
                && item.CondominiumId == condominiumId
                && item.IsActive
                && item.EndedAt == null,
                cancellationToken);
        if (!member) return Results.Forbid();

        var units = await (
                from membership in db.UnitMemberships.AsNoTracking()
                join unit in db.Units.AsNoTracking()
                    on membership.UnitId equals unit.Id
                join block in db.CondominiumBlocks.AsNoTracking()
                    on unit.BlockId equals block.Id into blocks
                from block in blocks.DefaultIfEmpty()
                where membership.UserId == userId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && unit.CondominiumId == condominiumId
                    && unit.IsActive
                orderby block == null ? string.Empty : block.Identifier,
                    unit.Identifier
                select new Response(
                    unit.Id,
                    unit.Identifier,
                    block == null ? null : block.Identifier))
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return Results.Ok(units);
    }

    public sealed record Response(
        Guid Id,
        string Identifier,
        string? Block);
}
