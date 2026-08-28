using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class DeleteManagementCompanyEmployee
{
    public static IEndpointRouteBuilder MapDeleteManagementCompanyEmployee(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/employees/{employeeId:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Remove management company employee link");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid employeeId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.ManagementCompanyEmployees
            .FirstOrDefaultAsync(
                employee => employee.Id == employeeId,
                cancellationToken);
        if (employee is null)
        {
            return Results.NotFound(new
            {
                message = "Management company employee not found."
            });
        }

        employee.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
