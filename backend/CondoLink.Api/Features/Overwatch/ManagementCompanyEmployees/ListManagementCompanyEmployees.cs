using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class ListManagementCompanyEmployees
{
    public static IEndpointRouteBuilder MapListManagementCompanyEmployees(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/management-companies/{managementCompanyId:guid}/employees",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List management company employees");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var companyExists = await dbContext.ManagementCompanies
            .AnyAsync(
                company => company.Id == managementCompanyId,
                cancellationToken);
        if (!companyExists)
        {
            return Results.NotFound(new
            {
                message = "Management company not found."
            });
        }

        var employees = await (
            from employee in dbContext.ManagementCompanyEmployees.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on employee.UserId equals user.Id
            where employee.ManagementCompanyId == managementCompanyId && employee.IsActive && user.IsActive
            orderby user.FullName
            select new ManagementCompanyEmployeeResponse(
                employee.Id,
                employee.ManagementCompanyId,
                employee.UserId,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                employee.JobTitle,
                employee.AccessType,
                employee.IsActive,
                user.LastLoginAt,
                employee.CreatedAt,
                employee.UpdatedAt,
                dbContext.ManagementCompanyRequestCategoryResponsibles
                    .Where(x => x.ManagementCompanyEmployeeId == employee.Id)
                    .Select(x => x.ManagementCompanyRequestCategoryId).ToList()))
            .ToListAsync(cancellationToken);

        return Results.Ok(employees);
    }
}
