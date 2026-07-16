using CondoLink.Domain.Common;

namespace CondoLink.Domain.Entities;

public class User : AuditableEntity, IAggregateRoot
{
    protected User()
    {
        FullName = string.Empty;
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        PhoneNumber = string.Empty;
    }

    public User(string fullName, string email, string phoneNumber)
    {
        FullName = Guard.AgainstNullOrWhiteSpace(fullName, nameof(fullName));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        NormalizedEmail = Email.ToUpperInvariant();
        PhoneNumber = Guard.AgainstNullOrWhiteSpace(
            phoneNumber,
            nameof(phoneNumber));
        IsActive = true;
    }

    public string FullName { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeFullName(string fullName)
    {
        var normalizedFullName = Guard.AgainstNullOrWhiteSpace(
            fullName,
            nameof(fullName));

        if (string.Equals(FullName, normalizedFullName, StringComparison.Ordinal))
        {
            return;
        }

        FullName = normalizedFullName;
        Touch();
    }

    public void ChangeEmail(string email)
    {
        var validatedEmail = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        var normalizedEmail = validatedEmail.ToUpperInvariant();

        if (string.Equals(
                NormalizedEmail,
                normalizedEmail,
                StringComparison.Ordinal))
        {
            return;
        }

        Email = validatedEmail;
        NormalizedEmail = normalizedEmail;
        Touch();
    }

    public void ChangePhoneNumber(string phoneNumber)
    {
        var normalizedPhoneNumber = Guard.AgainstNullOrWhiteSpace(
            phoneNumber,
            nameof(phoneNumber));

        if (string.Equals(
                PhoneNumber,
                normalizedPhoneNumber,
                StringComparison.Ordinal))
        {
            return;
        }

        PhoneNumber = normalizedPhoneNumber;
        Touch();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Touch();
    }
}
