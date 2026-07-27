namespace CondoLink.Api.Features.Overwatch.Managers;

public static class RemoveManagerCondominium
{
    public static IEndpointRouteBuilder MapRemoveManagerCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete(
                "/overwatch/managers/{managerId:guid}/condominiums/{condominiumId:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Remove manager from condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        Guid condominiumId,
        ManagerOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.RemoveAsync(
            managerId, condominiumId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.IsConflict
                ? Results.Conflict(new { error = result.Error })
                : Results.NotFound(new { error = result.Error });
        }
        return Results.NoContent();
    }
}
