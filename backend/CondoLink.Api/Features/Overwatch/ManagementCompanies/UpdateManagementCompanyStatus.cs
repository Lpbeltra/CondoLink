using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class UpdateManagementCompanyStatus
{
    public static IEndpointRouteBuilder MapUpdateManagementCompanyStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/management-companies/{id:guid}/status", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Activate or deactivate management company");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.ManagementCompanies
            .FirstOrDefaultAsync(company => company.Id == id, cancellationToken);
        if (company is null)
            return Results.NotFound(new { message = "Management company not found." });

        company.SetActiveStatus(request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new Response(company.Id, company.Name, company.IsActive));
    }

    public sealed record Request(bool IsActive);
    public sealed record Response(Guid Id, string Name, bool IsActive);
}
