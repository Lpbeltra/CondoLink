using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class ManagementCompanyModuleEndpoints
{
    public static IEndpointRouteBuilder MapManagementCompanyModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/management-companies/{managementCompanyId:guid}/modules", ListAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapPut("/overwatch/management-companies/{managementCompanyId:guid}/modules/employee-management", UpdateAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }
    private static async Task<IResult> ListAsync(Guid managementCompanyId, AppDbContext db, CancellationToken ct)
    {
        if (!await db.ManagementCompanies.AsNoTracking().AnyAsync(x => x.Id == managementCompanyId, ct)) return Results.NotFound();
        var row = await db.ManagementCompanyModules.AsNoTracking().SingleOrDefaultAsync(x => x.ManagementCompanyId == managementCompanyId && x.Module == ManagementCompanyModuleType.EmployeeManagement, ct);
        return Results.Ok(new[] { new { module = "EmployeeManagement", isEnabled = row?.IsEnabled ?? false } });
    }
    private static async Task<IResult> UpdateAsync(Guid managementCompanyId, UpdateRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.ManagementCompanies.AnyAsync(x => x.Id == managementCompanyId, ct)) return Results.NotFound();
        var row = await db.ManagementCompanyModules.SingleOrDefaultAsync(x => x.ManagementCompanyId == managementCompanyId && x.Module == ManagementCompanyModuleType.EmployeeManagement, ct);
        if (row is null) db.ManagementCompanyModules.Add(new ManagementCompanyModule(managementCompanyId, ManagementCompanyModuleType.EmployeeManagement, request.Enabled, DateTime.UtcNow)); else row.SetEnabled(request.Enabled, DateTime.UtcNow);
        await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    public sealed record UpdateRequest(bool Enabled);
}
