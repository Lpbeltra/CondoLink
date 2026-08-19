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
        var template = options.Value.Templates.ResidentFirstAccess;
        if (string.IsNullOrWhiteSpace(template.Name) || string.IsNullOrWhiteSpace(template.Language))
            return false;

        try
        {
            var key = $"first-access:{user.Id:N}:{operationId}";
            if (await db.WhatsAppOutboundMessages.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == key, ct)) return true;

            var link = await firstAccess.CreateLinkAsync(user);
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
            logger.LogWarning(exception,
                "First-access WhatsApp enqueue failed for UserId {UserId} and CondominiumId {CondominiumId}.",
                user.Id, condominiumId);
            return false;
        }
    }

    internal static string DynamicButtonParameter(string link)
    {
        const string marker = "/primeiro-acesso";
        var markerIndex = link.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) throw new InvalidOperationException("Invalid first access URL.");
        return link[(markerIndex + marker.Length)..];
    }
}
