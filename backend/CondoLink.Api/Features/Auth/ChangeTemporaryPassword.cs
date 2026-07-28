using System.ComponentModel.DataAnnotations;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CondoLink.Api.Features.Auth;

public static class ChangeTemporaryPassword
{
    public static IEndpointRouteBuilder MapChangeTemporaryPassword(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/change-temporary-password", HandleAsync)
            .WithTags("Authentication")
            .WithSummary("Change a temporary password");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return Results.BadRequest(new { error = "Informe um e-mail válido." });
        if (string.IsNullOrEmpty(request.TemporaryPassword))
            return Results.BadRequest(new { error = "Informe a senha temporária." });
        if (string.IsNullOrEmpty(request.NewPassword))
            return Results.BadRequest(new { error = "Informe a nova senha." });
        if (request.NewPassword != request.Confirmation)
            return Results.BadRequest(new { error = "A confirmação da senha não confere." });
        if (request.NewPassword == request.TemporaryPassword)
            return Results.BadRequest(new { error = "A nova senha deve ser diferente da senha temporária." });

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null
            || !await userManager.CheckPasswordAsync(user, request.TemporaryPassword))
            return Results.Json(
                new { error = "Senha temporária inválida." },
                statusCode: StatusCodes.Status401Unauthorized);
        if (!user.IsActive)
            return Results.Json(
                new { error = "A conta está inativa." },
                statusCode: StatusCodes.Status403Forbidden);
        if (!user.MustChangePassword)
            return Results.Conflict(new { error = "Esta conta não possui uma troca de senha pendente." });

        user.MarkPasswordChanged(DateTime.UtcNow);
        var changeResult = await userManager.ChangePasswordAsync(
            user,
            request.TemporaryPassword,
            request.NewPassword);
        if (!changeResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                error = "A nova senha não atende aos requisitos de segurança.",
                requirements = new[]
                {
                    "Use ao menos 8 caracteres.",
                    "Inclua letras maiúsculas, minúsculas e números."
                }
            });
        }

        return Results.Ok(new
        {
            message = "Senha atualizada com sucesso."
        });
    }

    public sealed record Request(
        string? Email,
        string? TemporaryPassword,
        string? NewPassword,
        string? Confirmation);
}
