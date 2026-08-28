namespace CondoLink.Domain.Entities;

public sealed class CondominiumManagementCompanyLink
{
    private CondominiumManagementCompanyLink() { }
    public CondominiumManagementCompanyLink(Guid condominiumId, Guid managementCompanyId)
    {
        if (condominiumId == Guid.Empty || managementCompanyId == Guid.Empty)
            throw new ArgumentException("Condominium and management company are required.");
        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        ManagementCompanyId = managementCompanyId;
        IsActive = true;
        LinkedAt = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public Condominium Condominium { get; private set; } = null!;
    public Guid ManagementCompanyId { get; private set; }
    public ManagementCompany ManagementCompany { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime LinkedAt { get; private set; }
    public DateTime? UnlinkedAt { get; private set; }
    public void Unlink(DateTime at) { IsActive = false; UnlinkedAt = at; }
}
