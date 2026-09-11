using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class GetEmployeeById
{
    public static IEndpointRouteBuilder MapGetEmployeeById(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/{employeeId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("Get a condominium employee by id");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid employeeId,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);

        var employee = await db.Employees.AsNoTracking()
            .Where(x => x.Id == employeeId && x.CondominiumId == condominiumId)
            .Select(x => new EmployeeResponse(x.Id, x.CondominiumId, x.FullName, x.JobTitle, x.PhoneNumber,
                x.Email, x.RegistrationNumber, x.AdmissionDate, x.IsActive, x.CreatedAt, x.UpdatedAt))
            .SingleOrDefaultAsync(ct);

        return employee is null ? Results.NotFound(new { message = "Funcionário não encontrado." }) : Results.Ok(employee);
    }
}
