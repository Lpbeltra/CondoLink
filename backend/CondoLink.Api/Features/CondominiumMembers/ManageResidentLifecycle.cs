using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class ManageResidentLifecycle
{
    public static IEndpointRouteBuilder MapManageResidentLifecycle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/{userId:guid}/unit-memberships/{unitMembershipId:guid}/inactivate", InactivateAsync).RequireAuthorization();
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/{userId:guid}/unit-memberships/{unitMembershipId:guid}/reactivate", ReactivateAsync).RequireAuthorization();
        endpoints.MapDelete("/condominiums/{condominiumId:guid}/members/{userId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> InactivateAsync(Guid condominiumId, Guid userId, Guid unitMembershipId,
        ClaimsPrincipal principal, AppDbContext db, ILoggerFactory logs, CancellationToken ct)
    {
        var administratorId = await GetAuthorizedAdministratorIdAsync(condominiumId, principal, db, ct);
        if (administratorId is null) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var link = await FindLinkAsync(condominiumId, userId, unitMembershipId, db, ct);
        if (link is null) return Results.NotFound(new { error = "Vínculo residencial não encontrado." });
        if (!link.IsActive) return Results.Conflict(new { error = "Este vínculo já está inativo." });
        link.End(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logs.CreateLogger("ResidentLifecycle").LogInformation(
            "Resident lifecycle {Operation}. CondominiumId {CondominiumId}; UserId {UserId}; UnitMembershipId {UnitMembershipId}; AdministratorId {AdministratorId}; Success {Success}",
            "Inactivate", condominiumId, userId, unitMembershipId, administratorId, true);
        return Results.NoContent();
    }

    private static async Task<IResult> ReactivateAsync(Guid condominiumId, Guid userId, Guid unitMembershipId,
        ClaimsPrincipal principal, AppDbContext db, ILoggerFactory logs, CancellationToken ct)
    {
        var administratorId = await GetAuthorizedAdministratorIdAsync(condominiumId, principal, db, ct);
        if (administratorId is null) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var link = await FindLinkAsync(condominiumId, userId, unitMembershipId, db, ct);
        if (link is null) return Results.NotFound(new { error = "Vínculo residencial não encontrado." });
        if (link.IsActive) return Results.Conflict(new { error = "Este vínculo já está ativo." });
        var duplicate = await db.UnitMemberships.AnyAsync(x => x.Id != link.Id && x.UserId == userId
            && x.UnitId == link.UnitId && x.RelationshipType == link.RelationshipType && x.IsActive, ct);
        if (duplicate) return Results.Conflict(new { error = "Já existe um vínculo ativo equivalente." });
        link.Reactivate(link.IsResident, link.IsPrimaryResidence, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logs.CreateLogger("ResidentLifecycle").LogInformation(
            "Resident lifecycle {Operation}. CondominiumId {CondominiumId}; UserId {UserId}; UnitMembershipId {UnitMembershipId}; AdministratorId {AdministratorId}; Success {Success}",
            "Reactivate", condominiumId, userId, unitMembershipId, administratorId, true);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid condominiumId, Guid userId, ClaimsPrincipal principal,
        AppDbContext db, UserManager<ApplicationUser> users, ILoggerFactory logs, CancellationToken ct)
    {
        var administratorId = await GetAuthorizedAdministratorIdAsync(condominiumId, principal, db, ct);
        if (administratorId is null) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return Results.NotFound();
        var membership = await db.CondominiumMemberships.SingleOrDefaultAsync(
            x => x.CondominiumId == condominiumId && x.UserId == userId, ct);
        if (membership is null) return Results.NotFound();
        if (await HasBlockingHistoryAsync(condominiumId, userId, db, ct))
            return Results.Conflict(new { error = "Este morador possui histórico ou outros vínculos e não pode ser excluído. Você pode inativá-lo." });

        var links = await db.UnitMemberships.Where(x => x.UserId == userId).ToListAsync(ct);
        var roles = await db.CondominiumMembershipRoles.Where(x => x.CondominiumMembershipId == membership.Id).ToListAsync(ct);
        db.UnitMemberships.RemoveRange(links);
        db.CondominiumMembershipRoles.RemoveRange(roles);
        db.CondominiumMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return Results.Conflict(new { error = "Não foi possível excluir este cadastro com segurança." });
        }
        await transaction.CommitAsync(ct);
        logs.CreateLogger("ResidentLifecycle").LogInformation(
            "Resident lifecycle {Operation}. CondominiumId {CondominiumId}; UserId {UserId}; UnitMembershipId {UnitMembershipId}; AdministratorId {AdministratorId}; Success {Success}",
            "Delete", condominiumId, userId, (Guid?)null, administratorId, true);
        return Results.NoContent();
    }

    private static async Task<UnitMembership?> FindLinkAsync(Guid condominiumId, Guid userId,
        Guid linkId, AppDbContext db, CancellationToken ct) => await (
        from link in db.UnitMemberships
        join unit in db.Units on link.UnitId equals unit.Id
        where link.Id == linkId && link.UserId == userId && unit.CondominiumId == condominiumId
        select link).SingleOrDefaultAsync(ct);

    private static async Task<bool> HasBlockingHistoryAsync(Guid condominiumId, Guid userId, AppDbContext db, CancellationToken ct)
    {
        if (await db.CondominiumMemberships.AnyAsync(x => x.UserId == userId && x.CondominiumId != condominiumId, ct)) return true;
        if (await db.UnitMemberships.CountAsync(x => x.UserId == userId, ct) > 1) return true;
        if (await (from membership in db.CondominiumMemberships
                   join role in db.CondominiumMembershipRoles on membership.Id equals role.CondominiumMembershipId
                   where membership.UserId == userId && role.Role == CondominiumRole.Manager
                   select role).AnyAsync(ct)) return true;
        return await db.Requests.AnyAsync(x => x.AuthorUserId == userId, ct)
            || await db.RequestMessages.AnyAsync(x => x.AuthorUserId == userId, ct)
            || await db.RequestStatusHistories.AnyAsync(x => x.ChangedByUserId == userId, ct)
            || await db.RequestAttachments.AnyAsync(x => x.UploadedByUserId == userId, ct)
            || await db.RequestResidentReplyRequirements.AnyAsync(x => x.RequestedByUserId == userId, ct)
            || await db.Notifications.AnyAsync(x => x.RecipientUserId == userId, ct)
            || await db.ManagementCompanyEmployees.AnyAsync(x => x.UserId == userId, ct)
            || await db.WhatsAppOutboundMessages.AnyAsync(x => x.UserId == userId, ct)
            || await db.WhatsAppPhoneVerifications.AnyAsync(x => x.UserId == userId, ct)
            || await db.WhatsAppInboundMessages.AnyAsync(x => x.IdentifiedUserId == userId, ct)
            || await db.WhatsAppSessions.AnyAsync(x => x.UserId == userId, ct)
            || await db.CondominiumDocuments.AnyAsync(x => x.UploadedByUserId == userId, ct)
            || await db.CondominiumAssistantConversations.AnyAsync(x => x.CreatedByUserId == userId, ct);
    }

    private static async Task<Guid?> GetAuthorizedAdministratorIdAsync(Guid condominiumId, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var administratorId)) return null;
        if (principal.IsInRole(DependencyInjection.PlatformAdminRole)) return administratorId;
        var canManage = await db.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == administratorId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null),
                x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
        return canManage ? administratorId : null;
    }
}
