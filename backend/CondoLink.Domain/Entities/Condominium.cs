namespace CondoLink.Domain.Entities;

public sealed class Condominium
{
    private Condominium() { }

    public Condominium(string name, string? email, string? legacyPhoneNumber)
        : this(name, email, null, null, null, null, false, false, null) { }

    public Condominium(
        string name, string? email, string? cnpj, string? address, string? city,
        string? state, bool hasDoorman, bool isRemoteDoorman, string? doormanContact)
    {
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        ApplyChanges(name, email, cnpj, address, city, state, hasDoorman,
            isRemoteDoorman, doormanContact);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Cnpj { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public bool HasDoorman { get; private set; }
    public bool IsRemoteDoorman { get; private set; }
    public string? DoormanContact { get; private set; }
    public Guid? ManagementCompanyId { get; private set; }
    public ManagementCompany? ManagementCompany { get; private set; }
    public bool IsActive { get; private set; }
    public bool WhatsAppUpdatesEnabled { get; private set; }
    public string? WhatsAppDisplayName { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void SetActiveStatus(bool isActive) { IsActive = isActive; UpdatedAt = DateTime.UtcNow; }
    public void SetManagementCompany(Guid? id) { ManagementCompanyId = id; UpdatedAt = DateTime.UtcNow; }
    public void ConfigureWhatsAppUpdates(bool enabled, string? displayName)
    {
        WhatsAppUpdatesEnabled = enabled;
        WhatsAppDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? null : displayName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string name, string? email, string? cnpj, string? address, string? city,
        string? state, bool hasDoorman, bool isRemoteDoorman, string? doormanContact)
    {
        ApplyChanges(name, email, cnpj, address, city, state, hasDoorman,
            isRemoteDoorman, doormanContact);
        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyChanges(
        string name, string? email, string? cnpj, string? address, string? city,
        string? state, bool hasDoorman, bool isRemoteDoorman, string? doormanContact)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
        Email = RegistrationData.Optional(email)?.ToLowerInvariant();
        Cnpj = RegistrationData.Digits(cnpj);
        Address = RegistrationData.Optional(address);
        City = RegistrationData.Optional(city);
        State = RegistrationData.State(state);
        HasDoorman = hasDoorman;
        IsRemoteDoorman = hasDoorman && isRemoteDoorman;
        DoormanContact = hasDoorman ? RegistrationData.Optional(doormanContact) : null;
    }
}
