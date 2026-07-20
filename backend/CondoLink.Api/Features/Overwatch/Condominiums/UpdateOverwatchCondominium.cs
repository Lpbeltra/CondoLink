using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class UpdateOverwatchCondominium
{
    public static IEndpointRouteBuilder MapUpdateOverwatchCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/overwatch/condominiums/{id:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Update condominium")
            .WithDescription("Updates a condominium.");
    
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
                condominium => condominium.Id == id,
                cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        var name = request.Name.Trim();

        var duplicated = await dbContext.Condominiums
            .AnyAsync(
                current =>
                    current.Id != id &&
                    current.Name == name,
                cancellationToken);

        if (duplicated)
        {
            return Results.Conflict(new
            {
                message = "A condominium with this name already exists."
            });
        }

        condominium.Update(
            name,
            request.Email?.Trim(),
            request.PhoneNumber?.Trim());

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            condominium.Id,
            condominium.Name,
            condominium.Email,
            condominium.PhoneNumber,
            condominium.IsActive,
            condominium.CreatedAt,
            condominium.UpdatedAt));
    }

    public sealed record Request(
        string Name,
        string? Email,
        string? PhoneNumber);

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}