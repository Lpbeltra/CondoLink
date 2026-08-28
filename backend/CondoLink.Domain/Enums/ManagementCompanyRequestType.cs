using System.Text.Json.Serialization;
namespace CondoLink.Domain.Enums;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagementCompanyRequestType { Fine = 1, Payment = 2, GeneralQuestion = 3 }
