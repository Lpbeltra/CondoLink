namespace CondoLink.Api.Features.EmployeeManagement;

public static class MapEmployeeManagement
{
    public static IEndpointRouteBuilder MapEmployeeManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdministratorEmployeeEndpoints();
        return endpoints;
    }
}
