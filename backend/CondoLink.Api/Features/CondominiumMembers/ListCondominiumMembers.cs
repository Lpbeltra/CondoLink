using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class ListCondominiumMembers
{
    public static IEndpointRouteBuilder MapListCondominiumMembers(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/condominiums/{condominiumId:guid}/members",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        string? search,
        string? status,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(authenticatedUserIdValue, out var authenticatedUserId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == authenticatedUserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var condominiumExists = await dbContext.Condominiums
            .AsNoTracking()
            .AnyAsync(
                condominium => condominium.Id == condominiumId,
                cancellationToken);

        if (!condominiumExists)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == condominiumId
                && membership.IsActive
                && membership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        if (!isCondominiumManager
            && !principal.IsInRole(
                DependencyInjection.PlatformAdminRole))
        {
            return Results.Json(
                new { error = "Only condominium managers can view members." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var activeRoles = dbContext.CondominiumMembershipRoles
            .AsNoTracking()
            .Where(role => role.IsActive && role.RevokedAt == null);

        var rows = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join user in dbContext.Set<ApplicationUser>().AsNoTracking()
                    on membership.UserId equals user.Id
                join role in activeRoles
                    on membership.Id equals role.CondominiumMembershipId into roles
                from role in roles.DefaultIfEmpty()
                where membership.CondominiumId == condominiumId
                orderby user.FullName
                select new
                {
                    MembershipId = membership.Id,
                    membership.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.Cpf,
                    user.Cnpj,
                    user.Address,
                    user.City,
                    user.State,
                    UserActive = user.IsActive,
                    user.MustChangePassword,
                    user.EmailDeliveryEnabled,
                    user.FirstAccessInviteSentAt,
                    user.FirstAccessInviteFailedAt,
                    user.LastLoginAt,
                    MembershipActive = membership.IsActive,
                    membership.JoinedAt,
                    membership.EndedAt,
                    Role = role == null ? (CondominiumRole?)null : role.Role
                })
            .ToListAsync(cancellationToken);

        var unitLinks = await (
                from link in dbContext.UnitMemberships.AsNoTracking()
                join unit in dbContext.Units.AsNoTracking()
                    on link.UnitId equals unit.Id
                join block in dbContext.CondominiumBlocks.AsNoTracking()
                    on unit.BlockId equals block.Id into blocks
                from block in blocks.DefaultIfEmpty()
                where unit.CondominiumId == condominiumId
                select new
                {
                    link.UserId,
                    Link = new UnitLinkResponse(
                        link.Id,
                        unit.Id,
                        unit.Identifier,
                        block == null ? null : block.Identifier,
                        link.RelationshipType.ToString(),
                        link.IsResident,
                        link.IsPrimaryResidence,
                        link.IsActive,
                        link.EndedAt)
                })
            .ToListAsync(cancellationToken);
        var linksByUser = unitLinks
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UnitLinkResponse>)group
                    .Select(item => item.Link)
                    .OrderBy(item => item.Block)
                    .ThenBy(item => item.UnitIdentifier)
                    .ToArray());

        var response = rows
            .GroupBy(row => new
            {
                row.MembershipId,
                row.UserId,
                row.FullName,
                row.Email,
                row.PhoneNumber,
                row.Cpf,
                row.Cnpj,
                row.Address,
                row.City,
                row.State,
                row.UserActive,
                row.MustChangePassword,
                row.EmailDeliveryEnabled,
                row.FirstAccessInviteSentAt,
                row.FirstAccessInviteFailedAt,
                row.LastLoginAt,
                row.MembershipActive,
                row.JoinedAt,
                row.EndedAt
            })
            .Select(group =>
            {
                var links = linksByUser.GetValueOrDefault(group.Key.UserId, []);
                var isResidentActive = links.Count == 0
                    ? group.Key.MembershipActive
                    : links.Any(link => link.IsActive);
                return new Response(
                group.Key.MembershipId,
                group.Key.UserId,
                group.Key.FullName,
                group.Key.Email!,
                group.Key.PhoneNumber,
                group.Key.Cpf,
                group.Key.Cnpj,
                group.Key.Address,
                group.Key.City,
                group.Key.State,
                group.Key.UserActive,
                group.Key.MustChangePassword,
                group.Key.EmailDeliveryEnabled,
                group.Key.MustChangePassword
                    ? group.Key.FirstAccessInviteFailedAt.HasValue
                        && (!group.Key.FirstAccessInviteSentAt.HasValue
                            || group.Key.FirstAccessInviteFailedAt > group.Key.FirstAccessInviteSentAt)
                            ? "DeliveryFailed"
                            : group.Key.FirstAccessInviteSentAt.HasValue ? "InviteSent" : "Pending"
                    : "Completed",
                group.Key.LastLoginAt,
                group.Key.MembershipActive,
                group.Key.JoinedAt,
                group.Key.EndedAt,
                isResidentActive,
                group
                    .Where(row => row.Role.HasValue)
                    .OrderBy(row => row.Role)
                    .Select(row => row.Role!.Value.ToString())
                    .ToArray(),
                links,
                false,
                "A elegibilidade é revalidada ao excluir.");
            })
            .OrderBy(member => member.FullName)
            .ToArray();

        var normalizedSearch = NormalizeSearch(search);
        var requestedActive = status?.ToLowerInvariant() switch
        {
            "active" => true,
            "inactive" => false,
            _ => (bool?)null
        };
        response = response.Where(member =>
                (!requestedActive.HasValue || requestedActive.Value == member.IsResidentActive)
                && (normalizedSearch is null || SearchText(member).Contains(normalizedSearch, StringComparison.Ordinal)))
            .ToArray();

        var eligibleIds = await FindDeleteEligibleUserIdsAsync(
            condominiumId, response.Select(x => x.UserId).ToArray(), dbContext, cancellationToken);
        response = response.Select(member => member with
        {
            CanDelete = eligibleIds.Contains(member.UserId),
            DeleteBlockedReason = eligibleIds.Contains(member.UserId)
                ? null
                : "Este morador possui histórico ou outros vínculos e não pode ser excluído. Você pode inativá-lo."
        }).ToArray();

        return Results.Ok(response);
    }

    public sealed record Response(
        Guid MembershipId,
        Guid UserId,
        string FullName,
        string Email,
        string? PhoneNumber,
        string? Cpf,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        bool UserActive,
        bool MustChangePassword,
        bool EmailDeliveryEnabled,
        string FirstAccessStatus,
        DateTime? LastLoginAt,
        bool MembershipActive,
        DateTime JoinedAt,
        DateTime? EndedAt,
        bool IsResidentActive,
        IReadOnlyList<string> Roles,
        IReadOnlyList<UnitLinkResponse> UnitLinks,
        bool CanDelete,
        string? DeleteBlockedReason);

    public sealed record UnitLinkResponse(
        Guid UnitMembershipId,
        Guid UnitId,
        string UnitIdentifier,
        string? Block,
        string RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence,
        bool IsActive,
        DateTime? EndedAt);

    private static string? NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
            && !char.IsPunctuation(character) && !char.IsWhiteSpace(character)).ToArray());
    }

    private static string SearchText(Response member) => NormalizeSearch(
        $"{member.FullName} {member.Email} {member.PhoneNumber} "
        + string.Join(' ', member.UnitLinks.Select(x => $"{x.Block} {x.UnitIdentifier}"))) ?? "";

    private static async Task<HashSet<Guid>> FindDeleteEligibleUserIdsAsync(Guid condominiumId, Guid[] userIds,
        AppDbContext db, CancellationToken ct)
    {
        if (userIds.Length == 0) return [];
        var blocked = new HashSet<Guid>();
        void Add(IEnumerable<Guid> ids) { foreach (var id in ids) blocked.Add(id); }
        Add(await db.Requests.Where(x => userIds.Contains(x.AuthorUserId)).Select(x => x.AuthorUserId).Distinct().ToListAsync(ct));
        Add(await db.RequestMessages.Where(x => userIds.Contains(x.AuthorUserId)).Select(x => x.AuthorUserId).Distinct().ToListAsync(ct));
        Add(await db.RequestStatusHistories.Where(x => userIds.Contains(x.ChangedByUserId)).Select(x => x.ChangedByUserId).Distinct().ToListAsync(ct));
        Add(await db.RequestAttachments.Where(x => userIds.Contains(x.UploadedByUserId)).Select(x => x.UploadedByUserId).Distinct().ToListAsync(ct));
        Add(await db.RequestResidentReplyRequirements.Where(x => userIds.Contains(x.RequestedByUserId)).Select(x => x.RequestedByUserId).Distinct().ToListAsync(ct));
        Add(await db.Notifications.Where(x => userIds.Contains(x.RecipientUserId)).Select(x => x.RecipientUserId).Distinct().ToListAsync(ct));
        Add(await db.UnitMemberships.Where(x => userIds.Contains(x.UserId)).GroupBy(x => x.UserId).Where(x => x.Count() > 1).Select(x => x.Key).ToListAsync(ct));
        Add(await db.CondominiumMemberships.Where(x => userIds.Contains(x.UserId) && x.CondominiumId != condominiumId).Select(x => x.UserId).Distinct().ToListAsync(ct));
        Add(await db.CondominiumMemberships.Where(x => userIds.Contains(x.UserId)).GroupBy(x => x.UserId).Where(x => x.Count() > 1).Select(x => x.Key).ToListAsync(ct));
        Add(await (from membership in db.CondominiumMemberships
                   join role in db.CondominiumMembershipRoles on membership.Id equals role.CondominiumMembershipId
                   where userIds.Contains(membership.UserId) && role.Role == CondominiumRole.Manager
                   select membership.UserId).Distinct().ToListAsync(ct));
        Add(await db.ManagementCompanyEmployees.Where(x => userIds.Contains(x.UserId)).Select(x => x.UserId).Distinct().ToListAsync(ct));
        Add(await db.WhatsAppOutboundMessages.Where(x => userIds.Contains(x.UserId)).Select(x => x.UserId).Distinct().ToListAsync(ct));
        Add(await db.WhatsAppPhoneVerifications.Where(x => userIds.Contains(x.UserId)).Select(x => x.UserId).Distinct().ToListAsync(ct));
        Add(await db.WhatsAppInboundMessages.Where(x => x.IdentifiedUserId.HasValue && userIds.Contains(x.IdentifiedUserId.Value)).Select(x => x.IdentifiedUserId.Value).Distinct().ToListAsync(ct));
        Add(await db.WhatsAppSessions.Where(x => x.UserId.HasValue && userIds.Contains(x.UserId.Value)).Select(x => x.UserId.Value).Distinct().ToListAsync(ct));
        Add(await db.CondominiumDocuments.Where(x => userIds.Contains(x.UploadedByUserId)).Select(x => x.UploadedByUserId).Distinct().ToListAsync(ct));
        Add(await db.CondominiumAssistantConversations.Where(x => userIds.Contains(x.CreatedByUserId)).Select(x => x.CreatedByUserId).Distinct().ToListAsync(ct));
        return userIds.Where(id => !blocked.Contains(id)).ToHashSet();
    }
}
