using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class GetManagementCompany
{
    public static IEndpointRouteBuilder MapGetManagementCompany(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/management-companies/{id:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get management company details");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.ManagementCompanies
            .AsNoTracking()
            .Where(company => company.Id == id)
            .Select(company => new ManagementCompanyResponse(
                company.Id, company.Name, company.LegalName, company.Document,
                company.Email, company.PhoneNumber, company.IsActive,
                company.CreatedAt, company.UpdatedAt,
                company.Condominiums.Count,
                company.Employees.Count))
            .FirstOrDefaultAsync(cancellationToken);

        return company is null
            ? Results.NotFound(new { message = "Management company not found." })
            : Results.Ok(company);
    }
}
