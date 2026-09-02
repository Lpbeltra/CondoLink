using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public enum ManagementCompanyRequestActorKind { Management, ManagementCompany }
public sealed record ManagementCompanyRequestActor(Guid UserId,string FullName,ManagementCompanyRequestActorKind Kind);

public sealed class ManagementCompanyRequestAccessService(AppDbContext db)
{
    public async Task<Guid> RequireUserIdAsync(ClaimsPrincipal principal,CancellationToken ct)=>(await RequireActiveUserAsync(principal,ct)).Id;
    public async Task<ManagementCompanyRequestActor> RequireForRequestAsync(ClaimsPrincipal principal, Guid requestId, CancellationToken ct)
    {
        var request = await db.ManagementCompanyRequests.AsNoTracking().Where(x=>x.Id==requestId)
            .Select(x=>new{x.CondominiumId,x.ManagementCompanyId,x.CategoryId}).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundAppException("Solicitação não encontrada.");
        return await RequireAsync(principal,request.CondominiumId,request.ManagementCompanyId,request.CategoryId,ct);
    }
    public async Task<ManagementCompanyRequestActor> RequireManagementAsync(ClaimsPrincipal principal,Guid condominiumId,CancellationToken ct)
    {
        var user=await RequireActiveUserAsync(principal,ct);
        if(!await HasManagementScopeAsync(user.Id,condominiumId,ct))throw new ForbiddenAppException("Você não possui acesso de gestão a este condomínio.");
        return new(user.Id,user.FullName,ManagementCompanyRequestActorKind.Management);
    }
    public async Task<ManagementCompanyRequestActor> RequireAsync(ClaimsPrincipal principal,Guid condominiumId,Guid companyId,Guid categoryId,CancellationToken ct)
    {
        var user=await RequireActiveUserAsync(principal,ct);
        if(await HasManagementScopeAsync(user.Id,condominiumId,ct))return new(user.Id,user.FullName,ManagementCompanyRequestActorKind.Management);
        var companyAccess=await db.ManagementCompanyEmployees.AsNoTracking().AnyAsync(a=>a.UserId==user.Id&&a.IsActive&&a.ManagementCompanyId==companyId
            &&db.ManagementCompanyRequestCategoryResponsibles.Any(r=>r.ManagementCompanyEmployeeId==a.Id&&r.ManagementCompanyRequestCategoryId==categoryId),ct);
        if(!companyAccess)throw new ForbiddenAppException("Você não possui acesso a esta solicitação.");
        return new(user.Id,user.FullName,ManagementCompanyRequestActorKind.ManagementCompany);
    }
    public Task<bool> HasManagementScopeAsync(Guid userId,Guid condominiumId,CancellationToken ct)=>
        db.CondominiumMemberships.AsNoTracking().Where(m=>m.UserId==userId&&m.CondominiumId==condominiumId&&m.IsActive&&m.EndedAt==null)
        .Join(db.CondominiumMembershipRoles.AsNoTracking().Where(r=>(r.Role==CondominiumRole.Manager || (r.Role==CondominiumRole.SubManager && (!db.SubManagerModulePermissions.Any(p=>p.CondominiumMembershipId==r.CondominiumMembershipId) || db.SubManagerModulePermissions.Any(p=>p.CondominiumMembershipId==r.CondominiumMembershipId && p.Module==SubManagerModule.ManagementCompany && p.IsAllowed && p.RevokedAt==null))))&&r.IsActive&&r.RevokedAt==null),m=>m.Id,r=>r.CondominiumMembershipId,(_,_)=>true).AnyAsync(ct);
    private async Task<ApplicationUser> RequireActiveUserAsync(ClaimsPrincipal principal,CancellationToken ct)
    {
        var value=principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if(!Guid.TryParse(value,out var id))throw new UnauthorizedAppException("Usuário autenticado inválido.");
        var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(user is null)throw new UnauthorizedAppException("Usuário autenticado não encontrado.");
        if(!user.IsActive)throw new ForbiddenAppException("Usuário inativo.");
        return user;
    }
}
