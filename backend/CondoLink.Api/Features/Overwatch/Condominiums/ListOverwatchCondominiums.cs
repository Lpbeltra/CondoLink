using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class ListOverwatchCondominiums
{
    public static IEndpointRouteBuilder MapListOverwatchCondominiums(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List condominiums")
            .WithDescription(
                "Lists all condominiums registered in CondoLink.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? search,
        bool? isActive,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Condominiums
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();

            query = query.Where(condominium =>
                EF.Functions.ILike(
                    condominium.Name,
                    $"%{normalizedSearch}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(condominium =>
                condominium.IsActive == isActive.Value);
        }

        var condominiums = await query
            .OrderBy(condominium => condominium.Name)
            .Select(condominium => new CondominiumListItemResponse(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.PhoneNumber,
                condominium.IsActive,
                condominium.CreatedAt,
                condominium.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(condominiums);
    }

    public sealed record CondominiumListItemResponse(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}