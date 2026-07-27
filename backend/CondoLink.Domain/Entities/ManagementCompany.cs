namespace CondoLink.Domain.Entities;

public sealed class ManagementCompany
{
    private ManagementCompany() { }

    public ManagementCompany(
        string name, string? legacyLegalName, string? legacyDocument,
        string? email, string? phoneNumber)
        : this(name, legacyDocument, null, null, null, email, phoneNumber) { }

    public ManagementCompany(
        string name, string? cnpj, string? address, string? city, string? state,
        string? email, string? phoneNumber)
    {
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        ApplyChanges(name, cnpj, address, city, state, email, phoneNumber);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Cnpj { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public ICollection<Condominium> Condominiums { get; private set; } = [];
    public ICollection<ManagementCompanyEmployee> Employees { get; private set; } = [];
    public ICollection<ManagementCompanyRequestCategory> RequestCategories { get; private set; } = [];

    public void Update(
        string name, string? cnpj, string? address, string? city, string? state,
        string? email, string? phoneNumber)
    {
        ApplyChanges(name, cnpj, address, city, state, email, phoneNumber);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyChanges(
        string name, string? cnpj, string? address, string? city, string? state,
        string? email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
        Cnpj = RegistrationData.Digits(cnpj);
        Address = RegistrationData.Optional(address);
        City = RegistrationData.Optional(city);
        State = RegistrationData.State(state);
        Email = RegistrationData.Optional(email)?.ToLowerInvariant();
        PhoneNumber = RegistrationData.Optional(phoneNumber);
    }
}
