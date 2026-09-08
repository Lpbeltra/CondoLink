using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Management;
using ServiceProviderEntity = CondoLink.Domain.Entities.ServiceProvider;
using ServiceProvider = CondoLink.Domain.Entities.ServiceProvider;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class RequestServiceProviderEndpoints
{
    public static IEndpointRouteBuilder MapRequestServiceProviderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/service-providers", OptionsAsync).RequireAuthorization();
        app.MapPatch("/requests/{requestId:guid}/service-provider", LinkAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> OptionsAsync(Guid requestId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await RequestAccessAsync(requestId, principal, db, ct); if (access is null) return Results.NotFound(new { error="Request not found." }); if (!access.Value.Value) return Results.Forbid();
        var userId = UserId(principal); var query = db.ServiceProviders.AsNoTracking().Where(p => p.IsActive && (db.ServiceProviderUserLinks.Any(l=>l.ServiceProviderId==p.Id && l.UserId==userId) || db.ServiceProviderCondominiumLinks.Any(l=>l.ServiceProviderId==p.Id && l.CondominiumId==access.Value.CondominiumId)));
        var rows = await query.OrderBy(p=>p.Name).Select(p=>new { p.Id,p.Name,p.CompanyName,p.Specialty, p.Phone, IsMine=db.ServiceProviderUserLinks.Any(l=>l.ServiceProviderId==p.Id&&l.UserId==userId), IsCondominium=db.ServiceProviderCondominiumLinks.Any(l=>l.ServiceProviderId==p.Id&&l.CondominiumId==access.Value.CondominiumId) }).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var specialties = await db.ServiceProviderSpecialties.AsNoTracking().Where(x => ids.Contains(x.ServiceProviderId)).OrderBy(x => x.Name).ToArrayAsync(ct);
        return Results.Ok(rows.Select(x => new { x.Id, x.Name, x.CompanyName, x.Specialty, Specialties = specialties.Where(s => s.ServiceProviderId == x.Id).Select(s => s.Name).ToArray(), x.Phone, x.IsMine, x.IsCondominium }));
    }

    private static async Task<IResult> LinkAsync(Guid requestId, LinkRequest input, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await RequestAccessAsync(requestId, principal, db, ct); if (access is null) return Results.NotFound(new { error="Request not found." }); if (!access.Value.Value) return Results.Forbid();
        var request = await db.Requests.SingleAsync(x=>x.Id==requestId,ct); ServiceProvider? next = null; if (input.ServiceProviderId is Guid providerId) { next=await db.ServiceProviders.SingleOrDefaultAsync(x=>x.Id==providerId&&x.IsActive,ct); if(next is null)return Results.NotFound(new{error="Prestador não encontrado."}); var userId=UserId(principal); var allowed=await db.ServiceProviderUserLinks.AnyAsync(x=>x.ServiceProviderId==providerId&&x.UserId==userId,ct)||await db.ServiceProviderCondominiumLinks.AnyAsync(x=>x.ServiceProviderId==providerId&&x.CondominiumId==request.CondominiumId,ct); if(!allowed)return Results.Forbid(); }
        var previous=request.ServiceProviderId is Guid oldId?await db.ServiceProviders.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==oldId,ct):null; var now=DateTime.UtcNow; var eventType=previous is null?(next is null?null:"Linked"):(next is null?"Removed":"Changed"); if(eventType is null)return Results.NoContent(); request.SetServiceProvider(next?.Id,now); db.RequestServiceProviderHistories.Add(new(requestId,eventType,previous?.Name,previous?.Specialty,next?.Name,next?.Specialty,UserId(principal),now)); await db.SaveChangesAsync(ct); return Results.Ok(new{serviceProviderId=next?.Id});
    }

    private static async Task<(bool Value, Guid CondominiumId)?> RequestAccessAsync(Guid requestId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    { var request=await db.Requests.AsNoTracking().Where(x=>x.Id==requestId).Select(x=>new{x.Id,x.CondominiumId}).SingleOrDefaultAsync(ct); if(request is null)return null; var uid=UserId(principal); var allowed=await db.CondominiumMemberships.AsNoTracking().Where(m=>m.UserId==uid&&m.CondominiumId==request.CondominiumId&&m.IsActive&&m.EndedAt==null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(r=>r.IsActive&&r.RevokedAt==null&&(r.Role==CondominiumRole.Manager||r.Role==CondominiumRole.SubManager)),m=>m.Id,r=>r.CondominiumMembershipId,(m,r)=>new{m.Id,r.Role}).AnyAsync(ct); if(!allowed)return(false,request.CondominiumId); var sub=await db.CondominiumMemberships.AsNoTracking().Where(m=>m.UserId==uid&&m.CondominiumId==request.CondominiumId&&m.IsActive&&m.EndedAt==null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(r=>r.Role==CondominiumRole.SubManager&&r.IsActive&&r.RevokedAt==null),m=>m.Id,r=>r.CondominiumMembershipId,(m,r)=>m.Id).SingleOrDefaultAsync(ct); if(sub!=Guid.Empty&& !await SubManagerAccess.HasAsync(db,uid,request.CondominiumId,SubManagerModule.Attendance,ct))return(false,request.CondominiumId); return(true,request.CondominiumId); }
    private static Guid UserId(ClaimsPrincipal p)=>Guid.Parse(p.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??p.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    public sealed record LinkRequest(Guid? ServiceProviderId);
}
