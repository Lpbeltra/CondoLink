using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Units;

public static class GetUnitById
{
    public static IEndpointRouteBuilder MapGetUnitById(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/units/{id:guid}", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.Id == id)
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
            .SingleOrDefaultAsync(cancellationToken);

        return unit is null
            ? Results.NotFound(new { error = "Unit not found." })
            : Results.Ok(unit);
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
