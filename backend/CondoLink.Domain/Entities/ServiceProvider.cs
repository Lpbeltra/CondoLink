using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class ServiceProvider
{
    private ServiceProvider() { }

    public ServiceProvider(string name, string? companyName, string specialty,
        string? contactName, string phone, string? email, string? pixKey,
        ServiceProviderPixKeyType? pixKeyType, string? notes, DateTime now)
    {
        Id = Guid.NewGuid();
        CreatedAt = now;
        IsActive = true;
        Update(name, companyName, specialty, contactName, phone, email, pixKey, pixKeyType, notes, now);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? CompanyName { get; private set; }
    public string Specialty { get; private set; } = null!;
    public string? ContactName { get; private set; }
    public string Phone { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? PixKey { get; private set; }
    public ServiceProviderPixKeyType? PixKeyType { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string? companyName, string specialty,
        string? contactName, string phone, string? email, string? pixKey,
        ServiceProviderPixKeyType? pixKeyType, string? notes, DateTime now)
    {
        Name = Required(name, 160, nameof(name));
        Specialty = Required(specialty, 120, nameof(specialty));
        Phone = Required(phone, 40, nameof(phone));
        CompanyName = Optional(companyName, 160, nameof(companyName));
        ContactName = Optional(contactName, 160, nameof(contactName));
        Email = Optional(email, 254, nameof(email));
        PixKey = Optional(pixKey, 200, nameof(pixKey));
        PixKeyType = PixKey is null ? null : pixKeyType ?? throw new ArgumentException("PIX type is required.");
        Notes = Optional(notes, 2000, nameof(notes));
        IsActive = IsActive || CreatedAt == default;
        UpdatedAt = now;
    }

    public void Activate(DateTime now) { IsActive = true; UpdatedAt = now; }
    public void Deactivate(DateTime now) { IsActive = false; UpdatedAt = now; }
    public void SetStatus(bool active, DateTime now) { IsActive = active; UpdatedAt = now; }

    private static string Required(string value, int max, string name)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max) throw new ArgumentException($"Invalid {name}.");
        return result;
    }
    private static string? Optional(string? value, int max, string name)
    {
        var result = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (result?.Length > max) throw new ArgumentException($"Invalid {name}.");
        return result;
    }
}
