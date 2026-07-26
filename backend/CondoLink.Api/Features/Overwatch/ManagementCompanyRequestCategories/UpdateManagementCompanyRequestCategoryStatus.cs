using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public static class UpdateManagementCompanyRequestCategoryStatus
{
    public static IEndpointRouteBuilder
        MapUpdateManagementCompanyRequestCategoryStatus(
            this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/management-companies/{managementCompanyId:guid}/request-categories/{categoryId:guid}/status",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Activate or deactivate request category");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        Guid categoryId,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await CreateManagementCompanyRequestCategory.CompanyExistsAsync(
                dbContext, managementCompanyId, cancellationToken))
            return Results.NotFound(new
            {
                message = "Management company not found."
            });

        var category = await dbContext.ManagementCompanyRequestCategories
            .FirstOrDefaultAsync(
                category =>
                    category.ManagementCompanyId == managementCompanyId &&
                    category.Id == categoryId,
                cancellationToken);
        if (category is null)
            return Results.NotFound(new
            {
                message = "Request category not found."
            });

        category.SetActiveStatus(request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(
            ManagementCompanyRequestCategoryResponse.From(category));
    }

    public sealed record Request(bool IsActive);
}
