using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.WebPush;

public static class WebPushEndpoints
{
    private const int EndpointLimit = 2048;
    private const int KeyLimit = 512;
    private const int UserAgentLimit = 256;

    public static IEndpointRouteBuilder MapWebPush(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users/me/push").RequireAuthorization()
            .WithTags("Web Push");
        group.MapGet("/config", ConfigAsync);
        group.MapPost("/subscriptions", RegisterAsync);
        group.MapDelete("/subscriptions", DeactivateAsync);
        return endpoints;
    }

    private static IResult ConfigAsync(IOptions<WebPushOptions> options)
    {
        var configured = options.Value.IsConfigured;
        return Results.Ok(new
        {
            enabled = configured,
            vapidPublicKey = configured ? options.Value.VapidPublicKey : null
        });
    }

    private static async Task<IResult> RegisterAsync(
        RegisterSubscription body,
        ClaimsPrincipal principal,
        HttpRequest request,
        AppDbContext db,
        IOptions<WebPushOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
            return Results.Problem("Notificações estão desativadas.", statusCode: 503);
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();

        var validation = Validate(body.Endpoint, body.Keys?.P256dh, body.Keys?.Auth);
        if (validation is not null) return Results.ValidationProblem(validation);
        var activeUser = await db.Set<ApplicationUser>().AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!activeUser) return Results.Unauthorized();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userAgent = SanitizeUserAgent(request.Headers.UserAgent.ToString());
        var subscription = await db.WebPushSubscriptions
            .SingleOrDefaultAsync(x => x.Endpoint == body.Endpoint.Trim(), cancellationToken);
        var isNew = subscription is null;
        if (subscription is null)
        {
            subscription = new WebPushSubscription(userId, body.Endpoint,
                body.Keys!.P256dh, body.Keys.Auth, userAgent, now);
            db.WebPushSubscriptions.Add(subscription);
        }
        else
        {
            // Possession of the endpoint and keys comes from an explicit browser
            // subscription action. Reassignment prevents a shared browser from
            // retaining a previous user's association.
            subscription.AssignTo(userId, body.Endpoint, body.Keys!.P256dh,
                body.Keys.Auth, userAgent, now);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNew)
        {
            // A concurrent idempotent registration may win the unique Endpoint
            // index. Reload and update that row instead of surfacing a duplicate.
            db.Entry(subscription).State = EntityState.Detached;
            subscription = await db.WebPushSubscriptions.SingleOrDefaultAsync(
                x => x.Endpoint == body.Endpoint.Trim(), cancellationToken);
            if (subscription is null) throw;
            subscription.AssignTo(userId, body.Endpoint, body.Keys!.P256dh,
                body.Keys.Auth, userAgent, now);
            await db.SaveChangesAsync(cancellationToken);
        }
        return Results.Ok(new { subscription.Id, active = true });
    }

    private static async Task<IResult> DeactivateAsync(
        [FromBody] DeactivateSubscription body,
        ClaimsPrincipal principal,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Endpoint) || body.Endpoint.Length > EndpointLimit)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["endpoint"] = ["Endpoint inválido."] });

        var subscription = await db.WebPushSubscriptions.SingleOrDefaultAsync(
            x => x.Endpoint == body.Endpoint.Trim() && x.UserId == userId, cancellationToken);
        if (subscription is not null)
        {
            subscription.Deactivate(timeProvider.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(cancellationToken);
        }
        return Results.NoContent();
    }

    private static Dictionary<string, string[]>? Validate(string? endpoint,
        string? p256dh, string? auth)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > EndpointLimit
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            errors["endpoint"] = ["Endpoint HTTPS inválido."];
        if (!IsBase64Url(p256dh, 40))
            errors["p256dh"] = ["Chave p256dh inválida."];
        if (!IsBase64Url(auth, 16))
            errors["auth"] = ["Chave auth inválida."];
        return errors.Count == 0 ? null : errors;
    }

    private static bool IsBase64Url(string? value, int minimumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length >= minimumLength
        && value.Length <= KeyLimit
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '=');

    private static string? SanitizeUserAgent(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sanitized = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return sanitized.Length <= UserAgentLimit ? sanitized : sanitized[..UserAgentLimit];
    }

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }

    public sealed record RegisterSubscription(string Endpoint, SubscriptionKeys Keys);
    public sealed record SubscriptionKeys(string P256dh, string Auth);
    public sealed record DeactivateSubscription(string Endpoint);
}
