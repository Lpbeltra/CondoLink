using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Infrastructure;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;
using CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;
using CondoLink.Api.Features.Overwatch.OperationalMessages;
using CondoLink.Api.Features.Overwatch.SubManagers;
using CondoLink.Api.Features.Overwatch.ManagementCompanyRequests;


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

            endpoints.MapGetOverwatchDashboard();
            endpoints.MapGetSystemStatus();
            endpoints.MapListOverwatchCondominiums();
            endpoints.MapGetOverwatchCondominium();
            endpoints.MapCreateOverwatchCondominium();
            endpoints.MapUpdateOverwatchCondominium();
            endpoints.MapUpdateOverwatchCondominiumStatus();
            endpoints.MapListOverwatchCondominiumManagers();
            endpoints.MapGetOverwatchCondominiumManager();
            endpoints.MapReplaceOverwatchCondominiumManager();
            endpoints.MapListOverwatchManagers();
            endpoints.MapCreateOverwatchManager();
            endpoints.MapGetOverwatchManager();
            endpoints.MapUpdateOverwatchManager();
            endpoints.MapUpdateOverwatchManagerStatus();
            endpoints.MapListManagerCondominiums();
            endpoints.MapCreateOverwatchManagementMembership();
            endpoints.MapRemoveManagerCondominium();
            endpoints.MapManagementPixEndpoint();
            endpoints.MapSubManagerEndpoints();
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
            endpoints.MapManagementCompanyAccessLifecycleEndpoints();
            endpoints.MapDeleteManagementCompanyRequest();
            endpoints.MapListManagementCompanyRequests();
            endpoints.MapListManagementCompanyRequestCategories();
            endpoints.MapGetManagementCompanyRequestCategory();
            endpoints.MapCreateManagementCompanyRequestCategory();
            endpoints.MapUpdateManagementCompanyRequestCategory();
            endpoints.MapUpdateManagementCompanyRequestCategoryStatus();
            endpoints.MapOperationalMessageEndpoints();

        return endpoints;
    }
}
