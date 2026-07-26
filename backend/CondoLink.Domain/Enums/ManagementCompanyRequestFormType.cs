using System.Text.Json.Serialization;

namespace CondoLink.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagementCompanyRequestFormType
{
    Generic,
    SupplierPayment,
    UnitFine,
    Reimbursement
}
