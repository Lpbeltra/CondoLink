using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppPhoneVerificationService(
    AppDbContext db,
    IPhoneVerificationCodeGenerator codeGenerator,
    IPhoneVerificationMessageProtector messageProtector,
    TimeProvider timeProvider,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppPhoneVerificationService> logger)
{
    internal const int ValidityMinutes = 10;
    internal const int MaximumAttempts = 5;
    internal const int ResendIntervalSeconds = 60;
    internal const int MaximumChallengesPerHour = 3;

    public async Task<StartResult> StartAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Authenticated WhatsApp phone verification request received for user {UserId}.",
            userId);
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning(
                "WhatsApp phone verification user was not found.");
            return new(StartStatus.NotFound);
        }
        if (!user.IsActive)
        {
            logger.LogWarning(
                "WhatsApp phone verification rejected because the user is inactive.");
            return new(StartStatus.Inactive);
        }
        if (user.NormalizedPhoneNumber is null)
        {
            logger.LogWarning(
                "WhatsApp phone verification rejected because the phone is missing or invalid.");
            return new(StartStatus.NoPhone);
        }
        if (user.PhoneNumberConfirmed)
        {
            logger.LogInformation(
                "WhatsApp phone verification skipped because the phone is already confirmed.");
            return new(StartStatus.AlreadyConfirmed);
        }

        var settings = options.Value;
        if (!settings.Enabled || !settings.OutboundWorkerEnabled)
        {
            logger.LogWarning(
                "WhatsApp phone verification integration is unavailable. Enabled: {Enabled}; OutboundWorkerEnabled: {OutboundWorkerEnabled}.",
                settings.Enabled,
                settings.OutboundWorkerEnabled);
            return new(StartStatus.Unavailable);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var latestCreatedAt = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .Where(x => x.UserId == userId
                && x.Purpose == WhatsAppChallengePurpose.PhoneVerification)
            .MaxAsync(x => (DateTime?)x.CreatedAt, cancellationToken);
        if (latestCreatedAt > now.AddSeconds(-ResendIntervalSeconds))
        {
            logger.LogInformation(
                "WhatsApp phone verification request is within the resend cooldown.");
            return new(
                StartStatus.TooSoon,
                RetryAfter: latestCreatedAt.Value
                    .AddSeconds(ResendIntervalSeconds));
        }

        var recentCount = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .CountAsync(x => x.UserId == userId
                && x.Purpose == WhatsAppChallengePurpose.PhoneVerification
                && x.CreatedAt >= now.AddHours(-1), cancellationToken);
        if (recentCount >= MaximumChallengesPerHour)
        {
            logger.LogWarning(
                "WhatsApp phone verification hourly request limit was reached.");
            return new(StartStatus.RateLimited);
        }

        var previous = await db.WhatsAppPhoneVerifications
            .Where(x => x.UserId == userId
                && x.Purpose == WhatsAppChallengePurpose.PhoneVerification
                && x.ConsumedAt == null
                && x.InvalidatedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var verification in previous) verification.Invalidate(now);

        WhatsAppPhoneVerification challenge;
        WhatsAppOutboundMessage outbound;
        var expiresAt = now.AddMinutes(ValidityMinutes);
        try
        {
            var code = codeGenerator.Generate();
            var (hash, salt) = PhoneVerificationCodeHasher.Hash(code);
            challenge = new WhatsAppPhoneVerification(
                user.Id, user.NormalizedPhoneNumber, hash, salt,
                now, expiresAt, MaximumAttempts,
                WhatsAppChallengePurpose.PhoneVerification);
            db.WhatsAppPhoneVerifications.Add(challenge);
            outbound = WhatsAppOutboundMessage.CreatePhoneVerification(
                user.Id,
                user.NormalizedPhoneNumber,
                $"phone-verification:{challenge.Id:N}",
                messageProtector.Protect(Message(code)),
                now);
            db.WhatsAppOutboundMessages.Add(outbound);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "WhatsApp phone verification failed before the challenge and outbound message could be persisted.");
            return new(StartStatus.Unavailable);
        }
        logger.LogInformation(
            "WhatsApp phone verification challenge {VerificationId} created with purpose {Purpose}.",
            challenge.Id,
            challenge.Purpose);
        logger.LogInformation(
            "WhatsApp phone verification outbound message {OutboundId} queued with notification type {NotificationType}.",
            outbound.Id,
            outbound.NotificationType);
        logger.LogInformation(
            "WhatsApp phone verification {VerificationId} created and outbound {OutboundId} queued for user {UserId} phone {Phone}.",
            challenge.Id,
            outbound.Id,
            user.Id,
            PhoneNumberNormalizer.Mask(user.NormalizedPhoneNumber));
        return new(StartStatus.Started, expiresAt);
    }

    public async Task<SafeStatus?> GetStatusAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var latest = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x =>
                x.Purpose == WhatsAppChallengePurpose.PhoneVerification)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.CreatedAt, x.ExpiresAt, x.ConsumedAt, x.InvalidatedAt,
                x.AttemptCount, x.MaximumAttempts
            })
            .FirstOrDefaultAsync(cancellationToken);
        var active = latest is not null && latest.ConsumedAt is null
            && latest.InvalidatedAt is null && latest.ExpiresAt > now
            && latest.AttemptCount < latest.MaximumAttempts;
        var canResendAt = latest is null
            ? now : latest.CreatedAt.AddSeconds(ResendIntervalSeconds);
        var recentCount = await db.WhatsAppPhoneVerifications.AsNoTracking()
            .CountAsync(x => x.UserId == userId
                && x.Purpose == WhatsAppChallengePurpose.PhoneVerification
                && x.CreatedAt >= now.AddHours(-1), cancellationToken);
        var canResend = !user.PhoneNumberConfirmed
            && user.NormalizedPhoneNumber is not null
            && options.Value.Enabled
            && options.Value.OutboundWorkerEnabled
            && recentCount < MaximumChallengesPerHour
            && canResendAt <= now;
        return new(
            user.NormalizedPhoneNumber is null
                ? null : PhoneNumberNormalizer.Mask(user.NormalizedPhoneNumber),
            user.PhoneNumberConfirmed,
            active,
            active ? latest!.ExpiresAt : null,
            canResend,
            !canResend && canResendAt > now ? canResendAt : null);
    }

    public async Task<ConfirmStatus> ConfirmAsync(
        Guid userId,
        string? code,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId, cancellationToken);
        if (user is null) return ConfirmStatus.NotFound;
        if (!user.IsActive) return ConfirmStatus.Inactive;

        var verification = await db.WhatsAppPhoneVerifications
            .Where(x => x.UserId == userId)
            .Where(x =>
                x.Purpose == WhatsAppChallengePurpose.PhoneVerification)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (verification is null)
            return user.PhoneNumberConfirmed
                ? ConfirmStatus.AlreadyConfirmed
                : ConfirmStatus.Unavailable;
        if (verification.ConsumedAt is not null)
            return ConfirmStatus.Used;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (verification.AttemptCount >= verification.MaximumAttempts)
            return ConfirmStatus.AttemptsExhausted;
        if (verification.ExpiresAt <= now)
        {
            verification.Invalidate(now);
            await db.SaveChangesAsync(cancellationToken);
            return ConfirmStatus.Expired;
        }
        if (verification.InvalidatedAt is not null)
            return ConfirmStatus.Unavailable;
        if (user.NormalizedPhoneNumber is null
            || !string.Equals(
                user.NormalizedPhoneNumber,
                verification.NormalizedPhoneNumber,
                StringComparison.Ordinal))
        {
            verification.Invalidate(now);
            await db.SaveChangesAsync(cancellationToken);
            return ConfirmStatus.Unavailable;
        }

        var normalizedCode = code?.Trim();
        var valid = normalizedCode is not null
            && LooksLikeCode(normalizedCode)
            && PhoneVerificationCodeHasher.Verify(
                normalizedCode, verification.CodeHash, verification.CodeSalt);
        if (!valid)
        {
            verification.RegisterFailedAttempt(now);
            await db.SaveChangesAsync(cancellationToken);
            return verification.InvalidatedAt is not null
                ? ConfirmStatus.AttemptsExhausted
                : ConfirmStatus.InvalidCode;
        }

        user.ConfirmPhoneNumber();
        verification.Confirm(now);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "WhatsApp phone verification {VerificationId} completed for user {UserId} phone {Phone}.",
            verification.Id,
            user.Id,
            PhoneNumberNormalizer.Mask(user.NormalizedPhoneNumber));
        return ConfirmStatus.Confirmed;
    }

    public async Task<ProcessResult> TryProcessAsync(
        NormalizedWhatsAppMessage message,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        if (message.MessageType != "text"
            || string.IsNullOrWhiteSpace(message.Text))
            return ProcessResult.NotHandled;
        var text = message.Text.Trim();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var verification = await db.WhatsAppPhoneVerifications
            .Where(x => x.NormalizedPhoneNumber == normalizedPhoneNumber
                && x.Purpose == WhatsAppChallengePurpose.PhoneVerification
                && x.ConsumedAt == null && x.InvalidatedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification is null)
        {
            if (!LooksLikeCode(text)) return ProcessResult.NotHandled;
            var previous = await db.WhatsAppPhoneVerifications.AsNoTracking()
                .Where(x => x.NormalizedPhoneNumber == normalizedPhoneNumber)
                .Where(x =>
                    x.Purpose == WhatsAppChallengePurpose.PhoneVerification)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return previous is not null
                && PhoneVerificationCodeHasher.Verify(
                    text, previous.CodeHash, previous.CodeSalt)
                    ? new(true, previous.UserId,
                        "Este código não está mais disponível. Solicite um novo código no Comvy.",
                        "phone_verification_unavailable")
                    : ProcessResult.NotHandled;
        }

        if (verification.ExpiresAt <= now)
        {
            verification.Invalidate(now);
            await db.SaveChangesAsync(cancellationToken);
            return LooksLikeCode(text)
                ? new(true, verification.UserId,
                    "Este código expirou. Solicite um novo código no Comvy.",
                    "phone_verification_expired")
                : ProcessResult.NotHandled;
        }

        var valid = LooksLikeCode(text)
            && PhoneVerificationCodeHasher.Verify(
                text, verification.CodeHash, verification.CodeSalt);
        if (!valid)
        {
            verification.RegisterFailedAttempt(now);
            await db.SaveChangesAsync(cancellationToken);
            var exhausted = verification.InvalidatedAt is not null;
            return new(true, verification.UserId,
                exhausted
                    ? "O limite de tentativas foi atingido. Solicite um novo código no Comvy."
                    : "Código inválido. Confira a mensagem recebida e tente novamente.",
                exhausted
                    ? "phone_verification_attempts_exhausted"
                    : "phone_verification_invalid_code");
        }

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == verification.UserId
                && x.IsActive
                && x.NormalizedPhoneNumber == normalizedPhoneNumber,
            cancellationToken);
        if (user is null)
        {
            verification.Invalidate(now);
            await db.SaveChangesAsync(cancellationToken);
            return new(true, verification.UserId,
                "Não foi possível confirmar este telefone. Solicite um novo código no Comvy.",
                "phone_verification_user_mismatch");
        }

        user.ConfirmPhoneNumber();
        verification.Confirm(now);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "WhatsApp phone verification {VerificationId} completed for user {UserId} phone {Phone}.",
            verification.Id,
            user.Id,
            PhoneNumberNormalizer.Mask(normalizedPhoneNumber));
        return new(true, user.Id,
            "Telefone confirmado com sucesso no Comvy.",
            "phone_verification_confirmed");
    }

    internal static string Message(string code) =>
        $"Seu código de confirmação da Comvy é: {code}. "
        + $"Ele expira em {ValidityMinutes} minutos. Não compartilhe este código.";

    private static bool LooksLikeCode(string value) =>
        value.Length == 6 && value.All(char.IsAsciiDigit);

    public sealed record StartResult(
        StartStatus Status,
        DateTime? ExpiresAt = null,
        DateTime? RetryAfter = null);

    public sealed record SafeStatus(
        string? MaskedPhoneNumber,
        bool Confirmed,
        bool ActiveChallenge,
        DateTime? ExpiresAt,
        bool CanResend,
        DateTime? CanResendAt);

    public sealed record ProcessResult(
        bool Handled, Guid? UserId, string? Response, string? Result)
    {
        public static ProcessResult NotHandled { get; } =
            new(false, null, null, null);
    }

    public enum StartStatus
    {
        Started,
        AlreadyConfirmed,
        NoPhone,
        Inactive,
        NotFound,
        TooSoon,
        RateLimited,
        Unavailable
    }

    public enum ConfirmStatus
    {
        Confirmed,
        AlreadyConfirmed,
        InvalidCode,
        Expired,
        AttemptsExhausted,
        Used,
        Unavailable,
        Inactive,
        NotFound
    }
}
