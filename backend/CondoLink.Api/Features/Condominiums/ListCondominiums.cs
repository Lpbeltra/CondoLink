using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Condominiums;

public static class ListCondominiums
{
    public static IEndpointRouteBuilder MapListCondominiums(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominiums = await dbContext.Condominiums
            .AsNoTracking()
            .OrderBy(condominium => condominium.Name)
            .Select(condominium => new Response(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.Cnpj,
                condominium.Address,
                condominium.City,
                condominium.State,
                condominium.IsActive,
                condominium.CreatedAt,
                condominium.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(condominiums);
    }

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
