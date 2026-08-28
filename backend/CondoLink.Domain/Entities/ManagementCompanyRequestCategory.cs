using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyRequestCategory
{
    private ManagementCompanyRequestCategory()
    {
    }

    public ManagementCompanyRequestCategory(
        Guid managementCompanyId,
        string name,
        string? description,
        ManagementCompanyRequestFormType formType)
    {
        if (managementCompanyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Management company id is required.",
                nameof(managementCompanyId));
        }

        var now = DateTimeOffset.UtcNow;
        Id = Guid.NewGuid();
        ManagementCompanyId = managementCompanyId;
        ApplyChanges(name, description, formType);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ManagementCompanyId { get; private set; }
    public ManagementCompany ManagementCompany { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public string? Description { get; private set; }
    public ManagementCompanyRequestFormType FormType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<ManagementCompanyRequestCategoryResponsible> Responsibles { get; private set; } = [];

    public void Update(
        string name,
        string? description,
        ManagementCompanyRequestFormType formType)
    {
        ApplyChanges(name, description, formType);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void ApplyChanges(
        string name,
        string? description,
        ManagementCompanyRequestFormType formType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var normalizedName = name.Trim();
        if (normalizedName.Length > 150)
            throw new ArgumentException(
                "Name must not exceed 150 characters.",
                nameof(name));

        var normalizedDescription = NormalizeOptional(description);
        if (normalizedDescription?.Length > 500)
            throw new ArgumentException(
                "Description must not exceed 500 characters.",
                nameof(description));

        if (!Enum.IsDefined(formType))
            throw new ArgumentOutOfRangeException(
                nameof(formType),
                "Form type is invalid.");

        Name = normalizedName;
        NormalizedName = normalizedName.ToUpperInvariant();
        Description = normalizedDescription;
        FormType = formType;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
