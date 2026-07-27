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
        var response = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking()
                on user.Id equals userRole.UserId
            join identityRole in dbContext.Roles.AsNoTracking()
                on userRole.RoleId equals identityRole.Id
            where user.Id == managerId && identityRole.Name == "Manager"
            select new OverwatchManagerResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.Cpf,
                user.Cnpj,
                user.Address,
                user.City,
                user.State,
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
