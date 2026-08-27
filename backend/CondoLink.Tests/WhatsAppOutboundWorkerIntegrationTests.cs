using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CondoLink.Tests;

public sealed class WhatsAppOutboundWorkerIntegrationTests
{
    [Fact]
    public async Task Worker_sends_all_post_24h_template_contracts_to_fake_client()
    {
        var fake = new CapturingWhatsAppClient();
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { }, builder =>
        {
            builder.Services.AddSingleton<IWhatsAppClient>(fake);
            builder.Services.AddSingleton<IPhoneVerificationMessageProtector,
                PassthroughVerificationProtector>();
            builder.Services.AddSingleton<IFirstAccessWhatsAppPayloadProtector,
                UnusedFirstAccessProtector>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<OperationalTelemetry>();
        });

        await host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial", null, null);
            var user = CoreTestSeed.User("Érica Gonçalves", "erica@example.com");
            user.Update(user.FullName, "+55 11 99999-0001");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new CondoLink.Domain.Entities.Request(condominium.Id,
                user.Id, null, category.Id, "Reparo da iluminação do salão", "Relato");
            var now = DateTime.UtcNow;
            var statusHistory = new RequestStatusHistory(request.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForThirdParty,
                user.Id, "A empresa foi acionada.", now);
            var closureHistory = new RequestStatusHistory(request.Id,
                RequestStatus.InProgress, RequestStatus.WaitingForResidentClosure,
                user.Id, "Serviço concluído literalmente.", now);
            var confirmation = new RequestClosureConfirmation(request.Id,
                closureHistory.Id, closureHistory.Reason!, now);
            db.AddRange(condominium, user, category, request, statusHistory,
                closureHistory, confirmation,
                new WhatsAppOutboundMessage(request.Id, null, user.Id,
                    condominium.Id, user.NormalizedPhoneNumber!,
                    WhatsAppNotificationType.StatusChanged, WhatsAppSendMode.Template,
                    "worker-status", "Atualização completa", "request_status_update",
                    "pt_BR", now, requestStatusHistoryId: statusHistory.Id),
                new WhatsAppOutboundMessage(request.Id, null, user.Id,
                    condominium.Id, user.NormalizedPhoneNumber!,
                    WhatsAppNotificationType.StatusChanged, WhatsAppSendMode.Template,
                    "worker-closure", "Conclusão completa",
                    "resident_closure_confirmation", "pt_BR", now,
                    templateParameterContent: closureHistory.Reason,
                    requestStatusHistoryId: closureHistory.Id,
                    requestClosureConfirmationId: confirmation.Id),
                new WhatsAppOutboundMessage(request.Id, null, user.Id,
                    condominium.Id, user.NormalizedPhoneNumber!,
                    WhatsAppNotificationType.RequestResolved, WhatsAppSendMode.Template,
                    "worker-resolved", "Finalização completa",
                    "task_finalization_notification", "pt_BR", now,
                    templateParameterContent: "Lâmpadas substituídas; serviço concluído."),
                new WhatsAppOutboundMessage(null, null, user.Id,
                    condominium.Id, user.NormalizedPhoneNumber!,
                    WhatsAppNotificationType.ManagerAgendaReminder,
                    WhatsAppSendMode.Template, "worker-agenda", "Lembrete",
                    "manager_agenda_reminder", "pt_BR", now,
                    templateParameterContent: JsonSerializer.Serialize(new[]
                    { "Érica", "Vistoria", "Conferir extintores", "Residencial",
                        "27/08/2026", "09:30" })));
            await db.SaveChangesAsync();
        });

        await host.WithServicesAsync(async services =>
        {
            var options = new WhatsAppOptions
            {
                Enabled = true,
                OutboundBatchSize = 10
            };
            var worker = new WhatsAppOutboundWorker(
                services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(options),
                services.GetRequiredService<OperationalTelemetry>(),
                NullLogger<WhatsAppOutboundWorker>.Instance);
            Assert.Equal(4, await worker.ProcessBatch(options, default));
        });

        var status = Assert.Single(fake.Templates,
            x => x.Name == "request_status_update");
        Assert.Equal(["Érica"], status.Body);
        Assert.Equal(["request_status_view"], status.QuickReplies);

        var closure = Assert.Single(fake.Templates,
            x => x.Name == "resident_closure_confirmation");
        Assert.Equal(["Érica", "Serviço concluído literalmente."], closure.Body);
        Assert.Equal(["closure_confirm", "closure_question"], closure.QuickReplies);

        var resolved = Assert.Single(fake.Templates,
            x => x.Name == "task_finalization_notification");
        Assert.Equal(["Érica", "Reparo da iluminação do salão", "FINALIZADA",
            "Lâmpadas substituídas; serviço concluído."], resolved.Body);
        Assert.Empty(resolved.QuickReplies);
        Assert.Empty(resolved.UrlButtons);
        var agenda = Assert.Single(fake.Templates,
            x => x.Name == "manager_agenda_reminder");
        Assert.Equal(["Érica", "Vistoria", "Conferir extintores", "Residencial",
            "27/08/2026", "09:30"], agenda.Body);
        Assert.Empty(agenda.QuickReplies);
        Assert.Equal(4, await host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.CountAsync()));
    }

    private sealed class CapturingWhatsAppClient : IWhatsAppClient
    {
        public List<TemplateCall> Templates { get; } = [];
        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber,
            string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppMediaResult> DownloadMediaAsync(string mediaId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber,
            string templateName, string language, IReadOnlyList<string> bodyParameters,
            IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName = null) => SendTemplateAsync(phoneNumber,
                templateName, language, bodyParameters, quickReplyPayloads,
                cancellationToken, bodyParameterName, []);
        public Task<WhatsAppSendResult> SendTemplateAsync(string phoneNumber,
            string templateName, string language, IReadOnlyList<string> bodyParameters,
            IReadOnlyList<string> quickReplyPayloads, CancellationToken cancellationToken,
            string? bodyParameterName, IReadOnlyList<string> urlButtonParameters)
        {
            Templates.Add(new(templateName, [.. bodyParameters],
                [.. quickReplyPayloads], [.. urlButtonParameters]));
            return Task.FromResult(new WhatsAppSendResult(true,
                $"wamid.fake-{Templates.Count}", null));
        }
    }

    private sealed record TemplateCall(string Name, IReadOnlyList<string> Body,
        IReadOnlyList<string> QuickReplies, IReadOnlyList<string> UrlButtons);
    private sealed class PassthroughVerificationProtector : IPhoneVerificationMessageProtector
    {
        public string Protect(string message) => message;
        public string Unprotect(string protectedMessage) => protectedMessage;
    }
    private sealed class UnusedFirstAccessProtector : IFirstAccessWhatsAppPayloadProtector
    {
        public string Protect(FirstAccessWhatsAppPayload payload) => throw new NotSupportedException();
        public FirstAccessWhatsAppPayload Unprotect(string value) => throw new NotSupportedException();
    }
}
