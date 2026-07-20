using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class GetOverwatchCondominium
{
    public static IEndpointRouteBuilder MapGetOverwatchCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums/{id:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get condominium details")
            .WithDescription(
                "Returns the details of a condominium registered in CondoLink.");

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
            .Select(condominium => new CondominiumDetailsResponse(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.PhoneNumber,
                condominium.IsActive,
                condominium.CreatedAt,
                condominium.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        return Results.Ok(condominium);
    }

    public sealed record CondominiumDetailsResponse(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}