using CondoLink.Domain;

namespace CondoLink.Domain.Entities;

public sealed class Employee
{
    private Employee() { }

    public Employee(Guid condominiumId, string fullName, string? jobTitle, string? phoneNumber,
        string? email, string? registrationNumber, DateOnly? admissionDate)
        : this(condominiumId, fullName, null, jobTitle, phoneNumber, email, registrationNumber, admissionDate) { }

    public Employee(Guid condominiumId, string fullName, string? cpf, string? jobTitle, string? phoneNumber,
        string? email, string? registrationNumber, DateOnly? admissionDate)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
        ApplyChanges(fullName, cpf, jobTitle, phoneNumber, email, registrationNumber, admissionDate);
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string? Cpf { get; private set; }
    public string? NormalizedCpf { get; private set; }
    public string? JobTitle { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? NormalizedPhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? NormalizedRegistrationNumber { get; private set; }
    public DateOnly? AdmissionDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string fullName, string? cpf, string? jobTitle, string? phoneNumber, string? email,
        string? registrationNumber, DateOnly? admissionDate)
    {
        ApplyChanges(fullName, cpf, jobTitle, phoneNumber, email, registrationNumber, admissionDate);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string fullName, string? jobTitle, string? phoneNumber, string? email,
        string? registrationNumber, DateOnly? admissionDate) =>
        Update(fullName, null, jobTitle, phoneNumber, email, registrationNumber, admissionDate);

    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void MoveToCondominium(Guid condominiumId)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
        CondominiumId = condominiumId;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyChanges(string fullName, string? cpf, string? jobTitle, string? phoneNumber, string? email,
        string? registrationNumber, DateOnly? admissionDate)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (phoneNumber is not null && normalizedPhoneNumber is null)
            throw new ArgumentException(
                "Phone number is invalid; include the international country code when outside Brazil.",
                nameof(phoneNumber));

        FullName = fullName.Trim();
        var normalizedCpf = RegistrationData.Digits(cpf);
        if (cpf is not null && !RegistrationData.IsValidCpf(cpf))
            throw new ArgumentException("CPF inválido.", nameof(cpf));
        Cpf = normalizedCpf is null ? null : cpf!.Trim();
        NormalizedCpf = normalizedCpf;
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        PhoneNumber = normalizedPhoneNumber is null ? null : phoneNumber!.Trim();
        NormalizedPhoneNumber = normalizedPhoneNumber;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        RegistrationNumber = string.IsNullOrWhiteSpace(registrationNumber) ? null : registrationNumber.Trim();
        NormalizedRegistrationNumber = RegistrationNumber?.ToUpperInvariant();
        AdmissionDate = admissionDate;
    }
}
