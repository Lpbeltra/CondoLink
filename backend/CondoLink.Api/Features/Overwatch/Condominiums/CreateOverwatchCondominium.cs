using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class CreateOverwatchCondominium
{
    public static IEndpointRouteBuilder MapCreateOverwatchCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/condominiums",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create condominium")
            .WithDescription("Creates a new condominium.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var exists = await dbContext.Condominiums
            .AnyAsync(
                condominium => condominium.Name == name,
                cancellationToken);

        if (exists)
        {
            return Results.Conflict(new
            {
                message = "A condominium with this name already exists."
            });
        }

        var condominium = new Condominium(
            name,
            request.Email?.Trim(),
            request.PhoneNumber?.Trim());

        dbContext.Condominiums.Add(condominium);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/overwatch/condominiums/{condominium.Id}",
            new Response(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.PhoneNumber,
                condominium.IsActive));
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
        bool IsActive);
}