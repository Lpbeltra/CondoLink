using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.UnitMemberships;

public static class CreateUnitMembership
{
    public static IEndpointRouteBuilder MapCreateUnitMembership(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/units/{unitId:guid}/memberships", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid unitId,
        Request request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(authenticatedUserIdValue, out var authenticatedUserId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == authenticatedUserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var unit = await dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.Id == unitId)
            .Select(unit => new { unit.CondominiumId, unit.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return Results.NotFound(new { error = "Unit not found." });
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == unit.CondominiumId)
            .Select(condominium => new { condominium.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == unit.CondominiumId
                && membership.IsActive
                && membership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        if (!isCondominiumManager)
        {
            return Results.Json(
                new { error = "Only condominium managers can create unit memberships." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!unit.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive unit cannot receive new memberships." });
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(new
            {
                error = "Inactive condominium cannot receive new unit memberships."
            });
        }

        var targetUser = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == request.UserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (targetUser is null)
        {
            return Results.NotFound(new { error = "User not found." });
        }

        if (!targetUser.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive user cannot be linked to a unit." });
        }

        var isActiveCondominiumMember = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == request.UserId
                    && membership.CondominiumId == unit.CondominiumId
                    && membership.IsActive
                    && membership.EndedAt == null,
                cancellationToken);

        if (!isActiveCondominiumMember)
        {
            return Results.Conflict(new
            {
                error = "User must be an active condominium member before being linked to a unit."
            });
        }

        if (!TryParseRelationshipType(request.RelationshipType, out var relationshipType))
        {
            return Results.BadRequest(new
            {
                error = "Relationship type must be Owner, Tenant or AuthorizedOccupant."
            });
        }

        if (request.IsPrimaryResidence && !request.IsResident)
        {
            return Results.BadRequest(new
            {
                error = "Primary residence requires the user to be a resident."
            });
        }

        var existingRelationship = await dbContext.UnitMemberships
            .SingleOrDefaultAsync(
                membership =>
                    membership.UserId == request.UserId
                    && membership.UnitId == unitId
                    && membership.RelationshipType == relationshipType,
                cancellationToken);

        if (existingRelationship?.IsActive == true)
        {
            return DuplicateRelationshipConflict();
        }

        if (existingRelationship is not null)
        {
            existingRelationship.Reactivate(request.IsResident, request.IsPrimaryResidence, DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new Response(existingRelationship.Id, existingRelationship.UserId, existingRelationship.UnitId, existingRelationship.RelationshipType.ToString(), existingRelationship.IsResident, existingRelationship.IsPrimaryResidence, existingRelationship.IsActive, existingRelationship.StartedAt, existingRelationship.EndedAt, existingRelationship.CreatedAt));
        }

        var unitMembership = new UnitMembership(
            request.UserId,
            unitId,
            relationshipType,
            request.IsResident,
            request.IsPrimaryResidence);

        dbContext.UnitMemberships.Add(unitMembership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateRelationshipViolation(exception))
        {
            return DuplicateRelationshipConflict();
        }

        var response = new Response(
            unitMembership.Id,
            unitMembership.UserId,
            unitMembership.UnitId,
            unitMembership.RelationshipType.ToString(),
            unitMembership.IsResident,
            unitMembership.IsPrimaryResidence,
            unitMembership.IsActive,
            unitMembership.StartedAt,
            unitMembership.EndedAt,
            unitMembership.CreatedAt);

        return Results.Created($"/unit-memberships/{unitMembership.Id}", response);
    }

    private static bool TryParseRelationshipType(
        string? value,
        out UnitRelationshipType relationshipType)
    {
        relationshipType = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out relationshipType)
            && Enum.IsDefined(relationshipType);
    }

    private static bool IsDuplicateRelationshipViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UnitMembershipConfiguration.UniqueUserUnitRelationshipIndex
        };
    }

    private static IResult DuplicateRelationshipConflict()
    {
        return Results.Conflict(new
        {
            error = "This unit relationship is already associated with the user."
        });
    }

    public sealed record Request(
        Guid UserId,
        string? RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence);

    public sealed record Response(
        Guid Id,
        Guid UserId,
        Guid UnitId,
        string RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence,
        bool IsActive,
        DateTime StartedAt,
        DateTime? EndedAt,
        DateTime CreatedAt);
}
