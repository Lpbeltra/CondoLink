using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public static class ListEmployees
{
    public static IEndpointRouteBuilder MapListEmployees(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeManagement")
            .WithSummary("List a condominium's employees");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, string? search, string? status,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagementAccessService access, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);

        var query = db.Employees.AsNoTracking().Where(x => x.CondominiumId == condominiumId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term)
                || (x.JobTitle != null && x.JobTitle.ToLower().Contains(term)));
        }
        query = status switch
        {
            "active" => query.Where(x => x.IsActive),
            "inactive" => query.Where(x => !x.IsActive),
            _ => query
        };

        var employees = await query.OrderBy(x => x.FullName)
            .Select(x => new EmployeeResponse(x.Id, x.CondominiumId, x.FullName, x.JobTitle, x.PhoneNumber,
                x.Email, x.RegistrationNumber, x.AdmissionDate, x.IsActive, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(employees);
    }
}
