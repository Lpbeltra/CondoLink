namespace CondoLink.Domain.Entities;

public sealed class ManagementCompany
{
    private ManagementCompany()
    {
    }

    public ManagementCompany(
        string name,
        string? legalName,
        string? document,
        string? email,
        string? phoneNumber)
    {
        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        ApplyChanges(name, legalName, document, email, phoneNumber);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? LegalName { get; private set; }
    public string? Document { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public ICollection<Condominium> Condominiums { get; private set; } =
        new List<Condominium>();
    public ICollection<ManagementCompanyEmployee> Employees { get; private set; } =
        new List<ManagementCompanyEmployee>();
    public ICollection<ManagementCompanyRequestCategory> RequestCategories
        { get; private set; } =
        new List<ManagementCompanyRequestCategory>();

    public void Update(
        string name,
        string? legalName,
        string? document,
        string? email,
        string? phoneNumber)
    {
        ApplyChanges(name, legalName, document, email, phoneNumber);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyChanges(
        string name,
        string? legalName,
        string? document,
        string? email,
        string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name.Trim();
        LegalName = NormalizeOptional(legalName);
        Document = NormalizeOptional(document);
        Email = NormalizeOptional(email)?.ToLowerInvariant();
        PhoneNumber = NormalizeOptional(phoneNumber);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
