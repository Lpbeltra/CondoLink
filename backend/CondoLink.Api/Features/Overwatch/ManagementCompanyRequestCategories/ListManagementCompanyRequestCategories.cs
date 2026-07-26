using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public static class ListManagementCompanyRequestCategories
{
    public static IEndpointRouteBuilder MapListManagementCompanyRequestCategories(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/management-companies/{managementCompanyId:guid}/request-categories",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List management company request categories");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await CreateManagementCompanyRequestCategory.CompanyExistsAsync(
                dbContext, managementCompanyId, cancellationToken))
            return Results.NotFound(new
            {
                message = "Management company not found."
            });

        var categories = await dbContext.ManagementCompanyRequestCategories
            .AsNoTracking()
            .Where(category =>
                category.ManagementCompanyId == managementCompanyId)
            .OrderBy(category => category.Name)
            .Select(category =>
                new ManagementCompanyRequestCategoryResponse(
                    category.Id,
                    category.ManagementCompanyId,
                    category.Name,
                    category.Description,
                    category.FormType,
                    category.IsActive,
                    category.CreatedAt,
                    category.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(categories);
    }
}
