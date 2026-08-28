using System.Security.Cryptography;
using CondoLink.Api.Features.Auth;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class ManagementCompanyAccessLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapManagementCompanyAccessLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/overwatch/management-company-accesses")
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        group.MapPut("/{accessId:guid}", UpdateAsync);
        group.MapPut("/{accessId:guid}/categories", SetCategoriesAsync);
        group.MapPost("/{accessId:guid}/resend-first-access", ResendAsync);
        group.MapPost("/{accessId:guid}/reset-password", ResetPasswordAsync);
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(Guid accessId, UpdateRequest request,
        AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 200
            || string.IsNullOrWhiteSpace(request.JobTitle) || request.JobTitle.Length > 100
            || !Enum.IsDefined(request.AccessType)) return Results.BadRequest(new { message = "Dados do acesso inválidos." });
        var row = await (from access in db.ManagementCompanyEmployees
            join user in db.Users on access.UserId equals user.Id
            where access.Id == accessId select new { Access = access, User = user }).SingleOrDefaultAsync(ct);
        if (row is null) return Results.NotFound();
        row.User.Update(request.DisplayName, request.PhoneNumber);
        row.Access.Update(request.JobTitle, request.AccessType);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetCategoriesAsync(Guid accessId, CategoriesRequest request,
        AppDbContext db, CancellationToken ct)
    {
        var access = await db.ManagementCompanyEmployees.SingleOrDefaultAsync(x => x.Id == accessId, ct);
        if (access is null) return Results.NotFound();
        var ids = request.CategoryIds.Distinct().ToArray();
        var validCount = await db.ManagementCompanyRequestCategories.CountAsync(x =>
            ids.Contains(x.Id) && x.ManagementCompanyId == access.ManagementCompanyId, ct);
        if (validCount != ids.Length) return Results.BadRequest(new { message = "Uma ou mais categorias não pertencem à administradora." });
        var current = await db.ManagementCompanyRequestCategoryResponsibles
            .Where(x => x.ManagementCompanyEmployeeId == accessId).ToListAsync(ct);
        db.RemoveRange(current.Where(x => !ids.Contains(x.ManagementCompanyRequestCategoryId)));
        var currentIds = current.Select(x => x.ManagementCompanyRequestCategoryId).ToHashSet();
        db.AddRange(ids.Where(x => !currentIds.Contains(x))
            .Select(x => new Domain.Entities.ManagementCompanyRequestCategoryResponsible(x, accessId)));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResendAsync(Guid accessId, AppDbContext db,
        FirstAccessService firstAccess, CancellationToken ct)
    {
        var row = await LoadAsync(accessId, db, ct);
        if (row is null) return Results.NotFound();
        if (!row.User.MustChangePassword) return Results.Conflict(new { message = "O primeiro acesso já foi concluído." });
        var sent = await firstAccess.SendAsync(row.User, row.CompanyName, ct);
        return Results.Ok(new { sent });
    }

    private static async Task<IResult> ResetPasswordAsync(Guid accessId, AppDbContext db,
        UserManager<ApplicationUser> users, FirstAccessService firstAccess, CancellationToken ct)
    {
        var row = await LoadAsync(accessId, db, ct);
        if (row is null) return Results.NotFound();
        var password = GenerateTemporaryPassword();
        var token = await users.GeneratePasswordResetTokenAsync(row.User);
        var reset = await users.ResetPasswordAsync(row.User, token, password);
        if (!reset.Succeeded) return Results.BadRequest(new { errors = reset.Errors.Select(x => x.Description) });
        row.User.RequirePasswordChange();
        await users.UpdateSecurityStampAsync(row.User);
        await users.UpdateAsync(row.User);
        var sent = await firstAccess.SendAsync(row.User, row.CompanyName, ct);
        return Results.Ok(new { row.User.Email, temporaryPassword = password, invitationSent = sent });
    }

    private static Task<AccessRow?> LoadAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        (from access in db.ManagementCompanyEmployees
         join user in db.Users on access.UserId equals user.Id
         join company in db.ManagementCompanies on access.ManagementCompanyId equals company.Id
         where access.Id == id select new AccessRow(user, company.Name)).SingleOrDefaultAsync(ct);
    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return "Aa1!" + new string(RandomNumberGenerator.GetBytes(12).Select(x => chars[x % chars.Length]).ToArray());
    }
    private sealed record AccessRow(ApplicationUser User, string CompanyName);
    public sealed record UpdateRequest(string DisplayName, string? PhoneNumber, string JobTitle, ManagementCompanyAccessType AccessType);
    public sealed record CategoriesRequest(IReadOnlyList<Guid> CategoryIds);
}
