using System.Text.Encodings.Web;
using System.Text.Json;

namespace CondoLink.Api.Features.WhatsApp;

internal sealed record ManagerNewRequestTemplatePayload(
    string CondominiumName,
    string ResidentName,
    string UnitIdentifier,
    string BlockIdentifier,
    string RequestTitle)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string Serialize(ManagerNewRequestTemplatePayload payload)
        => JsonSerializer.Serialize(new[]
        {
            payload.CondominiumName,
            payload.ResidentName,
            payload.UnitIdentifier,
            payload.BlockIdentifier,
            payload.RequestTitle
        }, JsonOptions);

    internal static ManagerNewRequestTemplatePayload Deserialize(string value)
    {
        var fields = JsonSerializer.Deserialize<string[]>(value);
        if (fields is not { Length: 5 })
            throw new InvalidOperationException(
                "Manager new request template payload is invalid.");
        return new(fields[0], fields[1], fields[2], fields[3], fields[4]);
    }
}
