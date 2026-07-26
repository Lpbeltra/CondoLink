using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public static class CreateManagementCompanyRequestCategory
{
    public static IEndpointRouteBuilder MapCreateManagementCompanyRequestCategory(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/management-companies/{managementCompanyId:guid}/request-categories",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create management company request category");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        ManagementCompanyRequestCategoryRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(
                dbContext, managementCompanyId, cancellationToken))
            return Results.NotFound(new
            {
                message = "Management company not found."
            });

        var validationError =
            ManagementCompanyRequestCategoryValidation.Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var normalizedName = request.Name!.Trim().ToUpperInvariant();
        if (await IsDuplicateAsync(
                dbContext,
                managementCompanyId,
                normalizedName,
                null,
                cancellationToken))
            return Results.Conflict(new
            {
                message =
                    "A request category with this name already exists."
            });

        var category = new ManagementCompanyRequestCategory(
            managementCompanyId,
            request.Name,
            request.Description,
            request.FormType);
        dbContext.ManagementCompanyRequestCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/overwatch/management-companies/{managementCompanyId}/request-categories/{category.Id}",
            ManagementCompanyRequestCategoryResponse.From(category));
    }

    internal static Task<bool> CompanyExistsAsync(
        AppDbContext dbContext,
        Guid managementCompanyId,
        CancellationToken cancellationToken) =>
        dbContext.ManagementCompanies.AnyAsync(
            company => company.Id == managementCompanyId,
            cancellationToken);

    internal static Task<bool> IsDuplicateAsync(
        AppDbContext dbContext,
        Guid managementCompanyId,
        string normalizedName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.ManagementCompanyRequestCategories.AnyAsync(
            category =>
                category.ManagementCompanyId == managementCompanyId &&
                category.NormalizedName == normalizedName &&
                (!excludedId.HasValue || category.Id != excludedId.Value),
            cancellationToken);
}
