using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CondoLink.Api.Features.Management;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class UpdateCondominiumMember
{
    public static IEndpointRouteBuilder MapUpdateCondominiumMember(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/condominiums/{condominiumId:guid}/members/{userId:guid}",
                HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Guid userId,
        Request request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var callerClaim =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerClaim, out var callerId))
            return Results.Unauthorized();
        var callerActive = await db.Users.AsNoTracking()
            .AnyAsync(item => item.Id == callerId && item.IsActive,
                cancellationToken);
        if (!callerActive) return Results.Unauthorized();
        if (!principal.IsInRole(DependencyInjection.PlatformAdminRole))
        {
            var manager = await SubManagerAccess.HasAsync(db, callerId, condominiumId, SubManagerModule.Management, cancellationToken);
            /* var manager = await db.CondominiumMemberships.AsNoTracking()
                .Where(item =>
                    item.UserId == callerId
                    && item.CondominiumId == condominiumId
                    && item.IsActive
                    && item.EndedAt == null)
                .Join(
                    db.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                        (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                        && role.IsActive
                        && role.RevokedAt == null),
                    membership => membership.Id,
                    role => role.CondominiumMembershipId,
                    (_, _) => true)
                .AnyAsync(cancellationToken); */
            if (!manager) return Results.Forbid();
        }

        var membership = await db.CondominiumMemberships
            .SingleOrDefaultAsync(item =>
                item.UserId == userId
                && item.CondominiumId == condominiumId,
                cancellationToken);
        if (membership is null)
            return Results.NotFound(
                new { error = "Pessoa não encontrada neste condomínio." });
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Results.NotFound(new { error = "Usuário não encontrado." });

        var validation = Validate(request);
        if (validation is not null)
            return Results.BadRequest(new { error = validation });
        var email = request.Email!.Trim().ToLowerInvariant();
        var cpf = RegistrationData.Digits(request.Cpf);
        var cnpj = RegistrationData.Digits(request.Cnpj);
        var normalizedPhone =
            PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (await db.Users.AsNoTracking().AnyAsync(item =>
                item.Id != userId
                && (item.NormalizedEmail == email.ToUpperInvariant()
                    || (normalizedPhone != null
                        && item.NormalizedPhoneNumber == normalizedPhone)
                    || (cpf != null && item.Cpf == cpf)
                    || (cnpj != null && item.Cnpj == cnpj)),
                cancellationToken))
        {
            return Results.Conflict(new
            {
                error =
                    "Já existe outro usuário com o e-mail, telefone, CPF ou CNPJ informado."
            });
        }

        UnitRelationshipType? relationship = null;
        Unit? targetUnit = null;
        UnitMembership? currentLink = null;
        if (request.UnitMembershipId.HasValue)
        {
            currentLink = await (
                    from link in db.UnitMemberships
                    join unit in db.Units on link.UnitId equals unit.Id
                    where link.Id == request.UnitMembershipId
                        && link.UserId == userId
                        && unit.CondominiumId == condominiumId
                    select link)
                .SingleOrDefaultAsync(cancellationToken);
            if (currentLink is null)
                return Results.BadRequest(new
                {
                    error = "O vínculo de unidade informado não é válido."
                });
        }
        if (request.UnitId.HasValue)
        {
            targetUnit = await db.Units.SingleOrDefaultAsync(item =>
                item.Id == request.UnitId
                && item.CondominiumId == condominiumId
                && item.IsActive,
                cancellationToken);
            if (targetUnit is null)
                return Results.BadRequest(new
                {
                    error = "A unidade selecionada não pertence ao condomínio."
                });
            if (!TryRelationship(
                    request.RelationshipType, out var parsedRelationship))
                return Results.BadRequest(new
                {
                    error =
                        "O tipo de vínculo deve ser Owner, Tenant "
                        + "ou AuthorizedOccupant."
                });
            relationship = parsedRelationship;
            if (request.IsPrimaryResidence && !request.IsResident)
                return Results.BadRequest(new
                {
                    error =
                        "Residência principal exige que a pessoa resida na unidade."
                });
        }
        else if (request.RelationshipType is not null
            || request.IsResident
            || request.IsPrimaryResidence)
        {
            return Results.BadRequest(new
            {
                error = "Os dados de vínculo exigem uma unidade."
            });
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);
        user.UpdateManagerProfile(
            request.FullName!,
            request.PhoneNumber,
            cpf,
            cnpj,
            request.Address,
            request.City,
            request.State);
        if (request.MembershipActive) membership.Activate();
        else membership.Deactivate(DateTime.UtcNow);

        if (currentLink is not null
            && (!request.UnitId.HasValue
                || currentLink.UnitId != request.UnitId.Value
                || currentLink.RelationshipType != relationship))
        {
            if (currentLink.IsActive) currentLink.End(DateTime.UtcNow);
            currentLink = null;
        }
        if (targetUnit is not null)
        {
            currentLink ??= await db.UnitMemberships
                .SingleOrDefaultAsync(item =>
                    item.UserId == userId
                    && item.UnitId == targetUnit.Id
                    && item.RelationshipType == relationship,
                    cancellationToken);
            if (currentLink is null)
            {
                currentLink = new UnitMembership(
                    userId,
                    targetUnit.Id,
                    relationship!.Value,
                    request.IsResident,
                    request.IsPrimaryResidence);
                db.UnitMemberships.Add(currentLink);
            }
            else if (!currentLink.IsActive)
            {
                currentLink.Reactivate(
                    request.IsResident,
                    request.IsPrimaryResidence,
                    DateTime.UtcNow);
            }
            else
            {
                currentLink.Update(
                    relationship!.Value,
                    request.IsResident,
                    request.IsPrimaryResidence);
            }
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
                return IdentityFailure(emailResult);
            var userNameResult = await userManager.SetUserNameAsync(user, email);
            if (!userNameResult.Succeeded)
                return IdentityFailure(userNameResult);
        }
        else
        {
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return IdentityFailure(updateResult);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new Response(
            user.Id,
            user.FullName,
            user.Email!,
            user.PhoneNumber,
            user.Cpf,
            user.Cnpj,
            user.Address,
            user.City,
            user.State,
            membership.IsActive,
            currentLink is null
                ? null
                : new UnitLinkResponse(
                    currentLink.Id,
                    currentLink.UnitId,
                    targetUnit!.Identifier,
                    targetUnit.BlockId.HasValue
                        ? await db.CondominiumBlocks.AsNoTracking()
                            .Where(item => item.Id == targetUnit.BlockId)
                            .Select(item => item.Identifier)
                            .SingleOrDefaultAsync(cancellationToken)
                        : null,
                    currentLink.RelationshipType.ToString(),
                    currentLink.IsResident,
                    currentLink.IsPrimaryResidence)));
    }

    private static string? Validate(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return "Informe o nome completo.";
        if (request.FullName.Trim().Length > 200)
            return "O nome deve possuir no máximo 200 caracteres.";
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Trim().Length > 254
            || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return "Informe um e-mail válido.";
        if (RegistrationData.Optional(request.PhoneNumber)?.Length > 30)
            return "O telefone deve possuir no máximo 30 caracteres.";
        if (!PhoneNumberNormalizer.IsValidOptional(request.PhoneNumber))
            return "Informe um telefone válido; fora do Brasil, inclua + e o código do país.";
        if (request.Cpf is not null
            && !RegistrationData.IsValidCpf(request.Cpf))
            return "CPF inválido.";
        if (request.Cnpj is not null
            && !RegistrationData.IsValidCnpj(request.Cnpj))
            return "CNPJ inválido.";
        var state = RegistrationData.State(request.State);
        if (state is not null && !RegistrationData.IsValidState(state))
            return "UF inválida.";
        if (RegistrationData.Optional(request.Address)?.Length > 300)
            return "O endereço deve possuir no máximo 300 caracteres.";
        if (RegistrationData.Optional(request.City)?.Length > 100)
            return "A cidade deve possuir no máximo 100 caracteres.";
        return null;
    }

    private static bool TryRelationship(
        string? value,
        out UnitRelationshipType relationship)
    {
        relationship = default;
        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, true, out relationship)
            && Enum.IsDefined(relationship);
    }

    private static IResult IdentityFailure(IdentityResult result) =>
        Results.BadRequest(new
        {
            error = "Não foi possível atualizar os dados da pessoa.",
            errors = result.Errors.Select(item => item.Description).ToArray()
        });

    public sealed record Request(
        string? FullName,
        string? Email,
        string? PhoneNumber,
        string? Cpf,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        bool MembershipActive,
        Guid? UnitMembershipId,
        Guid? UnitId,
        string? RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence);

    public sealed record UnitLinkResponse(
        Guid UnitMembershipId,
        Guid UnitId,
        string UnitIdentifier,
        string? Block,
        string RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence);

    public sealed record Response(
        Guid UserId,
        string FullName,
        string Email,
        string? PhoneNumber,
        string? Cpf,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        bool MembershipActive,
        UnitLinkResponse? UnitLink);
}
