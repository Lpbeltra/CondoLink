using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class WhatsAppOutboundMessageTests
{
    [Fact]
    public void Transient_failure_is_bounded_and_manual_retry_is_limited()
    {
        var now = DateTime.UtcNow;
        var message = NewMessage(now);
        message.StartProcessing();
        message.MarkFailure("429", "rate limited", true, 2, now,
            TimeSpan.FromMinutes(1));

        Assert.Equal(WhatsAppOutboundStatus.Pending, message.Status);
        Assert.Equal(now.AddMinutes(1), message.NextAttemptAt);

        message.StartProcessing();
        message.MarkFailure("429", "rate limited", true, 2, now,
            TimeSpan.FromMinutes(2));
        Assert.Equal(WhatsAppOutboundStatus.PermanentlyFailed, message.Status);
        Assert.True(message.RequestManualRetry(now));
        message.MarkFailure("400", "invalid", false, 2, now, TimeSpan.Zero);
        Assert.True(message.RequestManualRetry(now));
        message.MarkFailure("400", "invalid", false, 2, now, TimeSpan.Zero);
        Assert.True(message.RequestManualRetry(now));
        message.MarkFailure("400", "invalid", false, 2, now, TimeSpan.Zero);
        Assert.False(message.RequestManualRetry(now));
    }

    [Fact]
    public void Delivery_status_never_regresses_after_read()
    {
        var now = DateTime.UtcNow;
        var message = NewMessage(now);
        message.StartProcessing();
        message.MarkSent("wamid.1", now);
        message.ApplyProviderStatus("read", now.AddMinutes(2), null, null);
        message.ApplyProviderStatus("delivered", now.AddMinutes(1), null, null);
        message.ApplyProviderStatus("failed", now.AddMinutes(3), "131", "late");

        Assert.Equal(WhatsAppOutboundStatus.Read, message.Status);
        Assert.NotNull(message.ReadAt);
    }

    [Fact]
    public void Parser_normalizes_provider_delivery_status()
    {
        using var json = JsonDocument.Parse("""
        {"entry":[{"changes":[{"value":{"statuses":[{
          "id":"wamid.1","status":"delivered","timestamp":"1750000000"
        }]}}]}]}
        """);
        var status = Assert.Single(
            WhatsAppWebhookParser.ParseStatuses(json.RootElement));

        Assert.Equal("wamid.1", status.ExternalMessageId);
        Assert.Equal("delivered", status.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1750000000).UtcDateTime,
            status.OccurredAt);
    }

    private static WhatsAppOutboundMessage NewMessage(DateTime now) =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "5511999999999", WhatsAppNotificationType.StatusChanged,
            WhatsAppSendMode.SessionText, Guid.NewGuid().ToString(), "updated",
            null, null, now);
}
