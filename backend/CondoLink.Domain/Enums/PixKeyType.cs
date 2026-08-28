using System.Text.Json.Serialization;

namespace CondoLink.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PixKeyType
{
    Cpf = 1,
    Cnpj = 2,
    Email = 3,
    Phone = 4,
    Random = 5
}
