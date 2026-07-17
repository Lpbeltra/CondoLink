using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Blocks;

public static class CondominiumBlockEndpoints
{
    public static IEndpointRouteBuilder MapCondominiumBlocks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/blocks", ListAsync).RequireAuthorization();
        endpoints.MapPost("/condominiums/{condominiumId:guid}/blocks", CreateAsync).RequireAuthorization();
        endpoints.MapPut("/condominiums/{condominiumId:guid}/blocks/{blockId:guid}", UpdateAsync).RequireAuthorization();
        endpoints.MapDelete("/condominiums/{condominiumId:guid}/blocks/{blockId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid condominiumId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var blocks = await db.CondominiumBlocks.AsNoTracking().Where(x => x.CondominiumId == condominiumId)
            .GroupJoin(db.Units.AsNoTracking(), block => block.Id, unit => unit.BlockId, (block, units) => new Response(block.Id, block.CondominiumId, block.Identifier, units.Count(), block.CreatedAt, block.UpdatedAt))
            .ToListAsync(ct);
        return Results.Ok(blocks);
    }

    private static async Task<IResult> CreateAsync(Guid condominiumId, BlockRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var identifier = request.Identifier?.Trim();
        if (string.IsNullOrEmpty(identifier)) return Results.BadRequest(new { error = "Block identifier is required." });
        if (identifier.Length > 50) return Results.BadRequest(new { error = "Block identifier must not exceed 50 characters." });
        if (await db.CondominiumBlocks.AnyAsync(x => x.CondominiumId == condominiumId && x.Identifier.ToLower() == identifier.ToLower(), ct)) return Duplicate();
        var block = new CondominiumBlock(condominiumId, identifier); db.CondominiumBlocks.Add(block); await db.SaveChangesAsync(ct);
        return Results.Created($"/condominiums/{condominiumId}/blocks/{block.Id}", new Response(block.Id, condominiumId, block.Identifier, 0, block.CreatedAt, block.UpdatedAt));
    }

    private static async Task<IResult> UpdateAsync(Guid condominiumId, Guid blockId, BlockRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var block = await db.CondominiumBlocks.SingleOrDefaultAsync(x => x.Id == blockId && x.CondominiumId == condominiumId, ct);
        if (block is null) return Results.NotFound(new { error = "Block not found." });
        var identifier = request.Identifier?.Trim();
        if (string.IsNullOrEmpty(identifier)) return Results.BadRequest(new { error = "Block identifier is required." });
        if (identifier.Length > 50) return Results.BadRequest(new { error = "Block identifier must not exceed 50 characters." });
        if (await db.CondominiumBlocks.AnyAsync(x => x.CondominiumId == condominiumId && x.Id != blockId && x.Identifier.ToLower() == identifier.ToLower(), ct)) return Duplicate();
        block.Rename(identifier); await db.SaveChangesAsync(ct); return Results.Ok(new Response(block.Id, condominiumId, block.Identifier, await db.Units.CountAsync(x => x.BlockId == blockId, ct), block.CreatedAt, block.UpdatedAt));
    }

    private static async Task<IResult> DeleteAsync(Guid condominiumId, Guid blockId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var block = await db.CondominiumBlocks.SingleOrDefaultAsync(x => x.Id == blockId && x.CondominiumId == condominiumId, ct);
        if (block is null) return Results.NotFound(new { error = "Block not found." });
        var count = await db.Units.CountAsync(x => x.BlockId == blockId, ct);
        if (count > 0) return Results.Conflict(new { error = $"Não é possível excluir este bloco porque existem {count} unidades vinculadas a ele." });
        db.CondominiumBlocks.Remove(block); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<bool> IsManager(ClaimsPrincipal principal, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
    }

    private static IResult Duplicate() => Results.Conflict(new { error = "Já existe um bloco com esta identificação no condomínio." });
    public sealed record BlockRequest(string? Identifier);
    public sealed record Response(Guid Id, Guid CondominiumId, string Identifier, int UnitCount, DateTime CreatedAt, DateTime UpdatedAt);
}
