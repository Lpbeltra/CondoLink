namespace CondoLink.Api.Features.Overwatch.Managers;

public static class UpdateOverwatchManagerStatus
{
    public static IEndpointRouteBuilder MapUpdateOverwatchManagerStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/managers/{managerId:guid}/status",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Update manager status");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        Request request,
        ManagerOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.SetStatusAsync(
            managerId, request.IsActive, cancellationToken);
        if (!result.Succeeded)
        {
            return result.IsConflict
                ? Results.Conflict(new { error = result.Error })
                : Results.NotFound(new { error = result.Error });
        }
        return Results.Ok(new
        {
            Id = managerId,
            request.IsActive,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public sealed record Request(bool IsActive);
}
