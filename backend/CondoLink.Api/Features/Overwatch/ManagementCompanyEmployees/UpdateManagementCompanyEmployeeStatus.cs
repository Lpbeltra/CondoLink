using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class UpdateManagementCompanyEmployeeStatus
{
    public static IEndpointRouteBuilder MapUpdateManagementCompanyEmployeeStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/employees/{employeeId:guid}/status", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Activate or deactivate management company employee");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid employeeId,
        Request request,
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

        if (request.IsActive)
            employee.Activate();
        else
            employee.Deactivate();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new Response(
            employee.Id,
            employee.ManagementCompanyId,
            employee.UserId,
            employee.IsActive,
            employee.UpdatedAt));
    }

    public sealed record Request(bool IsActive);
    public sealed record Response(
        Guid Id,
        Guid ManagementCompanyId,
        Guid UserId,
        bool IsActive,
        DateTime UpdatedAt);
}
