using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class SuggestRequestStatusMessage
{
    public static IEndpointRouteBuilder MapSuggestRequestStatusMessage(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/status-message-suggestion", HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid requestId,
        RequestDto request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        IRequestDraftAiService ai,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Unauthorized();

        var userIsActive = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(user => user.Id == userId).Select(user => (bool?)user.IsActive)
            .SingleOrDefaultAsync(cancellationToken);
        if (userIsActive is null)
            return Results.Unauthorized();
        if (userIsActive is false)
            return Results.Forbid();

        var target = await dbContext.Requests.AsNoTracking()
            .Where(item => item.Id == requestId)
            .Select(item => new { item.CondominiumId, item.Title })
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
            return Results.NotFound(new { error = "Request not found." });

        var isManager = await dbContext.CondominiumMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId
                && membership.CondominiumId == target.CondominiumId
                && membership.IsActive && membership.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking()
                    .Where(role => (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                        && role.IsActive && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);
        if (!isManager)
            return Results.Forbid();

        if (!TryParseResidentStatus(request.Status, out var status))
            return Results.BadRequest(new { error = "Invalid request status." });

        var original = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(original))
            return Results.BadRequest(new { error = "Informe uma mensagem para gerar a sugestão." });
        if (original.Length > 1000)
            return Results.BadRequest(new { error = "A mensagem pode ter no máximo 1000 caracteres." });

        var result = await ai.SynthesizeResidentStatusAsync(target.Title,
            NotificationService.Describe(status), original, cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Message))
            return Results.Json(new { error = "Não foi possível gerar a sugestão." },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        return Results.Ok(new Response(result.Message));
    }

    private static bool TryParseResidentStatus(string? value, out RequestStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, true, out status)
            && status is RequestStatus.InProgress
                or RequestStatus.WaitingForResident
                or RequestStatus.WaitingForThirdParty
                or RequestStatus.WaitingForResidentClosure
                or RequestStatus.Resolved
                or RequestStatus.Cancelled
                or RequestStatus.Open;
    }

    public sealed record RequestDto(string? Status, string? Message);
    public sealed record Response(string Suggestion);
}
