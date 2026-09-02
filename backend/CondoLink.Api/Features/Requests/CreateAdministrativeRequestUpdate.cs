using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class CreateAdministrativeRequestUpdate
{
    public static IEndpointRouteBuilder MapCreateAdministrativeRequestUpdate(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/management/requests/{requestId:guid}/updates", HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid requestId, Input input,
        ClaimsPrincipal principal, AppDbContext db, NotificationService notifications,
        IServiceProvider services, ILogger<CreateAdministrativeRequestUpdateMarker> logger,
        CancellationToken ct)
    {
        var raw = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(raw, out var userId)) return Results.Unauthorized();
        var active = await db.Set<ApplicationUser>().AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive, ct);
        if (!active) return Results.Forbid();
        var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Results.NotFound(new { error = "Request not found." });
        var manager = await db.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == userId && x.CondominiumId == request.CondominiumId
                && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x =>
                    (x.Role == CondominiumRole.Manager || x.Role == CondominiumRole.SubManager) && x.IsActive
                    && x.RevokedAt == null), x => x.Id,
                x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
        if (!manager) return Results.Forbid();
        if (request.Status is not RequestStatus.InProgress
            and not RequestStatus.WaitingForThirdParty)
            return Results.Conflict(new
            { error = "O status atual não permite uma atualização administrativa avulsa." });
        var content = input.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return Results.BadRequest(new { error = "Informe a mensagem." });
        if (content.Length > RequestMessage.MaximumContentLength)
            return Results.BadRequest(new { error = "A mensagem pode ter no máximo 3000 caracteres." });

        var message = new RequestMessage(request.Id, userId, content,
            MessageChannel.Portal);
        db.RequestMessages.Add(message);
        await db.SaveChangesAsync(ct);
        try
        {
            await notifications.NotifyAdministrativeUpdateAsync(request, message, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Administrative update notification failed. RequestId: {RequestId}; RequestMessageId: {RequestMessageId}.",
                request.Id, message.Id);
        }
        if (services.GetService<RequestAiAnalysisRefresher>() is { } refresher)
            await refresher.RefreshAsync(request.Id, "manager_update", ct);
        var authorName = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.Id == userId).Select(x => x.FullName).SingleAsync(ct);
        return Results.Created($"/request-messages/{message.Id}", new Response(
            message.Id, message.RequestId, userId, authorName, message.Content,
            message.CreatedAt));
    }

    public sealed record Input(string? Content);
    public sealed record Response(Guid Id, Guid RequestId, Guid AuthorUserId,
        string AuthorFullName, string Content, DateTime CreatedAt);
    public sealed class CreateAdministrativeRequestUpdateMarker;
}
