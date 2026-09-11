namespace CondoLink.Api.Features.EmployeeManagement;

public static class MapEmployeeManagement
{
    public static IEndpointRouteBuilder MapEmployeeManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapListEmployees();
        endpoints.MapGetEmployeeById();
        endpoints.MapCreateEmployee();
        endpoints.MapUpdateEmployee();
        endpoints.MapUpdateEmployeeStatus();
        endpoints.MapListAdministratorEmployeeManagementCondominiums();
        return endpoints;
    }
}
