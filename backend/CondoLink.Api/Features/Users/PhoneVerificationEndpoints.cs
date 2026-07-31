using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.Users;

public static class PhoneVerificationEndpoints
{
    public static IEndpointRouteBuilder MapPhoneVerification(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users/me/phone-verification")
            .RequireAuthorization()
            .WithTags("Phone verification");
        group.MapPost("", StartAsync);
        group.MapPost("/confirm", ConfirmAsync);
        group.MapGet("", GetAsync);
        return endpoints;
    }

    private static async Task<IResult> StartAsync(
        ClaimsPrincipal principal,
        [FromServices] WhatsAppPhoneVerificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId))
            return Results.Unauthorized();
        var result = await service.StartAsync(userId, cancellationToken);
        return result.Status switch
        {
            WhatsAppPhoneVerificationService.StartStatus.Started =>
                Results.Accepted(value: new
                {
                    status = "started",
                    result.ExpiresAt
                }),
            WhatsAppPhoneVerificationService.StartStatus.AlreadyConfirmed =>
                Results.Ok(new { status = "already_confirmed" }),
            WhatsAppPhoneVerificationService.StartStatus.NoPhone =>
                Results.BadRequest(new
                    { error = "Cadastre um telefone válido antes de solicitar a confirmação." }),
            WhatsAppPhoneVerificationService.StartStatus.Inactive =>
                Results.Json(new { error = "A conta está inativa." },
                    statusCode: StatusCodes.Status403Forbidden),
            WhatsAppPhoneVerificationService.StartStatus.NotFound =>
                Results.Unauthorized(),
            WhatsAppPhoneVerificationService.StartStatus.TooSoon =>
                Results.Json(new
                    {
                        error = "Aguarde antes de solicitar outro código.",
                        result.RetryAfter
                    },
                    statusCode: StatusCodes.Status429TooManyRequests),
            WhatsAppPhoneVerificationService.StartStatus.RateLimited =>
                Results.Json(new
                    { error = "Limite de solicitações atingido. Tente novamente mais tarde." },
                    statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.Json(new
                { error = "A confirmação por WhatsApp está indisponível no momento." },
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static async Task<IResult> ConfirmAsync(
        ClaimsPrincipal principal,
        ConfirmPhoneVerificationRequest request,
        [FromServices] WhatsAppPhoneVerificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId))
            return Results.Unauthorized();
        var result = await service.ConfirmAsync(
            userId, request.Code, cancellationToken);
        return result switch
        {
            WhatsAppPhoneVerificationService.ConfirmStatus.Confirmed =>
                Results.Ok(new { status = "confirmed" }),
            WhatsAppPhoneVerificationService.ConfirmStatus.AlreadyConfirmed =>
                Results.Ok(new { status = "already_confirmed" }),
            WhatsAppPhoneVerificationService.ConfirmStatus.InvalidCode =>
                Results.BadRequest(new
                {
                    status = "invalid_code",
                    error = "Código inválido."
                }),
            WhatsAppPhoneVerificationService.ConfirmStatus.Expired =>
                Results.Json(new
                {
                    status = "expired",
                    error = "O código expirou. Solicite um novo código."
                }, statusCode: StatusCodes.Status410Gone),
            WhatsAppPhoneVerificationService.ConfirmStatus.AttemptsExhausted =>
                Results.Json(new
                {
                    status = "attempts_exhausted",
                    error = "O limite de tentativas foi atingido. Solicite um novo código."
                }, statusCode: StatusCodes.Status429TooManyRequests),
            WhatsAppPhoneVerificationService.ConfirmStatus.Used =>
                Results.Conflict(new
                {
                    status = "used",
                    error = "Este código já foi utilizado."
                }),
            WhatsAppPhoneVerificationService.ConfirmStatus.Inactive =>
                Results.Json(new { error = "A conta está inativa." },
                    statusCode: StatusCodes.Status403Forbidden),
            WhatsAppPhoneVerificationService.ConfirmStatus.NotFound =>
                Results.Unauthorized(),
            _ => Results.Conflict(new
            {
                status = "unavailable",
                error = "Não há código ativo para este telefone."
            })
        };
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        [FromServices] WhatsAppPhoneVerificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(principal, out var userId))
            return Results.Unauthorized();
        var status = await service.GetStatusAsync(userId, cancellationToken);
        return status is null ? Results.Unauthorized() : Results.Ok(status);
    }

    private static bool TryUserId(
        ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out userId);
    }

    public sealed record ConfirmPhoneVerificationRequest(string? Code);
}
