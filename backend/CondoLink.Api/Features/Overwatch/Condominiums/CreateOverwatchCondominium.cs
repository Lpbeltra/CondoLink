using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class CreateOverwatchCondominium
{
    public static IEndpointRouteBuilder MapCreateOverwatchCondominium(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/overwatch/condominiums", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Create condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        CondominiumRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var result = await OverwatchCondominiumCreationService.CreateAsync(db, request, null, null, null, cancellationToken);
        if (result.Error is not null)
            return result.Conflict ? Results.Conflict(new { message = result.Error }) : Results.BadRequest(new { message = result.Error });
        return Results.Created($"/overwatch/condominiums/{result.Condominium!.Id}", new { result.Condominium.Id });
    }
}
