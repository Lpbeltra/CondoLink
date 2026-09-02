using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class ResetMemberTemporaryPassword
{
    public static IEndpointRouteBuilder MapResetMemberTemporaryPassword(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/condominiums/{condominiumId:guid}/members/{userId:guid}/reset-temporary-password",
                HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Guid userId,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var callerValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerValue, out var callerId))
            return Results.Json(new { error = "Usuário autenticado inválido." }, statusCode: 401);

        var caller = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == callerId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (caller is null)
            return Results.Json(new { error = "Usuário autenticado não encontrado." }, statusCode: 401);
        if (!caller.IsActive)
            return Results.Json(new { error = "A conta está inativa." }, statusCode: 403);

        var isPlatformAdmin = principal.IsInRole(DependencyInjection.PlatformAdminRole);
        if (!isPlatformAdmin)
        {
            var isManager = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                where membership.UserId == callerId
                    && membership.CondominiumId == condominiumId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                select membership.Id)
                .AnyAsync(cancellationToken);
            if (!isManager)
                return Results.Json(
                    new { error = "Você não possui permissão para redefinir esta senha." },
                    statusCode: StatusCodes.Status403Forbidden);
        }

        var belongsToCondominium = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.CondominiumId == condominiumId
                && membership.IsActive
                && membership.EndedAt == null,
                cancellationToken);
        if (!belongsToCondominium)
            return Results.NotFound(new { error = "Pessoa não encontrada neste condomínio." });

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Results.NotFound(new { error = "Usuário não encontrado." });
        if (!user.IsActive)
            return Results.Conflict(new { error = "Não é possível redefinir a senha de uma conta inativa." });

        var temporaryPassword = GenerateTemporaryPassword();
        user.PasswordHash = userManager.PasswordHasher.HashPassword(
            user,
            temporaryPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.RequirePasswordChange();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);

        return Results.Ok(new Response(
            user.Id,
            user.FullName,
            user.Email!,
            temporaryPassword));
    }

    private static string GenerateTemporaryPassword()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var randomPart = new string(
            bytes.Select(value => characters[value % characters.Length])
                .ToArray());
        return $"Aa1!{randomPart}";
    }

    public sealed record Response(
        Guid UserId,
        string FullName,
        string Email,
        string TemporaryPassword);
}
