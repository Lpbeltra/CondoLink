using CondoLink.Api.Features.Management;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.TelegramAssistant;

public static class TelegramAssistantAccess
{
    public static async Task<(Guid Id, string Name)[]> CondominiumsAsync(
        AppDbContext db, Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, ct)) return [];
        var candidates = await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join condominium in db.Condominiums.AsNoTracking()
                on membership.CondominiumId equals condominium.Id
            where membership.UserId == userId && membership.IsActive && membership.EndedAt == null
                && role.IsActive && role.RevokedAt == null
                && (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
            select new { condominium.Id, condominium.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        var allowed = new List<(Guid, string)>();
        foreach (var item in candidates)
            if (await SubManagerAccess.HasAsync(db, userId, item.Id, SubManagerModule.Assistant, ct))
                allowed.Add((item.Id, item.Name));
        return [.. allowed];
    }
}
