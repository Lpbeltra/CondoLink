using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Condominiums;

public static class GetCondominiumById
{
    public static IEndpointRouteBuilder MapGetCondominiumById(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{id:guid}", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == id)
            .Select(condominium => new Response(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.PhoneNumber,
                condominium.IsActive,
                condominium.CreatedAt,
                condominium.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return condominium is null
            ? Results.NotFound(new { error = "Condominium not found." })
            : Results.Ok(condominium);
    }

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
