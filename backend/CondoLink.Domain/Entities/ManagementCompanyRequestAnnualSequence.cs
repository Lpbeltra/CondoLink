namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyRequestAnnualSequence
{
    private ManagementCompanyRequestAnnualSequence() { }
    public int Year { get; private set; }
    public long LastValue { get; private set; }
}
