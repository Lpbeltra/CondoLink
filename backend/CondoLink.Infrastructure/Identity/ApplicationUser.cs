using Microsoft.AspNetCore.Identity;

namespace CondoLink.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(string fullName, string email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var now = DateTime.UtcNow;
        var normalizedEmail = email.Trim().ToLowerInvariant();

        Id = Guid.NewGuid();
        FullName = fullName.Trim();
        UserName = normalizedEmail;
        Email = normalizedEmail;
        PhoneNumber = NormalizeOptional(phoneNumber);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string FullName { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? ActiveManagementCondominiumId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string fullName,
        string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException(
                "Full name is required.",
                nameof(fullName));
        }

        FullName = fullName.Trim();
        PhoneNumber = NormalizeOptional(phoneNumber);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActiveManagementCondominium(Guid condominiumId)
    {
        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException(
                "Condominium id cannot be empty.",
                nameof(condominiumId));
        }

        ActiveManagementCondominiumId = condominiumId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearActiveManagementCondominium()
    {
        ActiveManagementCondominiumId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed;
    }
}