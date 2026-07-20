namespace CondoLink.Api.Features.Overwatch.Managers;

public static class CreateOverwatchManagementMembership
{
    public static IEndpointRouteBuilder MapCreateOverwatchManagementMembership(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/management-memberships",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        ManagerOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        if (request.ManagerId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = "ManagerId is required."
            });
        }

        if (request.CondominiumId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = "CondominiumId is required."
            });
        }

        var result = await onboardingService.OnboardAsync(
            request.ManagerId,
            request.CondominiumId,
            cancellationToken);

        if (!result.Succeeded)
        {
            if (result.IsConflict)
            {
                return Results.Conflict(new
                {
                    error = result.Error
                });
            }

            return Results.NotFound(new
            {
                error = result.Error
            });
        }

        var response = new Response(
            result.MembershipId!.Value,
            request.ManagerId,
            request.CondominiumId,
            "Manager");

        return Results.Created(
            $"/overwatch/management-memberships/{response.MembershipId}",
            response);
    }

    public sealed record Request(
        Guid ManagerId,
        Guid CondominiumId);

    public sealed record Response(
        Guid MembershipId,
        Guid ManagerId,
        Guid CondominiumId,
        string Role);
}