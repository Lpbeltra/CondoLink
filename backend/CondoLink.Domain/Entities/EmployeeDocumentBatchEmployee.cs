namespace CondoLink.Domain.Entities;

public sealed class EmployeeDocumentBatchEmployee
{
    private EmployeeDocumentBatchEmployee() { }
    public EmployeeDocumentBatchEmployee(Guid batchId, Guid employeeId)
    { Id = Guid.NewGuid(); BatchId = batchId; EmployeeId = employeeId; }
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid EmployeeId { get; private set; }
}
