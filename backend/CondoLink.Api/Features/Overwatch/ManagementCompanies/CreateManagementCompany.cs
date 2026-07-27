using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class CreateManagementCompany
{
    public static IEndpointRouteBuilder MapCreateManagementCompany(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/overwatch/management-companies", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Create management company");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ManagementCompanyRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var error = ManagementCompanyValidation.Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var cnpj = RegistrationData.Digits(request.Cnpj)!;
        var email = RegistrationData.Optional(request.Email)?.ToLowerInvariant();
        var conflict = await FindConflictAsync(db, cnpj, email, null, cancellationToken);
        if (conflict is not null) return Results.Conflict(new { message = conflict });
        var company = new ManagementCompany(request.Name!, cnpj, request.Address,
            request.City, request.State, email, request.PhoneNumber);
        db.ManagementCompanies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/overwatch/management-companies/{company.Id}",
            ManagementCompanyResponse.From(company));
    }

    internal static async Task<string?> FindConflictAsync(
        AppDbContext db, string cnpj, string? email, Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await db.ManagementCompanies.AnyAsync(company =>
            (!excludedId.HasValue || company.Id != excludedId) && company.Cnpj == cnpj,
            cancellationToken))
            return "A management company with this CNPJ already exists.";
        if (email is not null && await db.ManagementCompanies.AnyAsync(company =>
            (!excludedId.HasValue || company.Id != excludedId) && company.Email == email,
            cancellationToken))
            return "A management company with this email already exists.";
        return null;
    }
}
