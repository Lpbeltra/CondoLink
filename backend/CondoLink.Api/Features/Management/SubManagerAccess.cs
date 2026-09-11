using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Management;

public static class SubManagerAccess
{
    public static IReadOnlyList<SubManagerModule> ConfigurableModules { get; } =
        [SubManagerModule.Attendance, SubManagerModule.ManagementCompany,
         SubManagerModule.Agenda, SubManagerModule.Assistant,
         SubManagerModule.Documents, SubManagerModule.Management,
         SubManagerModule.EmployeeManagement];

    // Every configurable module defaults to allowed once backfilled, except EmployeeManagement:
    // a SubManager must be granted that one explicitly.
    private static bool DefaultAllowed(SubManagerModule module) => module != SubManagerModule.EmployeeManagement;

    public static IQueryable<Guid> ActiveMemberships(AppDbContext db, Guid userId, Guid condominiumId) =>
        from membership in db.CondominiumMemberships.AsNoTracking()
        join role in db.CondominiumMembershipRoles.AsNoTracking() on membership.Id equals role.CondominiumMembershipId
        where membership.UserId == userId && membership.CondominiumId == condominiumId && membership.IsActive && membership.EndedAt == null
            && (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager) && role.IsActive && role.RevokedAt == null
        select membership.Id;

    public static Task<bool> HasAsync(AppDbContext db, Guid userId, Guid condominiumId, SubManagerModule module, CancellationToken ct) =>
        (from membershipId in ActiveMemberships(db, userId, condominiumId)
         join membership in db.CondominiumMemberships.AsNoTracking() on membershipId equals membership.Id
         join role in db.CondominiumMembershipRoles.AsNoTracking() on membership.Id equals role.CondominiumMembershipId
         where role.IsActive && role.RevokedAt == null && (role.Role == CondominiumRole.Manager
            || db.SubManagerModulePermissions.Any(p => p.CondominiumMembershipId == membershipId && p.Module == module && p.IsAllowed && p.RevokedAt == null)
            // Legacy fallback: a SubManager with no permission rows at all (never backfilled)
            // keeps full access to pre-existing modules. EmployeeManagement postdates that
            // legacy behavior and must never be granted by absence of rows — only by an
            // explicit allow row, so it is excluded from this fallback.
            || (module != SubManagerModule.EmployeeManagement
                && !db.SubManagerModulePermissions.Any(p => p.CondominiumMembershipId == membershipId)))
         select membershipId).AnyAsync(ct);

    public static async Task EnsureDefaultsAsync(AppDbContext db, Guid membershipId, Guid actorUserId, CancellationToken ct)
    {
        var existing = await db.SubManagerModulePermissions.Where(x => x.CondominiumMembershipId == membershipId).ToListAsync(ct);
        foreach (var module in ConfigurableModules)
            if (existing.All(x => x.Module != module)) db.SubManagerModulePermissions.Add(new(membershipId, module, actorUserId, DefaultAllowed(module)));
    }
}
