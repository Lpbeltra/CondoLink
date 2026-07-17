using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.Units;

public static class CreateUnit
{
    public static IEndpointRouteBuilder MapCreateUnit(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/units", HandleAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Request request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsManager(principal, condominiumId, dbContext, cancellationToken)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return Results.BadRequest(new { error = "Identifier is required." });
        }

        var identifier = request.Identifier.Trim();
        var blockId = request.BlockId;
        var floor = NormalizeOptional(request.Floor);
        var description = NormalizeOptional(request.Description);

        if (identifier.Length > 50)
        {
            return Results.BadRequest(new { error = "Identifier must not exceed 50 characters." });
        }

        if (floor?.Length > 20)
        {
            return Results.BadRequest(new { error = "Floor must not exceed 20 characters." });
        }

        if (description?.Length > 500)
        {
            return Results.BadRequest(new { error = "Description must not exceed 500 characters." });
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == condominiumId)
            .Select(condominium => new { condominium.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive condominium cannot receive new units." });
        }

        var blocksExist = await dbContext.CondominiumBlocks.AsNoTracking().AnyAsync(block => block.CondominiumId == condominiumId, cancellationToken);
        if (blocksExist && !blockId.HasValue) return Results.BadRequest(new { error = "Block is required when the condominium has registered blocks." });
        if (blockId.HasValue && !await dbContext.CondominiumBlocks.AsNoTracking().AnyAsync(block => block.Id == blockId && block.CondominiumId == condominiumId, cancellationToken)) return Results.BadRequest(new { error = "Block does not belong to this condominium." });

        var alreadyExists = await dbContext.Units
            .AsNoTracking()
            .AnyAsync(
                unit => unit.CondominiumId == condominiumId
                    && unit.BlockId == blockId
                    && unit.Identifier == identifier,
                cancellationToken);

        if (alreadyExists)
        {
            return DuplicateUnitConflict();
        }

        var unit = new Unit(condominiumId, identifier, blockId, floor, description);

        dbContext.Units.Add(unit);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateUnitViolation(exception))
        {
            return DuplicateUnitConflict();
        }

        var response = new Response(
            unit.Id,
            unit.CondominiumId,
            unit.Identifier,
            unit.BlockId,
            blockId.HasValue ? await dbContext.CondominiumBlocks.Where(block => block.Id == blockId).Select(block => block.Identifier).SingleAsync(cancellationToken) : null,
            unit.Floor,
            unit.Description,
            unit.IsActive,
            unit.CreatedAt,
            unit.UpdatedAt);

        return Results.Created($"/units/{unit.Id}", response);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static async Task<bool> IsManager(ClaimsPrincipal principal, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
    }

    private static bool IsDuplicateUnitViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UnitConfiguration.UniqueWithoutBlockIndex
                or UnitConfiguration.UniqueWithBlockIndex
        };
    }

    private static IResult DuplicateUnitConflict()
    {
        return Results.Conflict(new
        {
            error = "A unit with the same identifier and block already exists in this condominium."
        });
    }

    public sealed record Request(
        string? Identifier,
        Guid? BlockId,
        string? Floor,
        string? Description);

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
