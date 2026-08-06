using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class WhatsAppOutboundWorkerTests
{
    [Fact]
    public void Meta_diagnostic_is_persisted_and_permanent_failure_stops_retry()
    {
        var message = Message();
        message.StartProcessing();
        var result = new WhatsAppSendResult(false, null,
            "Meta HTTP 400; type=OAuthException; code=132001; details=Template does not exist",
            false, "132001", 400, "OAuthException", null);

        WhatsAppOutboundWorker.ApplyFailure(message, result,
            new WhatsAppOptions { OutboundMaxAttempts = 5 }, DateTime.UtcNow);

        Assert.Equal(WhatsAppOutboundStatus.PermanentlyFailed, message.Status);
        Assert.Equal("132001", message.LastErrorCode);
        Assert.Equal(result.Error, message.LastErrorDescription);
        Assert.Null(message.NextAttemptAt);
    }

    [Theory]
    [InlineData("timeout", null)]
    [InlineData("network", null)]
    [InlineData("4", 429)]
    [InlineData("2", 500)]
    public void Transient_failure_is_scheduled_for_retry(
        string code, int? httpStatus)
    {
        var now = DateTime.UtcNow;
        var message = Message();
        message.StartProcessing();
        var result = new WhatsAppSendResult(false, null, "Technical failure",
            true, code, httpStatus);

        WhatsAppOutboundWorker.ApplyFailure(message, result,
            new WhatsAppOptions
            {
                OutboundMaxAttempts = 5,
                OutboundInitialRetrySeconds = 30
            }, now);

        Assert.Equal(WhatsAppOutboundStatus.Pending, message.Status);
        Assert.Equal(code, message.LastErrorCode);
        Assert.Equal("Technical failure", message.LastErrorDescription);
        Assert.Equal(now.AddSeconds(30), message.NextAttemptAt);
    }

    private static WhatsAppOutboundMessage Message() => new(
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
        "+5511999990001", WhatsAppNotificationType.InformationRequested,
        WhatsAppSendMode.Template, $"worker:{Guid.NewGuid():N}", "content",
        "message_warning", "pt_BR", DateTime.UtcNow);
}
