using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Auth;

public static class PasswordResetEndpoints
{
    private const string NeutralMessage = "Se existir uma conta elegível para este e-mail, enviaremos as instruções de redefinição.";

    public static IEndpointRouteBuilder MapPasswordReset(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/forgot-password", ForgotAsync).WithTags("Authentication");
        endpoints.MapPost("/auth/reset-password", ResetAsync).WithTags("Authentication");
        return endpoints;
    }

    private static async Task<IResult> ForgotAsync(
        ForgotRequest? request,
        HttpContext context,
        PasswordResetRequestLimiter limiter,
        PasswordResetService service,
        CancellationToken ct)
    {
        var email = request?.Email?.Trim();
        var allowed = !string.IsNullOrWhiteSpace(email)
            && limiter.TryAcquire(email, context.Connection.RemoteIpAddress?.ToString());

        if (allowed
            && new EmailAddressAttribute().IsValid(email)
            )
            await service.SendAsync(email!, ct);

        return Results.Accepted(value: new { message = NeutralMessage });
    }

    private static async Task<IResult> ResetAsync(
        ResetRequest? request,
        PasswordResetService service,
        CancellationToken ct)
    {
        if (request is null || request.UserId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrEmpty(request.Password)
            || request.Password != request.Confirmation)
            return InvalidReset();

        var result = await service.ResetAsync(request.UserId, request.Token,
            request.Password, ct);
        return result switch
        {
            PasswordResetResult.Succeeded => Results.Ok(new { message = "Senha redefinida com sucesso." }),
            PasswordResetResult.PasswordRejected => Results.BadRequest(new
            {
                error = "A nova senha não atende aos requisitos de segurança.",
                requirements = new[] { "Use ao menos 8 caracteres.", "Inclua letras maiúsculas, minúsculas e números." }
            }),
            _ => InvalidReset()
        };
    }

    private static IResult InvalidReset() => Results.BadRequest(
        new { error = "O link é inválido, expirou ou já foi utilizado." });

    public sealed record ForgotRequest(string? Email);
    public sealed record ResetRequest(Guid UserId, string? Token, string? Password, string? Confirmation);
}

public enum PasswordResetResult { Succeeded, Invalid, PasswordRejected }

public sealed class PasswordResetService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    AuthenticationSessionService sessions,
    IEmailSender emailSender,
    IOptions<FirstAccessOptions> options,
    TimeProvider clock,
    ILogger<PasswordResetService> logger)
{
    public async Task SendAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !IsEligible(user)) return;

        try
        {
            var link = await CreateLinkAsync(user);
            var safeLink = WebUtility.HtmlEncode(link);
            var html = $"<p>Olá, {WebUtility.HtmlEncode(user.FullName)}.</p><p>Recebemos uma solicitação para redefinir sua senha no Comvy.</p><p><a href=\"{safeLink}\">Redefinir minha senha</a></p><p>Este link é válido por 24 horas. Se você não solicitou a redefinição, ignore esta mensagem.</p>";
            await emailSender.SendAsync(user.Email!, "Redefinição de senha do Comvy", html, ct);
            logger.LogInformation("Password-reset email sent for UserId {UserId}.", user.Id);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Password-reset email failed for UserId {UserId}.", user.Id);
        }
    }

    public async Task<PasswordResetResult> ResetAsync(Guid userId, string token,
        string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !IsEligible(user)) return PasswordResetResult.Invalid;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return result.Errors.Any(error => error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                ? PasswordResetResult.PasswordRejected
                : PasswordResetResult.Invalid;
        }

        user.MarkPasswordChanged(clock.GetUtcNow().UtcDateTime);
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException("Could not complete password reset.");

        await sessions.RevokeAllForUserAsync(user.Id, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Password reset completed for UserId {UserId}.", user.Id);
        return PasswordResetResult.Succeeded;
    }

    private async Task<string> CreateLinkAsync(ApplicationUser user)
    {
        var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("FirstAccess__FrontendBaseUrl deve ser uma URL HTTPS válida.");

        var token = await users.GeneratePasswordResetTokenAsync(user);
        return $"{baseUrl}/reset-password?userId={Uri.EscapeDataString(user.Id.ToString())}&token={Uri.EscapeDataString(token)}";
    }

    private static bool IsEligible(ApplicationUser user) => user.IsActive
        && user.EmailDeliveryEnabled
        && !string.IsNullOrWhiteSpace(user.Email)
        && FirstAccessEmailPolicy.IsDeliverable(user.Email);
}

public sealed class PasswordResetRequestLimiter(IMemoryCache cache)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly Lock Sync = new();
    private const int MaximumRequests = 5;

    public bool TryAcquire(string email, string? address)
    {
        var keys = new[] { Key("email", email.Trim().ToUpperInvariant()), Key("ip", address ?? "unknown") };
        var allowed = true;
        lock (Sync)
        {
            foreach (var key in keys)
            {
                var count = cache.GetOrCreate(key, entry => { entry.AbsoluteExpirationRelativeToNow = Window; return 0; });
                if (count >= MaximumRequests) allowed = false;
                else cache.Set(key, count + 1, Window);
            }
        }
        return allowed;
    }
    private static string Key(string kind, string value) => $"password-reset:{kind}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";
}
