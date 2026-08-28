namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyRequestCategoryResponsible
{
    private ManagementCompanyRequestCategoryResponsible() { }
    public ManagementCompanyRequestCategoryResponsible(Guid categoryId, Guid accessId)
    {
        if (categoryId == Guid.Empty || accessId == Guid.Empty)
            throw new ArgumentException("Category and access are required.");
        Id = Guid.NewGuid();
        ManagementCompanyRequestCategoryId = categoryId;
        ManagementCompanyEmployeeId = accessId;
        AssignedAt = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid ManagementCompanyRequestCategoryId { get; private set; }
    public ManagementCompanyRequestCategory Category { get; private set; } = null!;
    public Guid ManagementCompanyEmployeeId { get; private set; }
    public ManagementCompanyEmployee Access { get; private set; } = null!;
    public DateTime AssignedAt { get; private set; }
}
