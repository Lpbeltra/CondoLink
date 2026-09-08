using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ServiceProviderEntity = CondoLink.Domain.Entities.ServiceProvider;

namespace CondoLink.Api.Features.ServiceProviders;

public static class ServiceProviderEndpoints
{
    public static IEndpointRouteBuilder MapServiceProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/management/service-providers").RequireAuthorization();
        group.MapGet("", ListAsync); group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", CreateAsync); group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/activate", (Guid id, ClaimsPrincipal p, AppDbContext db, CancellationToken ct) => SetActiveAsync(id, true, p, db, ct));
        group.MapPost("/{id:guid}/deactivate", (Guid id, ClaimsPrincipal p, AppDbContext db, CancellationToken ct) => SetActiveAsync(id, false, p, db, ct));
        return app;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, AppDbContext db, string? search, string? status, string? availability, CancellationToken ct)
    {
        var access = await AccessAsync(principal, db, ct); if (!access.Allowed) return access.Result!;
        var query = db.ServiceProviders.AsNoTracking().Where(p => db.ServiceProviderUserLinks.Any(l => l.ServiceProviderId == p.Id && l.UserId == access.UserId)
            || db.ServiceProviderCondominiumLinks.Any(l => l.ServiceProviderId == p.Id && access.CondominiumIds.Contains(l.CondominiumId)));
        if (status == "active") query = query.Where(x => x.IsActive); else if (status == "inactive") query = query.Where(x => !x.IsActive);
        if (availability == "mine") query = query.Where(x => db.ServiceProviderUserLinks.Any(l => l.ServiceProviderId == x.Id && l.UserId == access.UserId));
        if (availability == "condominium") query = query.Where(x => db.ServiceProviderCondominiumLinks.Any(l => l.ServiceProviderId == x.Id && access.CondominiumIds.Contains(l.CondominiumId)));
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(term) || (x.CompanyName != null && x.CompanyName.ToLower().Contains(term)) || x.Specialty.ToLower().Contains(term) || x.Phone.ToLower().Contains(term) || (x.ContactName != null && x.ContactName.ToLower().Contains(term))); }
        var providers = await query.OrderBy(x => x.Name).ToArrayAsync(ct);
        return Results.Ok(await ShapeAsync(providers, access, db, ct));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await AccessAsync(principal, db, ct); if (!access.Allowed) return access.Result!;
        var provider = await db.ServiceProviders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (provider is null || !await VisibleAsync(id, access, db, ct)) return Results.NotFound(new { error = "Prestador não encontrado." });
        return Results.Ok((await ShapeAsync([provider], access, db, ct)).Single());
    }

    private static async Task<IResult> CreateAsync(Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await AccessAsync(principal, db, ct); if (!access.Allowed) return access.Result!;
        var validation = Validate(request); if (validation is not null) return validation;
        if (!request.IsMine && request.CondominiumIds.Count == 0) return Results.BadRequest(new { error = "Escolha uma disponibilidade." });
        if (request.IsMine && !access.CanManage) return Results.Forbid();
        if (!await CanUseCondominiumsAsync(request.CondominiumIds, access, db, ct)) return Results.Forbid();
        var now = DateTime.UtcNow; var provider = new ServiceProviderEntity(request.Name!, request.CompanyName, request.Specialty!, request.ContactName, request.Phone!, request.Email, request.PixKey, request.PixKeyType, request.Notes, now);
        db.ServiceProviders.Add(provider); AddLinks(provider.Id, request, access.UserId, db); await db.SaveChangesAsync(ct);
        return Results.Created($"/management/service-providers/{provider.Id}", (await ShapeAsync([provider], access, db, ct)).Single());
    }

    private static async Task<IResult> UpdateAsync(Guid id, Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await AccessAsync(principal, db, ct); if (!access.Allowed) return access.Result!;
        var provider = await db.ServiceProviders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (provider is null || !await VisibleAsync(id, access, db, ct)) return Results.NotFound(new { error = "Prestador não encontrado." });
        var validation = Validate(request); if (validation is not null) return validation;
        if (!request.IsMine && request.CondominiumIds.Count == 0) return Results.BadRequest(new { error = "Escolha uma disponibilidade." });
        if (!await CanUseCondominiumsAsync(request.CondominiumIds, access, db, ct)) return Results.Forbid();
        provider.Update(request.Name!, request.CompanyName, request.Specialty!, request.ContactName, request.Phone!, request.Email, request.PixKey, request.PixKeyType, request.Notes, DateTime.UtcNow);
        provider.SetStatus(request.IsActive, DateTime.UtcNow);
        var oldMine = await db.ServiceProviderUserLinks.Where(x => x.ServiceProviderId == id && x.UserId == access.UserId).ToListAsync(ct); db.ServiceProviderUserLinks.RemoveRange(oldMine);
        var oldCondo = await db.ServiceProviderCondominiumLinks.Where(x => x.ServiceProviderId == id && access.CondominiumIds.Contains(x.CondominiumId)).ToListAsync(ct); db.ServiceProviderCondominiumLinks.RemoveRange(oldCondo);
        AddLinks(id, request, access.UserId, db); await db.SaveChangesAsync(ct);
        return Results.Ok((await ShapeAsync([provider], access, db, ct)).Single());
    }

    private static async Task<IResult> SetActiveAsync(Guid id, bool active, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await AccessAsync(principal, db, ct); if (!access.Allowed) return access.Result!;
        var provider = await db.ServiceProviders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (provider is null || !await VisibleAsync(id, access, db, ct)) return Results.NotFound(new { error = "Prestador não encontrado." });
        provider.SetStatus(active, DateTime.UtcNow); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static void AddLinks(Guid id, Request request, Guid userId, AppDbContext db)
    { if (request.IsMine) db.ServiceProviderUserLinks.Add(new(id, userId)); foreach (var condo in request.CondominiumIds.Distinct()) db.ServiceProviderCondominiumLinks.Add(new(id, condo)); }

    private static IResult? Validate(Request r)
    { if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Specialty) || string.IsNullOrWhiteSpace(r.Phone)) return Results.BadRequest(new { error = "Nome, especialidade e telefone são obrigatórios." }); if (r.Email is not null && !MailAddress.TryCreate(r.Email, out _)) return Results.BadRequest(new { error = "E-mail inválido." }); return null; }

    private static async Task<bool> CanUseCondominiumsAsync(IReadOnlyList<Guid> ids, Access access, AppDbContext db, CancellationToken ct)
    { foreach (var id in ids.Distinct()) if (!access.CondominiumIds.Contains(id) || !await SubManagerAccess.HasAsync(db, access.UserId, id, SubManagerModule.Management, ct)) return false; return true; }
    private static async Task<bool> VisibleAsync(Guid id, Access access, AppDbContext db, CancellationToken ct) => await db.ServiceProviderUserLinks.AnyAsync(x => x.ServiceProviderId == id && x.UserId == access.UserId, ct) || await db.ServiceProviderCondominiumLinks.AnyAsync(x => x.ServiceProviderId == id && access.CondominiumIds.Contains(x.CondominiumId), ct);

    private static async Task<object[]> ShapeAsync(ServiceProviderEntity[] providers, Access access, AppDbContext db, CancellationToken ct)
    { var ids = providers.Select(x => x.Id).ToArray(); var condos = await (from l in db.ServiceProviderCondominiumLinks.AsNoTracking() join c in db.Condominiums.AsNoTracking() on l.CondominiumId equals c.Id where ids.Contains(l.ServiceProviderId) && access.CondominiumIds.Contains(l.CondominiumId) select new { l.ServiceProviderId, l.CondominiumId, c.Name }).ToArrayAsync(ct); var mine = await db.ServiceProviderUserLinks.AsNoTracking().Where(x => ids.Contains(x.ServiceProviderId) && x.UserId == access.UserId).Select(x => x.ServiceProviderId).ToArrayAsync(ct); return providers.Select(p => (object)new { p.Id, p.Name, p.CompanyName, p.Specialty, p.ContactName, p.Phone, p.Email, p.PixKey, PixKeyType = p.PixKeyType?.ToString(), p.Notes, p.IsActive, p.CreatedAt, p.UpdatedAt, IsMine = mine.Contains(p.Id), Condominiums = condos.Where(x => x.ServiceProviderId == p.Id).Select(x => new { x.CondominiumId, x.Name }).ToArray() }).ToArray(); }

    private static async Task<Access> AccessAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    { var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (!Guid.TryParse(value, out var userId)) return Access.Denied(Results.Unauthorized()); var memberships = await (from m in db.CondominiumMemberships.AsNoTracking() join r in db.CondominiumMembershipRoles.AsNoTracking() on m.Id equals r.CondominiumMembershipId where m.UserId == userId && m.IsActive && m.EndedAt == null && r.IsActive && r.RevokedAt == null && (r.Role == CondominiumRole.Manager || r.Role == CondominiumRole.SubManager) select new { m.CondominiumId, r.Role }).Distinct().ToArrayAsync(ct); var condoIds = memberships.Select(x => x.CondominiumId).Distinct().ToArray(); var manager = memberships.Any(x => x.Role == CondominiumRole.Manager); if (!manager) { var allowed = new List<Guid>(); foreach (var id in condoIds) if (await SubManagerAccess.HasAsync(db, userId, id, SubManagerModule.Management, ct)) allowed.Add(id); condoIds = allowed.ToArray(); } return new Access(userId, condoIds, manager || condoIds.Length > 0, null); }

    private sealed record Access(Guid UserId, Guid[] CondominiumIds, bool CanManage, IResult? Result) { public bool Allowed => Result is null; public static Access Denied(IResult result) => new(Guid.Empty, [], false, result); }
    public sealed record Request(string? Name, string? CompanyName, string? Specialty, string? ContactName, string? Phone, string? Email, string? PixKey, ServiceProviderPixKeyType? PixKeyType, string? Notes, bool IsMine, IReadOnlyList<Guid> CondominiumIds, bool IsActive = true);
}
