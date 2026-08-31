using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public sealed class ManagementCompanyRequestRealtimeService(AppDbContext db, IHubContext<ManagementCompanyRequestRealtimeHub> hub)
{
    public Task BroadcastMessageAsync(ManagementCompanyRequest request, ManagementCompanyRequestMessage message, ManagementCompanyRequestActorKind senderKind, Guid senderUserId, CancellationToken ct)
        => BroadcastAsync(request, new { kind = "message", requestId = request.Id, message = new { message.Id, message.AuthorUserId, message.Content, message.CreatedAt } }, senderKind, senderUserId, ct);

    public Task BroadcastUpdatedAsync(ManagementCompanyRequest request, CancellationToken ct)
        => BroadcastAsync(request, new { kind = "updated", requestId = request.Id, updatedAt = request.UpdatedAt }, null, Guid.Empty, ct);

    private async Task BroadcastAsync(ManagementCompanyRequest request, object payload, ManagementCompanyRequestActorKind? senderKind, Guid senderUserId, CancellationToken ct)
    {
        var recipients = senderKind switch
        {
            ManagementCompanyRequestActorKind.ManagementCompany => await GestaoRecipientsAsync(request.CondominiumId, ct),
            ManagementCompanyRequestActorKind.Management => await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct),
            null => await CombinedRecipientsAsync(request, ct),
            _ => Array.Empty<Guid>()
        };
        foreach (var userId in recipients.Distinct())
        {
            if (userId == senderUserId && senderUserId != Guid.Empty) continue;
            await hub.Clients.User(userId.ToString()).SendAsync("management-company-request-event", payload, ct);
        }
    }

    private async Task<Guid[]> CombinedRecipientsAsync(ManagementCompanyRequest request, CancellationToken ct)
    {
        var gestao = await GestaoRecipientsAsync(request.CondominiumId, ct);
        var admin = await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct);
        return gestao.Concat(admin).Distinct().ToArray();
    }

    private Task<Guid[]> GestaoRecipientsAsync(Guid condominiumId, CancellationToken ct) =>
        db.CondominiumMemberships.AsNoTracking()
            .Where(membership => membership.CondominiumId == condominiumId && membership.IsActive && membership.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(role => (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager) && role.IsActive && role.RevokedAt == null),
                membership => membership.Id, role => role.CondominiumMembershipId, (membership, _) => membership.UserId)
            .Distinct()
            .ToArrayAsync(ct);

    private Task<Guid[]> AdministradoraRecipientsAsync(Guid managementCompanyId, Guid categoryId, CancellationToken ct) =>
        (from responsible in db.ManagementCompanyRequestCategoryResponsibles.AsNoTracking()
         join employee in db.ManagementCompanyEmployees.AsNoTracking() on responsible.ManagementCompanyEmployeeId equals employee.Id
         join user in db.Users.AsNoTracking() on employee.UserId equals user.Id
         where responsible.ManagementCompanyRequestCategoryId == categoryId && employee.ManagementCompanyId == managementCompanyId && employee.IsActive && user.IsActive
         select user.Id).Distinct().ToArrayAsync(ct);
}
