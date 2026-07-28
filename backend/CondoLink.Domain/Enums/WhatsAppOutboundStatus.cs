namespace CondoLink.Domain.Enums;

public enum WhatsAppOutboundStatus
{
    Pending = 1, Processing = 2, Sent = 3, Delivered = 4, Read = 5,
    Failed = 6, PermanentlyFailed = 7, Cancelled = 8, Skipped = 9
}
