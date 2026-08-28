using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public static class GetManagementCompanyRequestCategory
{
    public static IEndpointRouteBuilder MapGetManagementCompanyRequestCategory(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/management-companies/{managementCompanyId:guid}/request-categories/{categoryId:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get management company request category");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        Guid categoryId,
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
            .AsNoTracking()
            .Where(category =>
                category.ManagementCompanyId == managementCompanyId &&
                category.Id == categoryId)
            .Select(category =>
                new ManagementCompanyRequestCategoryResponse(
                    category.Id,
                    category.ManagementCompanyId,
                    category.Name,
                    category.Description,
                    category.FormType,
                    category.IsActive,
                    category.CreatedAt,
                    category.UpdatedAt,
                    dbContext.ManagementCompanyRequestCategoryResponsibles
                        .Where(x => x.ManagementCompanyRequestCategoryId == category.Id)
                        .Select(x => x.ManagementCompanyEmployeeId).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return category is null
            ? Results.NotFound(new { message = "Request category not found." })
            : Results.Ok(category);
    }
}
