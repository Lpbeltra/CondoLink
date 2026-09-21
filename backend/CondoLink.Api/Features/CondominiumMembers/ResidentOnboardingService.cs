using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.CondominiumMembers;

public sealed class ResidentOnboardingService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    IServiceProvider services)
{
    public async Task<ResidentOnboardingResult> OnboardAsync(
        Guid authenticatedUserId,
        Guid condominiumId,
        ResidentOnboardingInput input,
        CancellationToken cancellationToken)
    {
        var authenticatedUser = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.Id == authenticatedUserId).Select(x => new { x.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (authenticatedUser is null)
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.AuthenticatedUserNotFound, "Authenticated user was not found.");
        if (!authenticatedUser.IsActive)
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.Forbidden, "Only condominium managers can onboard members.");

        var condominium = await dbContext.Condominiums.AsNoTracking().Where(x => x.Id == condominiumId)
            .Select(x => new { x.IsActive, x.Name }).SingleOrDefaultAsync(cancellationToken);
        if (condominium is null) return ResidentOnboardingResult.Failure(ResidentOnboardingError.CondominiumNotFound, "Condominium not found.");

        var manager = await SubManagerAccess.HasAsync(dbContext, authenticatedUserId, condominiumId, SubManagerModule.Management, cancellationToken);
        if (!manager)
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.Forbidden, "Only condominium managers can onboard members.");
        if (!condominium.IsActive)
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.InactiveCondominium, "Inactive condominium cannot receive new members.");

        if (string.IsNullOrWhiteSpace(input.FullName)) return ResidentOnboardingResult.BadRequest("Full name is required.");
        var fullName = input.FullName.Trim();
        if (fullName.Length > 200) return ResidentOnboardingResult.BadRequest("Full name must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(input.Email)) return ResidentOnboardingResult.BadRequest("Email is required.");
        var email = input.Email.Trim().ToLowerInvariant();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) return ResidentOnboardingResult.BadRequest("Email is invalid.");
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone?.Length > 30) return ResidentOnboardingResult.BadRequest("PhoneNumber must not exceed 30 characters.");
        var normalizedPhone = Domain.PhoneNumberNormalizer.Normalize(phone);
        if (phone is not null && normalizedPhone is null)
            return ResidentOnboardingResult.BadRequest("PhoneNumber must be valid; include + and the country code outside Brazil.");
        var accessChannel = ParseAccessChannel(input.FirstAccessChannel, input.SendAccessEmail);
        var emailDeliveryEnabled = input.EmailDeliveryEnabled || input.SendAccessEmail;
        if (accessChannel is null)
            return ResidentOnboardingResult.BadRequest("FirstAccessChannel must be WhatsApp, Email, WhatsAppAndEmail or None.");
        if ((accessChannel is FirstAccessChannel.WhatsApp or FirstAccessChannel.WhatsAppAndEmail) && normalizedPhone is null)
            return ResidentOnboardingResult.BadRequest("WhatsApp first access requires a valid phone number.");
        if ((accessChannel is FirstAccessChannel.Email or FirstAccessChannel.WhatsAppAndEmail) && !emailDeliveryEnabled)
            return ResidentOnboardingResult.BadRequest("Email first access requires a deliverable email address.");
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is { IsActive: false })
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.InactiveUser, "Inactive user cannot be associated.");
        if (existingUser is null && normalizedPhone is not null
            && await dbContext.Users.AsNoTracking().AnyAsync(x => x.NormalizedPhoneNumber == normalizedPhone, cancellationToken))
            return ResidentOnboardingResult.Failure(ResidentOnboardingError.DuplicatePhoneNumber, "A user with this phone number already exists.");

        UnitRelationshipType? relationship = null;
        if (input.UnitId is null)
        {
            if (input.RelationshipType is not null || input.IsResident || input.IsPrimaryResidence)
                return ResidentOnboardingResult.BadRequest("Unit relationship fields require a target unit.");
        }
        else
        {
            var unit = await dbContext.Units.AsNoTracking().Where(x => x.Id == input.UnitId)
                .Select(x => new { x.CondominiumId, x.IsActive }).SingleOrDefaultAsync(cancellationToken);
            if (unit is null) return ResidentOnboardingResult.Failure(ResidentOnboardingError.UnitNotFound, "Unit not found.");
            if (unit.CondominiumId != condominiumId) return ResidentOnboardingResult.BadRequest("Target unit must belong to the condominium.");
            if (!unit.IsActive) return ResidentOnboardingResult.Failure(ResidentOnboardingError.InactiveUnit, "Inactive unit cannot receive new memberships.");
            if (!TryRelationship(input.RelationshipType, out var parsed))
                return ResidentOnboardingResult.BadRequest("Relationship type must be Owner, Tenant or AuthorizedOccupant.");
            relationship = parsed;
            if (input.IsPrimaryResidence && !input.IsResident)
                return ResidentOnboardingResult.BadRequest("Primary residence requires the user to be a resident.");
        }

        var isNewUser = existingUser is null;
        var initialPassword = isNewUser ? GeneratePassword() : null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = existingUser ?? new ApplicationUser(fullName, email, phone);
            if (isNewUser)
            {
                user.RequirePasswordChange();
                user.SetEmailDeliveryEnabled(emailDeliveryEnabled);
                var identityResult = await userManager.CreateAsync(user, initialPassword!);
                if (!identityResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (identityResult.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName"))
                        return ResidentOnboardingResult.Failure(ResidentOnboardingError.DuplicateEmail, "A user with this email already exists.");
                    return ResidentOnboardingResult.ValidationFailure(identityResult.Errors.Select(x => x.Description).ToArray());
                }
            }
            else if (user.MustChangePassword)
                user.SetEmailDeliveryEnabled(emailDeliveryEnabled);

            var membership = await dbContext.CondominiumMemberships.SingleOrDefaultAsync(
                x => x.UserId == user.Id && x.CondominiumId == condominiumId, cancellationToken);
            if (membership is { IsActive: false })
                return ResidentOnboardingResult.Failure(ResidentOnboardingError.InactiveMembership, "Inactive condominium membership cannot be reused.");
            if (membership is null)
            {
                membership = new CondominiumMembership(user.Id, condominiumId);
                dbContext.CondominiumMemberships.Add(membership);
            }

            var role = await dbContext.CondominiumMembershipRoles.SingleOrDefaultAsync(
                x => x.CondominiumMembershipId == membership.Id && x.Role == CondominiumRole.Resident, cancellationToken);
            if (role is { IsActive: false })
                return ResidentOnboardingResult.Failure(ResidentOnboardingError.InactiveResidentRole, "Inactive resident role cannot be reused.");
            if (role is null) dbContext.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));

            UnitMembership? unitMembership = null;
            if (input.UnitId.HasValue)
            {
                unitMembership = await dbContext.UnitMemberships.SingleOrDefaultAsync(x => x.UserId == user.Id && x.UnitId == input.UnitId.Value && x.RelationshipType == relationship!.Value, cancellationToken);
                if (unitMembership is null)
                {
                    unitMembership = new UnitMembership(user.Id, input.UnitId.Value, relationship!.Value, input.IsResident, input.IsPrimaryResidence);
                    dbContext.UnitMemberships.Add(unitMembership);
                }
                else if (!unitMembership.IsActive)
                    unitMembership.Reactivate(input.IsResident, input.IsPrimaryResidence, DateTime.UtcNow);
                else
                    unitMembership.Update(relationship!.Value, input.IsResident, input.IsPrimaryResidence);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var firstAccessService = services.GetService<FirstAccessService>();
            var whatsapp = services.GetService<FirstAccessWhatsAppInvitationService>();
            var emailSent = false;
            var whatsappQueued = false;
            if (user.MustChangePassword && accessChannel == FirstAccessChannel.Email && firstAccessService is not null)
                emailSent = await firstAccessService.SendAsync(user, condominium.Name, cancellationToken);
            else if (user.MustChangePassword && accessChannel == FirstAccessChannel.WhatsApp && whatsapp is not null)
                whatsappQueued = await whatsapp.EnqueueAsync(user, condominiumId, condominium.Name, input.InvitationOperationId ?? $"onboard:{user.Id:N}", cancellationToken);
            else if (user.MustChangePassword && accessChannel == FirstAccessChannel.WhatsAppAndEmail && firstAccessService is not null && whatsapp is not null)
            {
                var operationId = input.InvitationOperationId ?? $"onboard:{user.Id:N}";
                var combined = await whatsapp.DeliverBothAsync(user, condominiumId, condominium.Name, operationId, cancellationToken);
                emailSent = combined.EmailSent;
                whatsappQueued = combined.WhatsAppQueued;
            }

            return ResidentOnboardingResult.Success(user, membership, unitMembership, isNewUser, emailSent, whatsappQueued);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            if (await userManager.FindByEmailAsync(email) is not null)
                return ResidentOnboardingResult.Failure(ResidentOnboardingError.DuplicateEmail, "A user with this email already exists.");
            if (exception.InnerException is PostgresException { ConstraintName: ApplicationUserConfiguration.UniqueNormalizedPhoneNumberIndex })
                return ResidentOnboardingResult.Failure(ResidentOnboardingError.DuplicatePhoneNumber, "A user with this phone number already exists.");
            throw;
        }
    }

    private static bool TryRelationship(string? value, out UnitRelationshipType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _)
            && Enum.TryParse(value, true, out type) && Enum.IsDefined(type);
    }

    private static FirstAccessChannel? ParseAccessChannel(string? value, bool legacyEmail) =>
        string.IsNullOrWhiteSpace(value) ? legacyEmail ? FirstAccessChannel.Email : FirstAccessChannel.None
        : Enum.TryParse<FirstAccessChannel>(value, true, out var channel) && Enum.IsDefined(channel) ? channel : null;

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[14];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)]; chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)]; chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var i = chars.Length - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars);
    }

    private enum FirstAccessChannel { None, Email, WhatsApp, WhatsAppAndEmail }
}

public sealed record ResidentOnboardingInput(string? FullName, string? Email, string? PhoneNumber, Guid? UnitId, string? RelationshipType, bool IsResident, bool IsPrimaryResidence, string? FirstAccessChannel = null, bool EmailDeliveryEnabled = false, bool SendAccessEmail = false, string? InvitationOperationId = null);
public enum ResidentOnboardingError { None, AuthenticatedUserNotFound, Forbidden, CondominiumNotFound, InactiveCondominium, InactiveUser, DuplicatePhoneNumber, UnitNotFound, InactiveUnit, DuplicateEmail, InactiveMembership, InactiveResidentRole, BadRequest, Validation }
public sealed record ResidentOnboardingResult(bool Succeeded, ApplicationUser? User, CondominiumMembership? Membership, UnitMembership? UnitMembership, bool IsNewUser, bool EmailSent, bool WhatsAppQueued, ResidentOnboardingError Error, string? ErrorMessage, IReadOnlyList<string>? ValidationErrors)
{
    public static ResidentOnboardingResult Success(ApplicationUser user, CondominiumMembership membership, UnitMembership? unitMembership, bool isNewUser, bool emailSent, bool whatsappQueued) => new(true, user, membership, unitMembership, isNewUser, emailSent, whatsappQueued, ResidentOnboardingError.None, null, null);
    public static ResidentOnboardingResult Failure(ResidentOnboardingError error, string errorMessage) => new(false, null, null, null, false, false, false, error, errorMessage, null);
    public static ResidentOnboardingResult BadRequest(string errorMessage) => Failure(ResidentOnboardingError.BadRequest, errorMessage);
    public static ResidentOnboardingResult ValidationFailure(IReadOnlyList<string> errors) => new(false, null, null, null, false, false, false, ResidentOnboardingError.Validation, null, errors);
}
