using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class ListManagementCompanies
{
    public static IEndpointRouteBuilder MapListManagementCompanies(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/management-companies", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List management companies");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken) =>
        Results.Ok(await dbContext.ManagementCompanies
            .AsNoTracking()
            .OrderBy(company => company.Name)
            .Select(company => new ManagementCompanyResponse(
                company.Id, company.Name, company.Cnpj, company.Address,
                company.City, company.State, company.Email, company.PhoneNumber, company.IsActive,
                company.CreatedAt, company.UpdatedAt,
                company.Condominiums.Count,
                company.Employees.Count))
            .ToListAsync(cancellationToken));
}
