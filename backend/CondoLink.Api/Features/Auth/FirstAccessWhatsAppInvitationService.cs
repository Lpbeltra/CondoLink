using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Auth;

public interface IFirstAccessWhatsAppPayloadProtector
{
    string Protect(FirstAccessWhatsAppPayload payload);
    FirstAccessWhatsAppPayload Unprotect(string value);
}

public sealed record FirstAccessWhatsAppPayload(
    string ResidentName, string CondominiumName, string ButtonParameter);
public sealed record FirstAccessCombinedDeliveryResult(
    bool EmailSent, bool WhatsAppQueued, bool AlreadyProcessed = false);

internal sealed class FirstAccessWhatsAppPayloadProtector(IDataProtectionProvider provider)
    : IFirstAccessWhatsAppPayloadProtector
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("Comvy.WhatsApp.ResidentFirstAccess.v1");

    public string Protect(FirstAccessWhatsAppPayload payload) =>
        _protector.Protect(JsonSerializer.Serialize(payload));

    public FirstAccessWhatsAppPayload Unprotect(string value) =>
        JsonSerializer.Deserialize<FirstAccessWhatsAppPayload>(_protector.Unprotect(value))
        ?? throw new InvalidOperationException("Invalid first access payload.");
}

public sealed class FirstAccessWhatsAppInvitationService(
    FirstAccessService firstAccess,
    IFirstAccessWhatsAppPayloadProtector protector,
    IOptions<WhatsAppOptions> options,
    AppDbContext db,
    ILogger<FirstAccessWhatsAppInvitationService> logger)
{
    public async Task<bool> EnqueueAsync(
        ApplicationUser user, Guid condominiumId, string condominiumName,
        string operationId, CancellationToken ct)
    {
        if (!user.MustChangePassword || string.IsNullOrWhiteSpace(user.NormalizedPhoneNumber))
            return false;
        try
        {
            if (await IsEnqueuedAsync(user.Id, operationId, ct)) return true;
            var link = await firstAccess.CreateLinkAsync(user);
            return await EnqueueLinkAsync(user, condominiumId, condominiumName,
                operationId, link, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            LogFailure(exception, user.Id, condominiumId);
            return false;
        }
    }

    public async Task<bool> EnqueueLinkAsync(
        ApplicationUser user, Guid condominiumId, string condominiumName,
        string operationId, string link, CancellationToken ct)
    {
        if (!user.MustChangePassword || string.IsNullOrWhiteSpace(user.NormalizedPhoneNumber))
            return false;
        var template = options.Value.Templates.ResidentFirstAccess;
        if (string.IsNullOrWhiteSpace(template.Name) || string.IsNullOrWhiteSpace(template.Language))
            return false;

        try
        {
            var key = $"first-access:{user.Id:N}:{operationId}";
            if (await db.WhatsAppOutboundMessages.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == key, ct)) return true;

            var buttonParameter = DynamicButtonParameter(link);
            var content = protector.Protect(new(
                user.FullName, condominiumName, buttonParameter));
            db.WhatsAppOutboundMessages.Add(new WhatsAppOutboundMessage(
                null, null, user.Id, condominiumId, user.NormalizedPhoneNumber,
                WhatsAppNotificationType.ResidentFirstAccess, WhatsAppSendMode.Template,
                key, content, template.Name, template.Language, DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogFailure(exception, user.Id, condominiumId);
            return false;
        }
    }

    public Task<bool> IsEnqueuedAsync(Guid userId, string operationId, CancellationToken ct)
    {
        var key = $"first-access:{userId:N}:{operationId}";
        return db.WhatsAppOutboundMessages.AsNoTracking()
            .AnyAsync(x => x.IdempotencyKey == key, ct);
    }

    public async Task<FirstAccessCombinedDeliveryResult> DeliverBothAsync(
        ApplicationUser user, Guid condominiumId, string condominiumName,
        string operationId, CancellationToken ct)
    {
        if (await IsEnqueuedAsync(user.Id, operationId, ct))
            return new(false, true, true);
        var link = await firstAccess.CreateLinkAsync(user);
        var emailSent = await firstAccess.SendLinkAsync(user, condominiumName, link, ct);
        var whatsappQueued = await EnqueueLinkAsync(user, condominiumId,
            condominiumName, operationId, link, ct);
        return new(emailSent, whatsappQueued);
    }

    private void LogFailure(Exception exception, Guid userId, Guid condominiumId) =>
        logger.LogWarning(exception,
            "First-access WhatsApp enqueue failed for UserId {UserId} and CondominiumId {CondominiumId}.",
            userId, condominiumId);

    internal static string DynamicButtonParameter(string link)
    {
        const string marker = "/primeiro-acesso";
        var markerIndex = link.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) throw new InvalidOperationException("Invalid first access URL.");
        return link[(markerIndex + marker.Length)..];
    }
}
