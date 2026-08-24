using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class WhatsAppOutboundWorkerTests
{
    [Fact]
    public void Manager_new_request_template_maps_exactly_five_positional_values()
    {
        var payload = ManagerNewRequestTemplatePayload.Serialize(new(
            "Residencial Monticello", "Tatiana Custódio", "1201", "1",
            "TAG da garagem"));

        Assert.Equal(
            ["Residencial Monticello", "Tatiana Custódio", "1201", "1",
                "TAG da garagem"],
            WhatsAppOutboundWorker.ManagerNewRequestTemplateParameters(payload));
    }

    [Fact]
    public void Manager_new_request_template_uses_dash_when_block_is_absent()
    {
        var payload = ManagerNewRequestTemplatePayload.Serialize(new(
            "Residencial Monticello", "Tatiana Custódio", "1201", "-",
            "TAG da garagem"));

        Assert.Equal("-", WhatsAppOutboundWorker
            .ManagerNewRequestTemplateParameters(payload)[3]);
    }

    [Fact]
    public void Status_template_maps_exactly_the_resident_first_name()
    {
        Assert.Equal(["Tatiana"],
            WhatsAppOutboundWorker.StatusChangedTemplateParameters(
                "Tatiana Custodio"));
    }

    [Fact]
    public void Closure_template_maps_name_and_literal_conclusion()
    {
        Assert.Equal(["Tatiana", "Serviço concluído literalmente."],
            WhatsAppOutboundWorker.ClosureTemplateParameters(
                "Tatiana Custodio", "Serviço concluído literalmente."));
    }

    [Fact]
    public void Finalization_template_maps_exactly_four_utf8_positional_values()
    {
        Assert.Equal(
            ["Érica", "Reparo da iluminação do salão", "FINALIZADA",
                "Lâmpadas substituídas; serviço concluído."],
            WhatsAppOutboundWorker.FinalizationTemplateParameters(
                "Érica Gonçalves", "Reparo da iluminação do salão",
                "Lâmpadas substituídas; serviço concluído."));
    }

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

    [Fact]
    public void Empty_failure_is_normalized_before_becoming_permanent()
    {
        var message = Message();
        message.StartProcessing();

        WhatsAppOutboundWorker.ApplyFailure(message,
            new WhatsAppSendResult(false, null, null),
            new WhatsAppOptions { OutboundMaxAttempts = 5 }, DateTime.UtcNow);

        Assert.Equal(WhatsAppOutboundStatus.PermanentlyFailed, message.Status);
        Assert.Equal("undiagnosed_failure", message.LastErrorCode);
        Assert.Equal(
            "Client returned a failure without a technical description.",
            message.LastErrorDescription);
        var normalized = WhatsAppOutboundWorker.EnsureFailureDiagnostic(
            new WhatsAppSendResult(false, null, null));
        Assert.Equal("UndiagnosedClientFailure", normalized.FailureKind);
        Assert.Equal("worker_received_result", normalized.FailureStage);
    }

    private static WhatsAppOutboundMessage Message() => new(
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
        "+5511999990001", WhatsAppNotificationType.InformationRequested,
        WhatsAppSendMode.Template, $"worker:{Guid.NewGuid():N}", "content",
        "message_warning", "pt_BR", DateTime.UtcNow);
}
