using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Requests;

public static class ManageResidentClosure
{
    public static IEndpointRouteBuilder MapManageResidentClosure(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/resident-closure/confirm", ConfirmAsync).RequireAuthorization();
        endpoints.MapPost("/requests/{requestId:guid}/resident-closure/question", QuestionAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ConfirmAsync(Guid requestId, ClaimsPrincipal principal,
        RequestClosureService service, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        return ToResult(await service.ConfirmAsync(requestId, userId, ct));
    }

    private static async Task<IResult> QuestionAsync(Guid requestId, QuestionRequest request,
        ClaimsPrincipal principal, RequestClosureService service, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Informe sua dúvida ou observação." });
        return ToResult(await service.QuestionAsync(requestId, userId, request.Message,
            MessageChannel.Portal, ct));
    }

    private static IResult ToResult(RequestClosureService.Result result) => result.Succeeded
        ? Results.Ok(new { result.Code })
        : result.Code == "not_found"
            ? Results.NotFound(new { error = "Atendimento não encontrado." })
            : result.Code == "question_required"
                ? Results.BadRequest(new { error = "Informe sua dúvida ou observação." })
                : Results.Conflict(new { error = "Este atendimento já foi atualizado." });

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }

    public sealed record QuestionRequest(string? Message);
}
