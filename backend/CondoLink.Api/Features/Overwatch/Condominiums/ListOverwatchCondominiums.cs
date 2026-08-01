using CondoLink.Infrastructure.Persistence;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class ListOverwatchCondominiums
{
    public static IEndpointRouteBuilder MapListOverwatchCondominiums(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List condominiums")
            .WithDescription(
                "Lists all condominiums registered in CondoLink.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? search,
        bool? isActive,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Condominiums
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();

            query = query.Where(condominium =>
                EF.Functions.ILike(
                    condominium.Name,
                    $"%{normalizedSearch}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(condominium =>
                condominium.IsActive == isActive.Value);
        }

        var condominiums = await query
            .OrderBy(condominium => condominium.Name)
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
            .ToListAsync(cancellationToken);

        return Results.Ok(condominiums);
    }

}
