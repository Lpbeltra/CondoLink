using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Infrastructure;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Api.Features.Overwatch.Managers;


namespace CondoLink.Api.Features.Overwatch;

public static class MapOverwatch
{
    public static IEndpointRouteBuilder MapOverwatchEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch", () =>
            Results.Ok(new
            {
                message = "Welcome to CondoLink Overwatch."
            }))
            .RequireAuthorization("PlatformAdmin");

            endpoints.MapListOverwatchCondominiums();
            endpoints.MapGetOverwatchCondominium();
            endpoints.MapCreateOverwatchCondominium();
            endpoints.MapUpdateOverwatchCondominium();
            endpoints.MapUpdateOverwatchCondominiumStatus();
            endpoints.MapListOverwatchManagers();
            endpoints.MapCreateOverwatchManager();
            endpoints.MapCreateOverwatchManagementMembership();

        return endpoints;
    }
}