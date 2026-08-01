using CondoLink.Infrastructure.Persistence;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class GetOverwatchCondominium
{
    public static IEndpointRouteBuilder MapGetOverwatchCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums/{id:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get condominium details")
            .WithDescription(
                "Returns the details of a condominium registered in CondoLink.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == id)
            .Select(condominium => new CondominiumResponse(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.Cnpj,
                condominium.Address,
                condominium.City,
                condominium.State,
                condominium.HasDoorman,
                condominium.IsRemoteDoorman,
                condominium.DoormanContact,
                condominium.WhatsAppUpdatesEnabled,
                condominium.IsActive,
                condominium.CreatedAt,
                condominium.UpdatedAt,
                condominium.ManagementCompanyId,
                condominium.ManagementCompany == null
                    ? null
                    : condominium.ManagementCompany.Name,
                dbContext.CondominiumMemberships.Count(membership =>
                    membership.CondominiumId == condominium.Id &&
                    membership.IsActive &&
                    membership.EndedAt == null &&
                    dbContext.Users.Any(user =>
                        user.Id == membership.UserId &&
                        user.IsActive) &&
                    dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id &&
                        role.Role == CondominiumRole.Manager &&
                        role.IsActive &&
                        role.RevokedAt == null))))
            .FirstOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        return Results.Ok(condominium);
    }

}
