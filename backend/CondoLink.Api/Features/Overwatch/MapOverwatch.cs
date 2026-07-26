using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Infrastructure;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;


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
            endpoints.MapListOverwatchCondominiumManagers();
            endpoints.MapListOverwatchManagers();
            endpoints.MapCreateOverwatchManager();
            endpoints.MapGetOverwatchManager();
            endpoints.MapUpdateOverwatchManagerStatus();
            endpoints.MapListManagerCondominiums();
            endpoints.MapCreateOverwatchManagementMembership();
            endpoints.MapRemoveManagerCondominium();
            endpoints.MapListManagementCompanies();
            endpoints.MapGetManagementCompany();
            endpoints.MapCreateManagementCompany();
            endpoints.MapUpdateManagementCompany();
            endpoints.MapUpdateManagementCompanyStatus();
            endpoints.MapSetCondominiumManagementCompany();
            endpoints.MapCreateManagementCompanyEmployee();
            endpoints.MapListManagementCompanyEmployees();
            endpoints.MapUpdateManagementCompanyEmployeeStatus();
            endpoints.MapDeleteManagementCompanyEmployee();
            endpoints.MapListManagementCompanyRequestCategories();
            endpoints.MapGetManagementCompanyRequestCategory();
            endpoints.MapCreateManagementCompanyRequestCategory();
            endpoints.MapUpdateManagementCompanyRequestCategory();
            endpoints.MapUpdateManagementCompanyRequestCategoryStatus();

        return endpoints;
    }
}
