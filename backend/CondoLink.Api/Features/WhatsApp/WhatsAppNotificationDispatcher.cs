using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppNotificationDispatcher(
    AppDbContext db,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppNotificationDispatcher> logger)
{
    public async Task EnqueueAsync(
        Guid requestId,
        WhatsAppNotificationType type,
        string idempotencyKey,
        string content,
        Guid? requestMessageId,
        CancellationToken ct)
    {
        var request = await db.Requests.AsNoTracking()
            .SingleAsync(x => x.Id == requestId, ct);
        if (await db.WhatsAppOutboundMessages.AsNoTracking()
            .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct)) return;
        var settings = options.Value;
        var condominium = await db.Condominiums.AsNoTracking()
            .Where(x => x.Id == request.CondominiumId)
            .Select(x => new { x.WhatsAppUpdatesEnabled }).SingleAsync(ct);
        var user = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.Id == request.AuthorUserId)
            .Select(x => new
            {
                x.IsActive, x.PhoneNumber, x.ReceiveWhatsAppUpdates
            }).SingleOrDefaultAsync(ct);
        var phone = PhoneNumberNormalizer.NormalizeBrazilian(user?.PhoneNumber);
        var activeMembership = user is not null && await db.CondominiumMemberships
            .AsNoTracking().AnyAsync(x => x.UserId == request.AuthorUserId
                && x.CondominiumId == request.CondominiumId
                && x.IsActive && x.EndedAt == null, ct);
        var ambiguous = phone is not null && (await db.Set<ApplicationUser>()
            .AsNoTracking().Where(x => x.IsActive && x.PhoneNumber != null)
            .Select(x => x.PhoneNumber).ToArrayAsync(ct))
            .Count(x => PhoneNumberNormalizer.NormalizeBrazilian(x) == phone) > 1;

        var lastInboundAt = phone is null ? null : await db.WhatsAppInboundMessages
            .AsNoTracking().Where(x => x.PhoneNumber == phone)
            .MaxAsync(x => (DateTime?)x.ReceivedAt, ct);
        var sessionOpen = lastInboundAt >= DateTime.UtcNow.AddHours(-24);
        var template = TemplateFor(type, settings.Templates);
        var mode = sessionOpen ? WhatsAppSendMode.SessionText : WhatsAppSendMode.Template;
        var skipReason = !settings.Enabled ? "Integração desabilitada."
            : !condominium.WhatsAppUpdatesEnabled ? "Condomínio desabilitado."
            : user is null || !user.IsActive ? "Usuário inativo."
            : !user.ReceiveWhatsAppUpdates ? "Preferência desabilitada."
            : phone is null ? "Telefone inválido."
            : ambiguous ? "Telefone ambíguo."
            : !activeMembership ? "Vínculo inativo."
            : mode == WhatsAppSendMode.Template
                && (string.IsNullOrWhiteSpace(template.Name)
                    || string.IsNullOrWhiteSpace(template.Language))
                ? "Template não configurado."
                : null;
        var status = skipReason is null
            ? WhatsAppOutboundStatus.Pending : WhatsAppOutboundStatus.Skipped;
        db.WhatsAppOutboundMessages.Add(new WhatsAppOutboundMessage(
            request.Id, requestMessageId, request.AuthorUserId,
            request.CondominiumId, phone ?? string.Empty, type, mode,
            idempotencyKey, content, template.Name, template.Language,
            DateTime.UtcNow, status, skipReason));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (await db.WhatsAppOutboundMessages.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct)) return;
            throw;
        }
        if (skipReason is not null)
            logger.LogInformation("WhatsApp outbound {Key} skipped: {Reason}.",
                idempotencyKey, skipReason);
    }

    private static WhatsAppTemplateDefinition TemplateFor(
        WhatsAppNotificationType type, WhatsAppTemplateOptions templates) =>
        type switch
        {
            WhatsAppNotificationType.AdministrationMessage =>
                templates.AdministrationMessage,
            WhatsAppNotificationType.InformationRequested =>
                templates.InformationRequested,
            WhatsAppNotificationType.RequestResolved => templates.Resolved,
            WhatsAppNotificationType.RequestCancelled => templates.Cancelled,
            WhatsAppNotificationType.RequestReopened => templates.Reopened,
            _ => templates.StatusChanged
        };
}
