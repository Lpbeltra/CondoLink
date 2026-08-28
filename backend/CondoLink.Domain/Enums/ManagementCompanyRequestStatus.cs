using System.Text.Json.Serialization;
namespace CondoLink.Domain.Enums;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagementCompanyRequestStatus
{
    Submitted = 1, Acknowledged = 2, InProgress = 3,
    WaitingManager = 4, Completed = 5, Cancelled = 6
}
