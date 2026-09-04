using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api;

/// <summary>Small, deterministic local-only dataset. Never runs without DevelopmentSeed:Enabled.</summary>
public static class DevelopmentSeedInitializer
{
    public static async Task InitializeDevelopmentSeedAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()
            || !app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var company = await db.ManagementCompanies.SingleOrDefaultAsync(x => x.Name == "Comvy Local Administradora")
            ?? new ManagementCompany("Comvy Local Administradora", null, null, "admin@local.comvy", null);
        var condominium = await db.Condominiums.SingleOrDefaultAsync(x => x.Name == "Residencial Comvy Local")
            ?? new Condominium("Residencial Comvy Local", null, null);
        if (db.Entry(company).State == EntityState.Detached) db.Add(company);
        if (db.Entry(condominium).State == EntityState.Detached) db.Add(condominium);
        condominium.SetManagementCompany(company.Id);

        var platform = await FindOrCreateUserAsync(users, "admin@local.comvy", "Comvy Local Platform Admin");
        await EnsureRoleAsync(users, platform, DependencyInjection.PlatformAdminRole);
        var manager = await FindOrCreateUserAsync(users, "manager@local.comvy", "Gestor Local");
        var subManager = await FindOrCreateUserAsync(users, "submanager@local.comvy", "Subsíndico Local");
        var resident = await FindOrCreateUserAsync(users, "resident@local.comvy", "Morador Local");
        var employeeUser = await FindOrCreateUserAsync(users, "employee@local.comvy", "Funcionário Local");

        await db.SaveChangesAsync();

        var managerMembership = await EnsureMembershipAsync(db, manager.Id, condominium.Id, CondominiumRole.Manager);
        var subMembership = await EnsureMembershipAsync(db, subManager.Id, condominium.Id, CondominiumRole.SubManager);
        var residentMembership = await EnsureMembershipAsync(db, resident.Id, condominium.Id, CondominiumRole.Resident);
        var unit = await db.Units.SingleOrDefaultAsync(x => x.CondominiumId == condominium.Id && x.Identifier == "101")
            ?? new Unit(condominium.Id, "101", null, null, "Unidade local de homologação");
        if (db.Entry(unit).State == EntityState.Detached) db.Add(unit);
        if (!await db.UnitMemberships.AnyAsync(x => x.UserId == resident.Id && x.UnitId == unit.Id))
            db.Add(new UnitMembership(resident.Id, unit.Id, UnitRelationshipType.Owner, true, true));

        foreach (var module in Enum.GetValues<SubManagerModule>().Where(x => x != SubManagerModule.Requests))
            if (!await db.SubManagerModulePermissions.AnyAsync(x => x.CondominiumMembershipId == subMembership.Id && x.Module == module))
                db.Add(new SubManagerModulePermission(subMembership.Id, module, platform.Id));

        var category = await db.ManagementCompanyRequestCategories.SingleOrDefaultAsync(x =>
            x.ManagementCompanyId == company.Id && x.Name == "Atendimento geral")
            ?? new ManagementCompanyRequestCategory(company.Id, "Atendimento geral", "Categoria local", ManagementCompanyRequestFormType.Generic);
        if (db.Entry(category).State == EntityState.Detached) db.Add(category);
        var employee = await db.ManagementCompanyEmployees.SingleOrDefaultAsync(x =>
            x.ManagementCompanyId == company.Id && x.UserId == employeeUser.Id)
            ?? new ManagementCompanyEmployee(company.Id, employeeUser.Id, "Atendimento");
        if (db.Entry(employee).State == EntityState.Detached) db.Add(employee);
        await db.SaveChangesAsync();
        if (!await db.ManagementCompanyRequestCategoryResponsibles.AnyAsync(x =>
                x.ManagementCompanyRequestCategoryId == category.Id && x.ManagementCompanyEmployeeId == employee.Id))
            db.Add(new ManagementCompanyRequestCategoryResponsible(category.Id, employee.Id));
        await db.SaveChangesAsync();
        _ = managerMembership;
        _ = residentMembership;
    }

    private static async Task<ApplicationUser> FindOrCreateUserAsync(
        UserManager<ApplicationUser> users, string email, string fullName)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new ApplicationUser(fullName, email, null);
        var result = await users.CreateAsync(user, "ComvyLocal123!");
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        return user;
    }

    private static async Task EnsureRoleAsync(UserManager<ApplicationUser> users, ApplicationUser user, string role)
    {
        if (!await users.IsInRoleAsync(user, role))
        {
            var result = await users.AddToRoleAsync(user, role);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
    }

    private static async Task<CondominiumMembership> EnsureMembershipAsync(
        AppDbContext db, Guid userId, Guid condominiumId, CondominiumRole role)
    {
        var membership = await db.CondominiumMemberships.SingleOrDefaultAsync(x =>
            x.UserId == userId && x.CondominiumId == condominiumId)
            ?? new CondominiumMembership(userId, condominiumId);
        if (db.Entry(membership).State == EntityState.Detached) db.Add(membership);
        if (!await db.CondominiumMembershipRoles.AnyAsync(x =>
                x.CondominiumMembershipId == membership.Id && x.Role == role && x.IsActive && x.RevokedAt == null))
            db.Add(new CondominiumMembershipRole(membership.Id, role));
        return membership;
    }
}
