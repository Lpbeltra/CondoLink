using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class UpdateManagementCompany
{
    public static IEndpointRouteBuilder MapUpdateManagementCompany(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/overwatch/management-companies/{id:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Update management company");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ManagementCompanyRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.ManagementCompanies
            .FirstOrDefaultAsync(company => company.Id == id, cancellationToken);
        if (company is null)
            return Results.NotFound(new { message = "Management company not found." });

        var validationError = ManagementCompanyValidation.Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var document = CreateManagementCompany.NormalizeOptional(request.Document);
        var email = CreateManagementCompany.NormalizeOptional(request.Email)
            ?.ToLowerInvariant();
        var conflict = await CreateManagementCompany.FindConflictAsync(
            dbContext, document, email, id, cancellationToken);
        if (conflict is not null)
            return Results.Conflict(new { message = conflict });

        company.Update(
            request.Name!, request.LegalName, document, email, request.PhoneNumber);
        await dbContext.SaveChangesAsync(cancellationToken);
        var condominiumCount = await dbContext.Condominiums.CountAsync(
            condominium => condominium.ManagementCompanyId == company.Id,
            cancellationToken);
        return Results.Ok(ManagementCompanyResponse.From(
            company,
            condominiumCount));
    }
}
