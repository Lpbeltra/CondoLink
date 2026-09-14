using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public enum EmployeeManagementActorKind { ManagementCompany }
public sealed record EmployeeManagementActor(Guid UserId, string FullName, EmployeeManagementActorKind Kind);

public sealed class EmployeeManagementAccessService(AppDbContext db)
{
    public async Task<EmployeeManagementActor> RequireAsync(ClaimsPrincipal principal, Guid condominiumId, CancellationToken ct)
    {
        var scope = await RequireAdministratorAsync(principal, ct);
        if (!await db.Condominiums.AsNoTracking().AnyAsync(x => x.Id == condominiumId && x.ManagementCompanyId == scope.ManagementCompanyId && x.IsActive, ct))
            throw new ForbiddenAppException("Condomínio fora da administradora atual.");
        return new(scope.UserId, scope.FullName, EmployeeManagementActorKind.ManagementCompany);
    }

    public async Task<(EmployeeManagementActor Actor, EmployeeDocumentBatch Batch)> RequireBatchAsync(
        ClaimsPrincipal principal, Guid batchId, CancellationToken ct)
    {
        var scope = await RequireAdministratorAsync(principal, ct);
        var batch = await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x => x.Id == batchId
            && x.ManagementCompanyId == scope.ManagementCompanyId, ct);
        if (batch is null)
            throw new ForbiddenAppException("Lote fora da administradora atual.");
        return (new(scope.UserId, scope.FullName, EmployeeManagementActorKind.ManagementCompany), batch);
    }

    public async Task<(Guid UserId, Guid ManagementCompanyId, string FullName)> RequireAdministratorAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var user = await RequireActiveUserAsync(principal, ct);
        var employee = await db.ManagementCompanyEmployees.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, ct);
        if (employee is null || !await HasGlobalAccessAsync(employee.Id, employee.ManagementCompanyId, ct))
            throw new ForbiddenAppException("Você não possui acesso à Gestão de Funcionários.");
        return (user.Id, employee.ManagementCompanyId, user.FullName);
    }

    public async Task<bool> HasGlobalAccessAsync(Guid employeeId, Guid managementCompanyId, CancellationToken ct) =>
        await db.ManagementCompanyModules.AsNoTracking().AnyAsync(x => x.ManagementCompanyId == managementCompanyId && x.Module == ManagementCompanyModuleType.EmployeeManagement && x.IsEnabled, ct)
        && await db.ManagementCompanyEmployeeModuleGrants.AsNoTracking().AnyAsync(x =>
            x.ManagementCompanyEmployeeId == employeeId
            && x.Module == ManagementCompanyModuleType.EmployeeManagement
            && x.IsAllowed && x.RevokedAt == null, ct);

    private async Task<ApplicationUser> RequireActiveUserAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var id)) throw new UnauthorizedAppException("Usuário autenticado inválido.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) throw new UnauthorizedAppException("Usuário autenticado não encontrado.");
        if (!user.IsActive) throw new ForbiddenAppException("Usuário inativo.");
        return user;
    }
}
