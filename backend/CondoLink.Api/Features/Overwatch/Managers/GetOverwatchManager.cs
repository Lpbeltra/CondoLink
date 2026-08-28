using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class GetOverwatchManager
{
    public static IEndpointRouteBuilder MapGetOverwatchManager(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/managers/{managerId:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get manager");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var response = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == managerId && (
                dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id
                    && dbContext.Roles.Any(role => role.Id == userRole.RoleId
                        && role.Name == "Manager"))
                || dbContext.CondominiumMemberships.Any(membership =>
                    membership.UserId == user.Id && membership.IsActive
                    && membership.EndedAt == null
                    && dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id
                        && role.Role == CondominiumRole.Manager
                        && role.IsActive && role.RevokedAt == null))))
            .Select(user => new OverwatchManagerResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.Cpf,
                user.Cnpj,
                user.Address,
                user.City,
                user.State,
                user.PixKeyType,
                user.PixKey,
                user.IsActive,
                dbContext.CondominiumMemberships.Count(membership =>
                    membership.UserId == user.Id &&
                    membership.IsActive &&
                    membership.EndedAt == null &&
                    dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id &&
                        role.Role == CondominiumRole.Manager &&
                        role.IsActive &&
                        role.RevokedAt == null)),
                user.CreatedAt,
                user.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return response is null
            ? Results.NotFound(new { message = "Manager not found." })
            : Results.Ok(response);
    }
}
