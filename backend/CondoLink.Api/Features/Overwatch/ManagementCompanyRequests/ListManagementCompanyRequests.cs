using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequests;
public static class ListManagementCompanyRequests
{
    public static IEndpointRouteBuilder MapListManagementCompanyRequests(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/management-company-requests", HandleAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(string? search, int page, AppDbContext db, CancellationToken ct)
    {
        page=Math.Max(1,page); var query=db.ManagementCompanyRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query=query.Where(x=>x.FriendlyIdentifier.Contains(search.Trim()));
        var total=await query.CountAsync(ct); var items=await query.OrderByDescending(x=>x.CreatedAt).Skip((page-1)*30).Take(30).Select(x=>new { x.Id,x.FriendlyIdentifier,x.Type,x.Status,x.CondominiumId,CondominiumName=db.Condominiums.Where(c=>c.Id==x.CondominiumId).Select(c=>c.Name).First(),ManagementCompanyName=db.ManagementCompanies.Where(c=>c.Id==x.ManagementCompanyId).Select(c=>c.Name).First(),CreatedByName=db.Users.Where(u=>u.Id==x.CreatedByUserId).Select(u=>u.FullName).First(),x.CreatedAt }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize=30, total, hasMore=page*30<total });
    }
}
