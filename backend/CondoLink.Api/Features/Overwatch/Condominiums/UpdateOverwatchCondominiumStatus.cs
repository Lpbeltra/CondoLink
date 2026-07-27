using CondoLink.Api.Features.Management;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class UpdateOverwatchCondominiumStatus
{
    public static IEndpointRouteBuilder MapUpdateOverwatchCondominiumStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch(
                "/overwatch/condominiums/{id:guid}/status",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Activate or deactivate condominium")
            .WithDescription("Updates the active status of a condominium.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominium = await dbContext.Condominiums
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var managerIds = await (
                from membership in dbContext.CondominiumMemberships
                join role in dbContext.CondominiumMembershipRoles
                    on membership.Id equals role.CondominiumMembershipId
                where membership.CondominiumId == id
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                select membership.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        condominium.SetActiveStatus(request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);

        var managers = await dbContext.Users
            .Where(user => managerIds.Contains(user.Id))
            .ToListAsync(cancellationToken);
        foreach (var manager in managers)
        {
            await ManagementContextReconciler.ReconcileAsync(
                manager, dbContext, cancellationToken);
        }
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new Response(
            condominium.Id,
            condominium.Name,
            condominium.IsActive));
    }

    public sealed record Request(
        bool IsActive);

    public sealed record Response(
        Guid Id,
        string Name,
        bool IsActive);
}
