using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.Auth;

public static class WhatsAppLoginEndpoints
{
    private const string GenericRequestMessage =
        "Se o telefone estiver apto para login, enviaremos um código pelo WhatsApp.";

    public static IEndpointRouteBuilder MapWhatsAppLogin(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth/whatsapp")
            .WithTags("Authentication");
        group.MapPost("/request-code", RequestCodeAsync);
        group.MapPost("/confirm", ConfirmAsync);
        return endpoints;
    }

    private static async Task<IResult> RequestCodeAsync(
        RequestCodeRequest request,
        [FromServices] WhatsAppLoginService service,
        CancellationToken cancellationToken)
    {
        var status = await service.RequestCodeAsync(
            request.PhoneNumber, cancellationToken);
        return status == WhatsAppLoginService.RequestStatus.Unavailable
            ? Results.Json(new
            {
                code = "whatsapp_unavailable",
                error = "O login pelo WhatsApp está temporariamente indisponível."
            }, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Accepted(value: new
            {
                status = "accepted",
                message = GenericRequestMessage,
                retryAfterSeconds =
                    WhatsAppLoginService.ResendIntervalSeconds
            });
    }

    private static async Task<IResult> ConfirmAsync(
        ConfirmRequest request,
        [FromServices] WhatsAppLoginService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmAsync(
            request.PhoneNumber, request.Code, cancellationToken);
        return result.Status switch
        {
            WhatsAppLoginService.ConfirmStatus.Confirmed =>
                Results.Ok(result.Session),
            WhatsAppLoginService.ConfirmStatus.Expired =>
                Results.Json(new
                {
                    code = "code_expired",
                    error = "O código expirou. Solicite um novo código."
                }, statusCode: StatusCodes.Status410Gone),
            WhatsAppLoginService.ConfirmStatus.AttemptsExhausted =>
                Results.Json(new
                {
                    code = "attempts_exhausted",
                    error = "O limite de tentativas foi atingido. Solicite um novo código."
                }, statusCode: StatusCodes.Status429TooManyRequests),
            WhatsAppLoginService.ConfirmStatus.Consumed =>
                Results.Conflict(new
                {
                    code = "code_consumed",
                    error = "Este código não está mais disponível."
                }),
            WhatsAppLoginService.ConfirmStatus.Unavailable =>
                Results.Json(new
                {
                    code = "login_unavailable",
                    error = "Não foi possível concluir o acesso agora."
                }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Json(new
            {
                code = "invalid_code",
                error = "Telefone ou código inválido."
            }, statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    public sealed record RequestCodeRequest(string? PhoneNumber);
    public sealed record ConfirmRequest(string? PhoneNumber, string? Code);
}
