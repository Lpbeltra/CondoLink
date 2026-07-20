using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.CondominiumMembers;

public sealed class CondominiumMembershipService(
    AppDbContext dbContext)
{
    public async Task<AddMemberResult> AddMemberAsync(
        Guid condominiumId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return AddMemberResult.Failure(
                AddMemberError.InvalidUserId,
                "UserId is required.");
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(current => current.Id == condominiumId)
            .Select(current => new
            {
                current.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return AddMemberResult.Failure(
                AddMemberError.CondominiumNotFound,
                "Condominium not found.");
        }

        if (!condominium.IsActive)
        {
            return AddMemberResult.Failure(
                AddMemberError.InactiveCondominium,
                "Inactive condominium cannot receive new members.");
        }

        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(current => current.Id == userId)
            .Select(current => new
            {
                current.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return AddMemberResult.Failure(
                AddMemberError.UserNotFound,
                "User not found.");
        }

        if (!user.IsActive)
        {
            return AddMemberResult.Failure(
                AddMemberError.InactiveUser,
                "Inactive user cannot be added to a condominium.");
        }

        var alreadyExists = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.CondominiumId == condominiumId,
                cancellationToken);

        if (alreadyExists)
        {
            return AddMemberResult.Failure(
                AddMemberError.DuplicateMembership,
                "User is already associated with this condominium.");
        }

        var membership = new CondominiumMembership(
            userId,
            condominiumId);

        dbContext.CondominiumMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateMembershipViolation(exception))
        {
            return AddMemberResult.Failure(
                AddMemberError.DuplicateMembership,
                "User is already associated with this condominium.");
        }

        return AddMemberResult.Success(membership);
    }

    private static bool IsDuplicateMembershipViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                CondominiumMembershipConfiguration.UniqueUserCondominiumIndex
        };
    }
}

public enum AddMemberError
{
    None,
    InvalidUserId,
    CondominiumNotFound,
    InactiveCondominium,
    UserNotFound,
    InactiveUser,
    DuplicateMembership
}

public sealed record AddMemberResult(
    bool Succeeded,
    CondominiumMembership? Membership,
    AddMemberError Error,
    string? ErrorMessage)
{
    public static AddMemberResult Success(
        CondominiumMembership membership)
    {
        return new AddMemberResult(
            true,
            membership,
            AddMemberError.None,
            null);
    }

    public static AddMemberResult Failure(
        AddMemberError error,
        string errorMessage)
    {
        return new AddMemberResult(
            false,
            null,
            error,
            errorMessage);
    }
}