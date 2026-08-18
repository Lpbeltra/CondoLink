using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Auth;

public static class FirstAccessEndpoints
{
    public static IEndpointRouteBuilder MapFirstAccess(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/first-access/validate", ValidateAsync);
        endpoints.MapPost("/auth/first-access/complete", CompleteAsync);
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/{userId:guid}/first-access/resend", ResendAsync).RequireAuthorization();
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/{userId:guid}/first-access/link", LinkAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ValidateAsync(TokenRequest request, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive || !user.MustChangePassword) return InvalidToken();
        var valid = await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose, request.Token);
        return valid ? Results.Ok(new { valid = true }) : InvalidToken();
    }

    private static async Task<IResult> CompleteAsync(CompleteRequest request, UserManager<ApplicationUser> users)
    {
        if (request.Password != request.ConfirmPassword)
            return Results.BadRequest(new { error = "As senhas não coincidem." });
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive || !user.MustChangePassword) return InvalidToken();
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
            return Results.BadRequest(new { error = "O link é inválido ou expirou.", errors = result.Errors.Select(x => x.Description) });
        user.MarkPasswordChanged(DateTime.UtcNow);
        await users.UpdateAsync(user);
        return Results.Ok(new { message = "Senha criada com sucesso." });
    }

    private static async Task<IResult> ResendAsync(Guid condominiumId, Guid userId, ClaimsPrincipal principal,
        AppDbContext db, UserManager<ApplicationUser> users, FirstAccessService service, CancellationToken ct)
    {
        if (!await CanManageAsync(condominiumId, userId, principal, db, ct)) return Results.NotFound();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.MustChangePassword) return Results.Conflict(new { error = "O primeiro acesso já foi concluído." });
        if (!user.EmailDeliveryEnabled) return Results.Conflict(new { error = "Este e-mail é apenas para acesso ao sistema." });
        var name = await db.Condominiums.Where(x => x.Id == condominiumId).Select(x => x.Name).SingleAsync(ct);
        var sent = await service.SendAsync(user, name, ct);
        return sent ? Results.Ok(new { status = "InviteSent" }) : Results.Json(new { status = "DeliveryFailed", error = "Não foi possível enviar o convite." }, statusCode: 502);
    }

    private static async Task<IResult> LinkAsync(Guid condominiumId, Guid userId, ClaimsPrincipal principal,
        AppDbContext db, UserManager<ApplicationUser> users, FirstAccessService service, CancellationToken ct)
    {
        if (!await CanManageAsync(condominiumId, userId, principal, db, ct)) return Results.NotFound();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.MustChangePassword) return Results.Conflict(new { error = "O primeiro acesso já foi concluído." });
        return Results.Ok(new { link = await service.CreateLinkAsync(user), expiresInHours = 24 });
    }

    private static async Task<bool> CanManageAsync(Guid condominiumId, Guid targetUserId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var current)) return false;
        var manager = await db.CondominiumMemberships.AsNoTracking().Where(x => x.UserId == current && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null), x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
        if (!manager && !principal.IsInRole(Infrastructure.DependencyInjection.PlatformAdminRole)) return false;
        return await db.CondominiumMemberships.AsNoTracking().AnyAsync(x => x.CondominiumId == condominiumId && x.UserId == targetUserId, ct);
    }
    private static IResult InvalidToken() => Results.BadRequest(new { error = "O link é inválido, expirou ou já foi utilizado." });
    public sealed record TokenRequest(Guid UserId, string Token);
    public sealed record CompleteRequest(Guid UserId, string Token, string Password, string ConfirmPassword);
}
