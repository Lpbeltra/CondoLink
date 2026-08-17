using Microsoft.AspNetCore.Identity;
using CondoLink.Domain;

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
        SetPhoneNumber(phoneNumber);
        IsActive = true;
        MustChangePassword = false;
        ReceiveWhatsAppUpdates = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string FullName { get; private set; }
    public string? NormalizedPhoneNumber { get; private set; }
    public string? Cpf { get; private set; }
    public string? Cnpj { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public bool IsActive { get; private set; }
    public bool MustChangePassword { get; private set; }
    public bool ReceiveWhatsAppUpdates { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? PasswordChangedAt { get; private set; }
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
        SetPhoneNumber(phoneNumber);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateManagerProfile(
        string fullName,
        string? phoneNumber,
        string? cpf,
        string? cnpj,
        string? address,
        string? city,
        string? state)
    {
        Update(fullName, phoneNumber);
        Cpf = Domain.RegistrationData.Digits(cpf);
        Cnpj = Domain.RegistrationData.Digits(cnpj);
        Address = Domain.RegistrationData.Optional(address);
        City = Domain.RegistrationData.Optional(city);
        State = Domain.RegistrationData.State(state);
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequirePasswordChange()
    {
        MustChangePassword = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPasswordChanged(DateTime changedAt)
    {
        MustChangePassword = false;
        PasswordChangedAt = changedAt;
        UpdatedAt = changedAt;
    }

    public void MarkSuccessfulLogin(DateTime loginAt)
    {
        LastLoginAt = loginAt;
        UpdatedAt = loginAt;
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

    public void SetReceiveWhatsAppUpdates(bool enabled)
    {
        ReceiveWhatsAppUpdates = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmPhoneNumber()
    {
        if (NormalizedPhoneNumber is null)
            throw new InvalidOperationException("A phone number is required.");
        PhoneNumberConfirmed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private void SetPhoneNumber(string? phoneNumber)
    {
        var previousNormalizedPhoneNumber = NormalizedPhoneNumber;
        var displayPhoneNumber = NormalizeOptional(phoneNumber);
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(displayPhoneNumber);
        if (displayPhoneNumber is not null && normalizedPhoneNumber is null)
        {
            throw new ArgumentException(
                "Phone number is invalid; include the international country code when outside Brazil.",
                nameof(phoneNumber));
        }

        PhoneNumber = displayPhoneNumber;
        NormalizedPhoneNumber = normalizedPhoneNumber;
        if (!string.Equals(
                previousNormalizedPhoneNumber,
                normalizedPhoneNumber,
                StringComparison.Ordinal))
            PhoneNumberConfirmed = false;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed;
    }
}
