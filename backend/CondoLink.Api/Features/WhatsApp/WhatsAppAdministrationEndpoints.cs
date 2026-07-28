using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public static class WhatsAppAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppAdministration(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/management/condominiums/{condominiumId:guid}/whatsapp")
            .RequireAuthorization()
            .WithTags("WhatsApp administration");
        group.MapGet("/outbound", ListAsync);
        group.MapPost("/outbound/{messageId:guid}/retry", RetryAsync);
        group.MapPut("/settings", ConfigureAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid condominiumId, string? status, int? take,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var authorization = await AuthorizeManagerAsync(
            condominiumId, principal, db, ct);
        if (authorization is not null) return authorization;

        WhatsAppOutboundStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WhatsAppOutboundStatus>(status, true, out var value))
                return Results.BadRequest(new { error = "Invalid outbound status." });
            parsed = value;
        }

        var query = db.WhatsAppOutboundMessages.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId);
        if (parsed.HasValue) query = query.Where(x => x.Status == parsed.Value);

        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take ?? 50, 1, 100))
            .Select(x => new
            {
                x.Id, x.RequestId, x.RequestMessageId, x.UserId,
                x.NotificationType, x.SendMode, x.Status, x.AttemptCount,
                x.ManualRetryCount, x.CreatedAt, x.SentAt, x.DeliveredAt,
                x.ReadAt, x.FailedAt, x.NextAttemptAt, x.LastErrorCode,
                x.LastErrorDescription
            }).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> RetryAsync(
        Guid condominiumId, Guid messageId, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct)
    {
        var authorization = await AuthorizeManagerAsync(
            condominiumId, principal, db, ct);
        if (authorization is not null) return authorization;
        var message = await db.WhatsAppOutboundMessages.SingleOrDefaultAsync(
            x => x.Id == messageId && x.CondominiumId == condominiumId, ct);
        if (message is null) return Results.NotFound();
        if (!message.RequestManualRetry(DateTime.UtcNow))
            return Results.Conflict(new
            {
                error = "This message cannot be retried or reached the retry limit."
            });
        await db.SaveChangesAsync(ct);
        return Results.Accepted(value: new { message.Id, message.Status });
    }

    private static async Task<IResult> ConfigureAsync(
        Guid condominiumId, SettingsRequest request, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct)
    {
        var authorization = await AuthorizeManagerAsync(
            condominiumId, principal, db, ct);
        if (authorization is not null) return authorization;
        if (request.DisplayName?.Trim().Length > 200)
            return Results.BadRequest(new { error = "Display name is too long." });
        var condominium = await db.Condominiums.SingleOrDefaultAsync(
            x => x.Id == condominiumId, ct);
        if (condominium is null) return Results.NotFound();
        condominium.ConfigureWhatsAppUpdates(request.Enabled, request.DisplayName);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new
        {
            condominium.Id,
            condominium.WhatsAppUpdatesEnabled,
            condominium.WhatsAppDisplayName
        });
    }

    private static async Task<IResult?> AuthorizeManagerAsync(
        Guid condominiumId, ClaimsPrincipal principal, AppDbContext db,
        CancellationToken ct)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId))
            return Results.Unauthorized();
        var allowed = await db.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == userId && x.CondominiumId == condominiumId
                && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking()
                    .Where(x => x.Role == CondominiumRole.Manager
                        && x.IsActive && x.RevokedAt == null),
                x => x.Id, x => x.CondominiumMembershipId, (_, _) => true)
            .AnyAsync(ct);
        return allowed ? null : Results.Forbid();
    }

    public sealed record SettingsRequest(bool Enabled, string? DisplayName);
}
