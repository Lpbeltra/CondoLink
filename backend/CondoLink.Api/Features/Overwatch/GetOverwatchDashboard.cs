using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch;

public static class GetOverwatchDashboard
{
    public static IEndpointRouteBuilder MapGetOverwatchDashboard(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/dashboard", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get Overwatch dashboard metrics");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var managementCompanyCount = await dbContext.ManagementCompanies.CountAsync(cancellationToken);
        var condominiumCount = await dbContext.Condominiums.CountAsync(cancellationToken);
        var managerRoleId = await dbContext.Roles
            .Where(role => role.Name == "Manager")
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var managerCount = managerRoleId is null
            ? 0
            : await dbContext.UserRoles.CountAsync(
                userRole => userRole.RoleId == managerRoleId.Value,
                cancellationToken);
        var employeeCount = await dbContext.ManagementCompanyEmployees.CountAsync(cancellationToken);

        return Results.Ok(new OverwatchDashboardResponse(
            managementCompanyCount, condominiumCount, managerCount, employeeCount));
    }
}

public sealed record OverwatchDashboardResponse(
    int ManagementCompanyCount,
    int CondominiumCount,
    int ManagerCount,
    int EmployeeCount);
