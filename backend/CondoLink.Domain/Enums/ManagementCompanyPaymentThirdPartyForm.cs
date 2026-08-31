using System.Text.Json.Serialization;

namespace CondoLink.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagementCompanyPaymentThirdPartyForm
{
    Pix = 1,
    Boleto = 2,
    DepositAccount = 3
}
