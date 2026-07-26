using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class CreateManagementCompany
{
    public static IEndpointRouteBuilder MapCreateManagementCompany(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/overwatch/management-companies", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create management company");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ManagementCompanyRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationError = ManagementCompanyValidation.Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var document = NormalizeOptional(request.Document);
        var email = NormalizeOptional(request.Email)?.ToLowerInvariant();
        var conflict = await FindConflictAsync(
            dbContext, document, email, null, cancellationToken);
        if (conflict is not null)
            return Results.Conflict(new { message = conflict });

        var company = new ManagementCompany(
            request.Name!, request.LegalName, document, email, request.PhoneNumber);
        dbContext.ManagementCompanies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/overwatch/management-companies/{company.Id}",
            ManagementCompanyResponse.From(company));
    }

    internal static async Task<string?> FindConflictAsync(
        AppDbContext dbContext,
        string? document,
        string? email,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (document is not null &&
            await dbContext.ManagementCompanies.AnyAsync(
                company => (!excludedId.HasValue || company.Id != excludedId.Value)
                    && company.Document == document,
                cancellationToken))
            return "A management company with this document already exists.";

        if (email is not null &&
            await dbContext.ManagementCompanies.AnyAsync(
                company => (!excludedId.HasValue || company.Id != excludedId.Value)
                    && company.Email == email,
                cancellationToken))
            return "A management company with this email already exists.";

        return null;
    }

    internal static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
