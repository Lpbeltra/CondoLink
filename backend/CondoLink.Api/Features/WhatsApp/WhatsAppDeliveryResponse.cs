using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.WhatsApp;

public sealed record WhatsAppDeliveryResponse(string Status)
{
    public static WhatsAppDeliveryResponse? FromStatus(WhatsAppOutboundStatus? status) =>
        status.HasValue ? new(status.Value.ToString()) : null;
}
