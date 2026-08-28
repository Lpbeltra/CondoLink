using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequestCategories;

public sealed record ManagementCompanyRequestCategoryRequest(
    string? Name,
    string? Description,
    ManagementCompanyRequestFormType FormType);

public sealed record ManagementCompanyRequestCategoryResponse(
    Guid Id,
    Guid ManagementCompanyId,
    string Name,
    string? Description,
    ManagementCompanyRequestFormType FormType,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid>? ResponsibleAccessIds = null)
{
    public static ManagementCompanyRequestCategoryResponse From(
        ManagementCompanyRequestCategory category) =>
        new(
            category.Id,
            category.ManagementCompanyId,
            category.Name,
            category.Description,
            category.FormType,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);
}

internal static class ManagementCompanyRequestCategoryValidation
{
    public static string? Validate(
        ManagementCompanyRequestCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (request.Name.Trim().Length > 150)
            return "Name must not exceed 150 characters.";
        if (request.Description?.Trim().Length > 500)
            return "Description must not exceed 500 characters.";
        if (!Enum.IsDefined(request.FormType))
            return "Form type is invalid.";
        return null;
    }
}
