using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class UpdateOverwatchCondominiumStatus
{
    public static IEndpointRouteBuilder MapUpdateOverwatchCondominiumStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/condominiums/{id:guid}/status",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Activate or deactivate condominium")
            .WithDescription("Updates the active status of a condominium.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominium = await dbContext.Condominiums
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        condominium.SetActiveStatus(request.IsActive);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            condominium.Id,
            condominium.Name,
            condominium.IsActive));
    }

    public sealed record Request(
        bool IsActive);

    public sealed record Response(
        Guid Id,
        string Name,
        bool IsActive);
}