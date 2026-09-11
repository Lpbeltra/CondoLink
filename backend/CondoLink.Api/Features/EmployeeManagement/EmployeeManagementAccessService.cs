using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeManagement;

public enum EmployeeManagementActorKind { Management, ManagementCompany }

public sealed record EmployeeManagementActor(Guid UserId, string FullName, EmployeeManagementActorKind Kind);

/// <summary>
/// Single authorization gate for the Employee Management module. Reused by every
/// Employee Management endpoint (and, later, payslip/document features built on
/// top of it) so the entitlement/delegation/permission rules live in one place.
///
/// A caller is authorized for a condominium's Employee Management module when
/// EITHER:
///  - they hold an active Manager role for the condominium (always allowed once
///    the module is enabled), or an active SubManager role explicitly granted the
///    <see cref="SubManagerModule.EmployeeManagement"/> permission; OR
///  - they are an active employee/department of the management company CURRENTLY
///    linked to the condominium, that condominium has delegated the module to its
///    management company, and that employee/department was explicitly granted the
///    <see cref="CondominiumModuleType.EmployeeManagement"/> permission.
///
/// Neither path requires an active Manager to exist on the condominium.
/// </summary>
public sealed class EmployeeManagementAccessService(AppDbContext db, ICondominiumModuleService modules)
{
    public async Task<EmployeeManagementActor> RequireAsync(ClaimsPrincipal principal, Guid condominiumId, CancellationToken ct)
    {
        var user = await RequireActiveUserAsync(principal, ct);

        if (!await modules.IsEnabledAsync(condominiumId, CondominiumModuleType.EmployeeManagement, ct))
            throw new ForbiddenAppException("A Gestão de Funcionários não está habilitada para este condomínio.");

        if (await SubManagerAccess.HasAsync(db, user.Id, condominiumId, SubManagerModule.EmployeeManagement, ct))
            return new(user.Id, user.FullName, EmployeeManagementActorKind.Management);

        var employee = await db.ManagementCompanyEmployees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, ct);
        if (employee is not null)
        {
            var canDelegate = await modules.CanManagementCompanyAccessAsync(
                condominiumId, employee.ManagementCompanyId, CondominiumModuleType.EmployeeManagement, ct);
            var hasPermission = canDelegate && await db.ManagementCompanyEmployeeModulePermissions.AsNoTracking()
                .AnyAsync(x => x.ManagementCompanyEmployeeId == employee.Id
                    && x.Module == CondominiumModuleType.EmployeeManagement
                    && x.IsAllowed && x.RevokedAt == null, ct);
            if (hasPermission) return new(user.Id, user.FullName, EmployeeManagementActorKind.ManagementCompany);
        }

        throw new ForbiddenAppException("Você não possui acesso à Gestão de Funcionários deste condomínio.");
    }

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
