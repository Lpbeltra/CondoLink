namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyEmployee
{
    private ManagementCompanyEmployee()
    {
    }

    public ManagementCompanyEmployee(Guid managementCompanyId, Guid userId)
        : this(managementCompanyId, userId, "Não informado") { }

    public ManagementCompanyEmployee(
        Guid managementCompanyId,
        Guid userId,
        string jobTitle)
    {
        if (managementCompanyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Management company id cannot be empty.",
                nameof(managementCompanyId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User id cannot be empty.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(jobTitle))
            throw new ArgumentException("Job title is required.", nameof(jobTitle));
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        ManagementCompanyId = managementCompanyId;
        UserId = userId;
        JobTitle = jobTitle.Trim();
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ManagementCompanyId { get; private set; }
    public ManagementCompany ManagementCompany { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public string JobTitle { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
