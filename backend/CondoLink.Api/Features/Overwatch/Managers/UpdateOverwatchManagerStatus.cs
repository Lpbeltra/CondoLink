using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class UpdateOverwatchManagerStatus
{
    public static IEndpointRouteBuilder MapUpdateOverwatchManagerStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/managers/{managerId:guid}/status",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Update manager status");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var manager = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.Id == managerId && role.Name == "Manager"
            select user)
            .SingleOrDefaultAsync(cancellationToken);

        if (manager is null)
        {
            return Results.NotFound(new { message = "Manager not found." });
        }

        manager.SetActiveStatus(request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { manager.Id, manager.IsActive, manager.UpdatedAt });
    }

    public sealed record Request(bool IsActive);
}
