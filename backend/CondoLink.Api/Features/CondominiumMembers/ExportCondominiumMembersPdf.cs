using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class ExportCondominiumMembersPdf
{
    public static IEndpointRouteBuilder MapExportCondominiumMembersPdf(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/members/export.pdf", HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid condominiumId,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var currentUserId)) return Results.Unauthorized();
        var currentUserActive = await db.Set<ApplicationUser>().AsNoTracking()
            .AnyAsync(x => x.Id == currentUserId && x.IsActive, ct);
        if (!currentUserActive) return Results.Forbid();

        var condominium = await db.Condominiums.AsNoTracking()
            .Where(x => x.Id == condominiumId)
            .Select(x => new { x.Name })
            .SingleOrDefaultAsync(ct);
        if (condominium is null) return Results.NotFound();
        var manager = await db.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == currentUserId && x.CondominiumId == condominiumId
                && x.IsActive && x.EndedAt == null)
            .Join(db.CondominiumMembershipRoles.AsNoTracking()
                    .Where(x => (x.Role == CondominiumRole.Manager || x.Role == CondominiumRole.SubManager) && x.IsActive && x.RevokedAt == null),
                x => x.Id, x => x.CondominiumMembershipId, (_, _) => true)
            .AnyAsync(ct);
        if (!manager && !principal.IsInRole(DependencyInjection.PlatformAdminRole))
            return Results.Forbid();

        var residents = await (
            from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join user in db.Set<ApplicationUser>().AsNoTracking()
                on membership.UserId equals user.Id
            where membership.CondominiumId == condominiumId
                && membership.IsActive && membership.EndedAt == null
                && role.Role == CondominiumRole.Resident && role.IsActive && role.RevokedAt == null
            select new
            {
                user.Id, user.FullName, Email = user.Email!, user.PhoneNumber,
                user.MustChangePassword, user.FirstAccessInviteSentAt,
                user.FirstAccessInviteFailedAt
            }).Distinct().ToListAsync(ct);
        var residentIds = residents.Select(x => x.Id).ToArray();
        var allLinks = await (
            from link in db.UnitMemberships.AsNoTracking()
            join unit in db.Units.AsNoTracking() on link.UnitId equals unit.Id
            join block in db.CondominiumBlocks.AsNoTracking()
                on unit.BlockId equals block.Id into blocks
            from block in blocks.DefaultIfEmpty()
            where residentIds.Contains(link.UserId) && unit.CondominiumId == condominiumId
            select new
            {
                link.UserId, link.IsActive, link.EndedAt, Unit = unit.Identifier,
                Block = block == null ? null : block.Identifier,
                link.RelationshipType, link.IsResident, link.IsPrimaryResidence
            }).ToListAsync(ct);
        var usersWithAnyLink = allLinks.Select(x => x.UserId).ToHashSet();
        var activeLinks = allLinks.Where(x => x.IsActive && x.EndedAt == null)
            .ToLookup(x => x.UserId);
        var rows = new List<ResidentReportRow>();
        foreach (var resident in residents)
        {
            var status = FirstAccessStatus(resident.MustChangePassword,
                resident.FirstAccessInviteSentAt, resident.FirstAccessInviteFailedAt);
            var links = activeLinks[resident.Id].ToArray();
            if (links.Length == 0 && usersWithAnyLink.Contains(resident.Id)) continue;
            if (links.Length == 0)
                rows.Add(new(null, "Sem unidade", resident.FullName, resident.Email,
                    resident.PhoneNumber, "Sem vínculo de unidade", false, false, status));
            else
                rows.AddRange(links.Select(link => new ResidentReportRow(
                    link.Block, link.Unit, resident.FullName, resident.Email,
                    resident.PhoneNumber, RelationshipLabel(link.RelationshipType),
                    link.IsResident, link.IsPrimaryResidence, status)));
        }

        var generatedAt = SaoPauloNow();
        var pdf = new ResidentReportPdf().Create(condominium.Name, generatedAt, rows);
        var safeName = SafeFileName(condominium.Name);
        return Results.File(pdf, "application/pdf",
            $"moradores-{safeName}-{generatedAt:yyyy-MM-dd}.pdf");
    }

    private static string FirstAccessStatus(bool mustChange, DateTime? sent, DateTime? failed) =>
        !mustChange ? "Completed"
        : failed.HasValue && (!sent.HasValue || failed > sent) ? "DeliveryFailed"
        : sent.HasValue ? "InviteSent" : "Pending";

    private static string RelationshipLabel(UnitRelationshipType type) => type switch
    {
        UnitRelationshipType.Owner => "Proprietário",
        UnitRelationshipType.Tenant => "Inquilino",
        _ => "Ocupante autorizado"
    };

    internal static string SafeFileName(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(x =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x)
                != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(ascii, "[^a-z0-9]+", "-").Trim('-') is { Length: > 0 } safe
            ? safe : "condominio";
    }

    private static DateTime SaoPauloNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"));
        }
    }
}
