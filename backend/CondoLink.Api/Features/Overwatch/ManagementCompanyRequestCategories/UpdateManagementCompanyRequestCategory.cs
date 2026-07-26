using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public static class UpdateManagementCompanyRequestCategory
{
    public static IEndpointRouteBuilder MapUpdateManagementCompanyRequestCategory(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/overwatch/management-companies/{managementCompanyId:guid}/request-categories/{categoryId:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Update management company request category");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        Guid categoryId,
        ManagementCompanyRequestCategoryRequest request,
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

        var validationError =
            ManagementCompanyRequestCategoryValidation.Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var normalizedName = request.Name!.Trim().ToUpperInvariant();
        if (await CreateManagementCompanyRequestCategory.IsDuplicateAsync(
                dbContext,
                managementCompanyId,
                normalizedName,
                categoryId,
                cancellationToken))
            return Results.Conflict(new
            {
                message =
                    "A request category with this name already exists."
            });

        category.Update(request.Name, request.Description, request.FormType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(
            ManagementCompanyRequestCategoryResponse.From(category));
    }
}
