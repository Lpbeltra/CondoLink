using System.Security.Cryptography;
using CondoLink.Api.Features.Auth;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

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
        group.MapGet("/{accessId:guid}/hard-delete-eligibility", HardDeleteEligibilityAsync);
        group.MapDelete("/{accessId:guid}/hard-delete", HardDeleteAsync);
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
    private static async Task<IResult> HardDeleteEligibilityAsync(Guid accessId, AppDbContext db, CancellationToken ct)
    {
        var result = await EvaluateHardDeleteAsync(accessId, db, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HardDeleteAsync(Guid accessId, [FromBody] HardDeleteConfirmation request, AppDbContext db, CancellationToken ct)
    {
        if (request.Confirmation != "EXCLUIR PERMANENTEMENTE") return Results.BadRequest(new { message = "Confirmação inválida." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var eligibility = await EvaluateHardDeleteAsync(accessId, db, ct);
        if (eligibility is null) return Results.NotFound();
        if (!eligibility.CanHardDelete) return Results.Conflict(new { message = eligibility.Reason });
        var employee = await db.ManagementCompanyEmployees.SingleAsync(x => x.Id == accessId, ct);
        var userId = employee.UserId;
        db.ManagementCompanyRequestCategoryResponsibles.RemoveRange(db.ManagementCompanyRequestCategoryResponsibles.Where(x => x.ManagementCompanyEmployeeId == accessId));
        db.ManagementCompanyEmployees.Remove(employee);
        if (!await db.ManagementCompanyEmployees.AnyAsync(x => x.UserId == userId && x.Id != accessId, ct)
            && !await db.CondominiumMemberships.AnyAsync(x => x.UserId == userId, ct))
            db.Users.Remove(await db.Users.SingleAsync(x => x.Id == userId, ct));
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "O acesso mudou e não pode ser excluído com segurança. Atualize a tela e tente novamente." }); }
        return Results.NoContent();
    }

    private static async Task<HardDeleteResult?> EvaluateHardDeleteAsync(Guid accessId, AppDbContext db, CancellationToken ct)
    {
        var employee = await db.ManagementCompanyEmployees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accessId, ct);
        if (employee is null) return null;
        var userId = employee.UserId;
        var historical = await db.ManagementCompanyRequests.AnyAsync(x => x.CreatedByUserId == userId || x.AcknowledgedByUserId == userId || x.CompletedByUserId == userId || x.CancelledByUserId == userId, ct)
            || await db.ManagementCompanyRequestMessages.AnyAsync(x => x.AuthorUserId == userId, ct)
            || await db.ManagementCompanyRequestHistories.AnyAsync(x => x.ChangedByUserId == userId, ct)
            || await db.ManagementCompanyRequestAttachments.AnyAsync(x => x.UploadedByUserId == userId, ct);
        if (historical) return new(false, "Este acesso participou de solicitações da administradora e precisa permanecer no histórico.");
        if (await db.CondominiumMemberships.AnyAsync(x => x.UserId == userId, ct)) return new(false, "Este usuário possui outro vínculo e sua conta precisa ser preservada.");
        if (await db.ManagementCompanyEmployees.AnyAsync(x => x.UserId == userId && x.Id != accessId, ct)) return new(false, "Este usuário possui outro acesso e sua conta precisa ser preservada.");
        return new(true, null);
    }

    public sealed record HardDeleteConfirmation(string Confirmation);
    public sealed record HardDeleteResult(bool CanHardDelete, string? Reason);
    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return "Aa1!" + new string(RandomNumberGenerator.GetBytes(12).Select(x => chars[x % chars.Length]).ToArray());
    }
    private sealed record AccessRow(ApplicationUser User, string CompanyName);
    public sealed record UpdateRequest(string DisplayName, string? PhoneNumber, string JobTitle, ManagementCompanyAccessType AccessType);
    public sealed record CategoriesRequest(IReadOnlyList<Guid> CategoryIds);
}
