namespace CondoLink.Domain.Entities;

public sealed class Condominium
{
    private Condominium()
    {
    }

    public Condominium(string name, string? email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        Name = name.Trim();
        Email = NormalizeOptional(email)?.ToLowerInvariant();
        PhoneNumber = NormalizeOptional(phoneNumber);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? email,
        string? phoneNumber)
    {
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
