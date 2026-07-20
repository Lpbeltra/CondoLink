using CondoLink.Infrastructure;

namespace CondoLink.Api.Features.Overwatch;

public static class MapOverwatch
{
    public static IEndpointRouteBuilder MapOverwatchEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch",
                () => Results.Ok(new
                {
                    message = "Welcome to CondoLink Overwatch."
                }))
            .RequireAuthorization(
                DependencyInjection.PlatformAdminPolicy)
            .WithTags("Overwatch")
            .WithSummary("Access Overwatch");

        return endpoints;
    }
}