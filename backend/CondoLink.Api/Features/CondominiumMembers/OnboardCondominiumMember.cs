using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CondoLink.Api.Features.Auth;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CondoLink.Api.Features.Management;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class OnboardCondominiumMember
{
    public static IEndpointRouteBuilder MapOnboardCondominiumMember(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/onboard", HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid condominiumId, Request request,
        ClaimsPrincipal principal, UserManager<ApplicationUser> userManager, AppDbContext dbContext,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var authenticatedUserId))
            return Results.Json(new { error = "Invalid authenticated user." }, statusCode: 401);

        var authenticatedUser = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.Id == authenticatedUserId).Select(x => new { x.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (authenticatedUser is null)
            return Results.Json(new { error = "Authenticated user was not found." }, statusCode: 401);
        if (!authenticatedUser.IsActive)
            return Results.Json(new { error = "Only condominium managers can onboard members." }, statusCode: 403);

        var condominium = await dbContext.Condominiums.AsNoTracking().Where(x => x.Id == condominiumId)
            .Select(x => new { x.IsActive, x.Name }).SingleOrDefaultAsync(cancellationToken);
        if (condominium is null) return Results.NotFound(new { error = "Condominium not found." });

        var manager = await SubManagerAccess.HasAsync(dbContext, authenticatedUserId, condominiumId, SubManagerModule.Management, cancellationToken);
        /* var manager = await dbContext.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == authenticatedUserId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(x => (x.Role == CondominiumRole.Manager || x.Role == CondominiumRole.SubManager) && x.IsActive && x.RevokedAt == null),
                x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(cancellationToken); */
        if (!manager)
            return Results.Json(new { error = "Only condominium managers can onboard members." }, statusCode: 403);
        if (!condominium.IsActive)
            return Results.Conflict(new { error = "Inactive condominium cannot receive new members." });

        if (string.IsNullOrWhiteSpace(request.FullName)) return Results.BadRequest(new { error = "Full name is required." });
        var fullName = request.FullName.Trim();
        if (fullName.Length > 200) return Results.BadRequest(new { error = "Full name must not exceed 200 characters." });
        if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new { error = "Email is required." });
        var email = request.Email.Trim().ToLowerInvariant();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) return Results.BadRequest(new { error = "Email is invalid." });
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (phone?.Length > 30) return Results.BadRequest(new { error = "PhoneNumber must not exceed 30 characters." });
        var normalizedPhone = Domain.PhoneNumberNormalizer.Normalize(phone);
        if (phone is not null && normalizedPhone is null)
            return Results.BadRequest(new { error = "PhoneNumber must be valid; include + and the country code outside Brazil." });
        var accessChannel = ParseAccessChannel(request.FirstAccessChannel, request.SendAccessEmail);
        var emailDeliveryEnabled = request.EmailDeliveryEnabled || request.SendAccessEmail;
        if (accessChannel is null)
            return Results.BadRequest(new { error = "FirstAccessChannel must be WhatsApp, Email, WhatsAppAndEmail or None." });
        if ((accessChannel is FirstAccessChannel.WhatsApp or FirstAccessChannel.WhatsAppAndEmail)
            && normalizedPhone is null)
            return Results.BadRequest(new { error = "WhatsApp first access requires a valid phone number." });
        if ((accessChannel is FirstAccessChannel.Email or FirstAccessChannel.WhatsAppAndEmail)
            && !emailDeliveryEnabled)
            return Results.BadRequest(new { error = "Email first access requires a deliverable email address." });
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is { IsActive: false })
            return Results.Conflict(new { error = "Inactive user cannot be associated." });
        if (existingUser is null && normalizedPhone is not null
            && await dbContext.Users.AsNoTracking().AnyAsync(
                x => x.NormalizedPhoneNumber == normalizedPhone,
                cancellationToken))
            return Results.Conflict(new { error = "A user with this phone number already exists." });

        UnitRelationshipType? relationship = null;
        if (request.UnitId is null)
        {
            if (request.RelationshipType is not null || request.IsResident || request.IsPrimaryResidence)
                return Results.BadRequest(new { error = "Unit relationship fields require a target unit." });
        }
        else
        {
            var unit = await dbContext.Units.AsNoTracking().Where(x => x.Id == request.UnitId)
                .Select(x => new { x.CondominiumId, x.IsActive }).SingleOrDefaultAsync(cancellationToken);
            if (unit is null) return Results.NotFound(new { error = "Unit not found." });
            if (unit.CondominiumId != condominiumId) return Results.BadRequest(new { error = "Target unit must belong to the condominium." });
            if (!unit.IsActive) return Results.Conflict(new { error = "Inactive unit cannot receive new memberships." });
            if (!TryRelationship(request.RelationshipType, out var parsed))
                return Results.BadRequest(new { error = "Relationship type must be Owner, Tenant or AuthorizedOccupant." });
            relationship = parsed;
            if (request.IsPrimaryResidence && !request.IsResident)
                return Results.BadRequest(new { error = "Primary residence requires the user to be a resident." });
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
                    if (identityResult.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName")) return DuplicateEmail();
                    return Results.BadRequest(new { errors = identityResult.Errors.Select(x => x.Description).ToArray() });
                }
            }
            else if (user.MustChangePassword)
            {
                user.SetEmailDeliveryEnabled(emailDeliveryEnabled);
            }

            var membership = await dbContext.CondominiumMemberships.SingleOrDefaultAsync(
                x => x.UserId == user.Id && x.CondominiumId == condominiumId, cancellationToken);
            if (membership is { IsActive: false })
                return Results.Conflict(new { error = "Inactive condominium membership cannot be reused." });
            if (membership is null)
            {
                membership = new CondominiumMembership(user.Id, condominiumId);
                dbContext.CondominiumMemberships.Add(membership);
            }

            var role = await dbContext.CondominiumMembershipRoles.SingleOrDefaultAsync(
                x => x.CondominiumMembershipId == membership.Id && x.Role == CondominiumRole.Resident, cancellationToken);
            if (role is { IsActive: false })
                return Results.Conflict(new { error = "Inactive resident role cannot be reused." });
            if (role is null) dbContext.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));

            UnitMembership? unitMembership = null;
            if (request.UnitId.HasValue)
            {
                unitMembership = await dbContext.UnitMemberships.SingleOrDefaultAsync(x =>
                    x.UserId == user.Id && x.UnitId == request.UnitId.Value && x.RelationshipType == relationship!.Value,
                    cancellationToken);
                if (unitMembership is null)
                {
                    unitMembership = new UnitMembership(user.Id, request.UnitId.Value, relationship!.Value, request.IsResident, request.IsPrimaryResidence);
                    dbContext.UnitMemberships.Add(unitMembership);
                }
                else if (!unitMembership.IsActive)
                {
                    unitMembership.Reactivate(request.IsResident, request.IsPrimaryResidence, DateTime.UtcNow);
                }
                else
                {
                    unitMembership.Update(relationship!.Value, request.IsResident, request.IsPrimaryResidence);
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var firstAccessService = services.GetService<FirstAccessService>();
            var whatsapp = services.GetService<FirstAccessWhatsAppInvitationService>();
            var emailSent = false;
            var whatsappQueued = false;
            if (user.MustChangePassword && accessChannel == FirstAccessChannel.Email
                && firstAccessService is not null)
                emailSent = await firstAccessService.SendAsync(user, condominium.Name, cancellationToken);
            else if (user.MustChangePassword && accessChannel == FirstAccessChannel.WhatsApp
                     && whatsapp is not null)
                whatsappQueued = await whatsapp.EnqueueAsync(user, condominiumId, condominium.Name,
                    request.InvitationOperationId ?? $"onboard:{user.Id:N}", cancellationToken);
            else if (user.MustChangePassword && accessChannel == FirstAccessChannel.WhatsAppAndEmail
                     && firstAccessService is not null && whatsapp is not null)
            {
                var operationId = request.InvitationOperationId ?? $"onboard:{user.Id:N}";
                var combined = await whatsapp.DeliverBothAsync(user, condominiumId,
                    condominium.Name, operationId, cancellationToken);
                emailSent = combined.EmailSent;
                whatsappQueued = combined.WhatsAppQueued;
            }

            return Results.Created($"/users/{user.Id}", new Response(
                new UserResponse(user.Id, user.FullName, user.Email!, user.PhoneNumber, user.IsActive),
                new MembershipResponse(membership.Id, membership.CondominiumId, membership.IsActive, membership.JoinedAt),
                [CondominiumRole.Resident.ToString()],
                unitMembership is null ? null : new UnitMembershipResponse(unitMembership.Id, unitMembership.UnitId,
                    unitMembership.RelationshipType.ToString(), unitMembership.IsResident, unitMembership.IsPrimaryResidence),
                isNewUser,
                whatsappQueued ? "InviteQueued" : emailSent ? "InviteSent"
                    : user.MustChangePassword ? "Pending" : "Completed",
                emailSent,
                whatsappQueued));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            if (await userManager.FindByEmailAsync(email) is not null) return DuplicateEmail();
            if (exception.InnerException is PostgresException
                {
                    ConstraintName:
                        ApplicationUserConfiguration
                            .UniqueNormalizedPhoneNumberIndex
                })
                return Results.Conflict(new
                    { error = "A user with this phone number already exists." });
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
        string.IsNullOrWhiteSpace(value)
            ? legacyEmail ? FirstAccessChannel.Email : FirstAccessChannel.None
            : Enum.TryParse<FirstAccessChannel>(value, true, out var channel)
                && Enum.IsDefined(channel) ? channel : null;

    private enum FirstAccessChannel { None, Email, WhatsApp, WhatsAppAndEmail }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[14];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private static IResult DuplicateEmail() => Results.Conflict(new { error = "A user with this email already exists." });
    public sealed record Request(string? FullName, string? Email, string? PhoneNumber, Guid? UnitId,
        string? RelationshipType, bool IsResident, bool IsPrimaryResidence,
        string? FirstAccessChannel = null, bool EmailDeliveryEnabled = false,
        bool SendAccessEmail = false, string? InvitationOperationId = null);
    public sealed record UserResponse(Guid Id, string FullName, string Email, string? PhoneNumber, bool IsActive);
    public sealed record MembershipResponse(Guid Id, Guid CondominiumId, bool IsActive, DateTime JoinedAt);
    public sealed record UnitMembershipResponse(Guid Id, Guid UnitId, string RelationshipType, bool IsResident, bool IsPrimaryResidence);
    public sealed record Response(UserResponse User, MembershipResponse Membership, IReadOnlyList<string> Roles,
        UnitMembershipResponse? UnitMembership, bool IsNewUser, string FirstAccessStatus,
        bool EmailSent = false, bool WhatsAppQueued = false);
}
