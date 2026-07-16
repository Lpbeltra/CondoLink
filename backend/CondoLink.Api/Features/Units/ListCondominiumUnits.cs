using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class ListCondominiumUnits
{
    public static IEndpointRouteBuilder MapListCondominiumUnits(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/units", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominiumExists = await dbContext.Condominiums
            .AnyAsync(
                condominium => condominium.Id == condominiumId,
                cancellationToken);

        if (!condominiumExists)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var units = await dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.CondominiumId == condominiumId)
            .OrderBy(unit => unit.Block != null)
            .ThenBy(unit => unit.Block)
            .ThenBy(unit => unit.Identifier)
            .Select(unit => new Response(
                unit.Id,
                unit.CondominiumId,
                unit.Identifier,
                unit.Block,
                unit.Floor,
                unit.Description,
                unit.IsActive,
                unit.CreatedAt,
                unit.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(units);
    }

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        string Identifier,
        string? Block,
        string? Floor,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
