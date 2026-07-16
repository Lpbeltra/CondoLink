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

    public string FullName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
