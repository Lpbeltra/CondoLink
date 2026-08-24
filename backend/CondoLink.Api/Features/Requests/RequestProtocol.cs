namespace CondoLink.Api.Features.Requests;

public static class RequestProtocol
{
    public static string From(Guid requestId) =>
        requestId.ToString("N")[..8].ToUpperInvariant();
}
