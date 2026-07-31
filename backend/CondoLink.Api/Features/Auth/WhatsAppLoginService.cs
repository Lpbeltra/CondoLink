using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Auth;

public sealed class WhatsAppLoginService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IPhoneVerificationCodeGenerator codeGenerator,
    IPhoneVerificationMessageProtector messageProtector,
    AuthenticationSessionService sessions,
    TimeProvider timeProvider,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppLoginService> logger)
{
    internal const int ValidityMinutes = 10;
    internal const int MaximumAttempts = 5;
    internal const int ResendIntervalSeconds = 60;
    internal const int MaximumChallengesPerHour = 3;

    public async Task<RequestStatus> RequestCodeAsync(
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled
            || !settings.OutboundWorkerEnabled
            || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            || string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            logger.LogWarning(
                "WhatsApp passwordless login is unavailable because outbound integration is not configured.");
            return RequestStatus.Unavailable;
        }

        var normalizedPhone = PhoneNumberNormalizer.NormalizeBrazilian(
            phoneNumber);
        if (normalizedPhone is null) return RequestStatus.Accepted;

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.NormalizedPhoneNumber == normalizedPhone,
            cancellationToken);
        if (user is null
            || !user.IsActive
            || !user.PhoneNumberConfirmed
            || user.MustChangePassword)
            return RequestStatus.Accepted;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var latestCreatedAt = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .Where(x => x.UserId == user.Id
                && x.Purpose == WhatsAppChallengePurpose.Login)
            .MaxAsync(x => (DateTime?)x.CreatedAt, cancellationToken);
        if (latestCreatedAt > now.AddSeconds(-ResendIntervalSeconds))
            return RequestStatus.Accepted;

        var recentCount = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .CountAsync(x => x.UserId == user.Id
                && x.Purpose == WhatsAppChallengePurpose.Login
                && x.CreatedAt >= now.AddHours(-1), cancellationToken);
        if (recentCount >= MaximumChallengesPerHour)
            return RequestStatus.Accepted;

        var previous = await db.WhatsAppPhoneVerifications
            .Where(x => x.UserId == user.Id
                && x.Purpose == WhatsAppChallengePurpose.Login
                && x.ConsumedAt == null
                && x.InvalidatedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var previousChallenge in previous)
            previousChallenge.Invalidate(now);

        var code = codeGenerator.Generate();
        var (hash, salt) = PhoneVerificationCodeHasher.Hash(code);
        var challenge = new WhatsAppPhoneVerification(
            user.Id,
            normalizedPhone,
            hash,
            salt,
            now,
            now.AddMinutes(ValidityMinutes),
            MaximumAttempts,
            WhatsAppChallengePurpose.Login);
        db.WhatsAppPhoneVerifications.Add(challenge);
        db.WhatsAppOutboundMessages.Add(
            WhatsAppOutboundMessage.CreateLoginCode(
                user.Id,
                normalizedPhone,
                $"whatsapp-login:{challenge.Id:N}",
                messageProtector.Protect(Message(code)),
                now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "WhatsApp passwordless login challenge could not be queued.");
            return RequestStatus.Unavailable;
        }

        logger.LogInformation(
            "WhatsApp passwordless login challenge {ChallengeId} queued for user {UserId} phone {Phone}.",
            challenge.Id,
            user.Id,
            PhoneNumberNormalizer.Mask(normalizedPhone));
        return RequestStatus.Accepted;
    }

    public async Task<ConfirmResult> ConfirmAsync(
        string? phoneNumber,
        string? code,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.NormalizeBrazilian(
            phoneNumber);
        var normalizedCode = code?.Trim();
        if (normalizedPhone is null
            || normalizedCode is null
            || !LooksLikeCode(normalizedCode))
            return new(ConfirmStatus.Invalid);

        var challenge = await db.WhatsAppPhoneVerifications
            .Where(x => x.NormalizedPhoneNumber == normalizedPhone
                && x.Purpose == WhatsAppChallengePurpose.Login)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null) return new(ConfirmStatus.Invalid);
        if (challenge.ConsumedAt is not null)
            return new(ConfirmStatus.Consumed);
        if (challenge.AttemptCount >= challenge.MaximumAttempts)
            return new(ConfirmStatus.AttemptsExhausted);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (challenge.ExpiresAt <= now)
        {
            challenge.Invalidate(now);
            await SaveChallengeStateAsync(cancellationToken);
            return new(ConfirmStatus.Expired);
        }
        if (challenge.InvalidatedAt is not null)
            return new(ConfirmStatus.Invalid);

        if (!PhoneVerificationCodeHasher.Verify(
                normalizedCode, challenge.CodeHash, challenge.CodeSalt))
        {
            challenge.RegisterFailedAttempt(now);
            if (!await SaveChallengeStateAsync(cancellationToken))
                return new(ConfirmStatus.Consumed);
            return new(
                challenge.InvalidatedAt is not null
                    ? ConfirmStatus.AttemptsExhausted
                    : ConfirmStatus.Invalid);
        }

        var user = await userManager.FindByIdAsync(
            challenge.UserId.ToString());
        if (user is null
            || !user.IsActive
            || !user.PhoneNumberConfirmed
            || user.MustChangePassword
            || !string.Equals(
                user.NormalizedPhoneNumber,
                normalizedPhone,
                StringComparison.Ordinal))
        {
            challenge.Invalidate(now);
            await SaveChallengeStateAsync(cancellationToken);
            return new(ConfirmStatus.Invalid);
        }

        challenge.Consume(now);
        if (!await SaveChallengeStateAsync(cancellationToken))
            return new(ConfirmStatus.Consumed);

        var session = await sessions.IssueAsync(user, cancellationToken);
        if (session is null) return new(ConfirmStatus.Unavailable);

        logger.LogInformation(
            "WhatsApp passwordless login challenge {ChallengeId} consumed for user {UserId}.",
            challenge.Id,
            user.Id);
        return new(ConfirmStatus.Confirmed, session);
    }

    private async Task<bool> SaveChallengeStateAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    internal static string Message(string code) =>
        $"Seu código de acesso à Comvy é: {code}. "
        + $"Ele expira em {ValidityMinutes} minutos. "
        + "Não compartilhe este código.";

    private static bool LooksLikeCode(string value) =>
        value.Length == 6 && value.All(char.IsAsciiDigit);

    public enum RequestStatus
    {
        Accepted,
        Unavailable
    }

    public enum ConfirmStatus
    {
        Confirmed,
        Invalid,
        Expired,
        AttemptsExhausted,
        Consumed,
        Unavailable
    }

    public sealed record ConfirmResult(
        ConfirmStatus Status,
        Login.Response? Session = null);
}
