using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public static class AdministratorRequestEndpoints
{
    public static IEndpointRouteBuilder MapAdministratorRequests(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/administrator").RequireAuthorization();
        group.MapGet("/context", Context);
        group.MapGet("/requests", List);
        group.MapGet("/requests/options", Options);
        return app;
    }

    private sealed record AdministratorScope(Guid AccessId, Guid ManagementCompanyId, string JobTitle, ManagementCompanyAccessType AccessType, string CompanyName, List<Guid> CategoryIds);
    private static async Task<AdministratorScope> Scope(ClaimsPrincipal principal, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    {
        var userId = await access.RequireUserIdAsync(principal, ct);
        var row = await db.ManagementCompanyEmployees.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => new { x.Id, x.ManagementCompanyId, x.JobTitle, x.AccessType, CompanyName = x.ManagementCompany.Name })
            .SingleOrDefaultAsync(ct) ?? throw new ForbiddenAppException("Seu acesso à administradora não está ativo.");
        var categoryIds = await db.ManagementCompanyRequestCategoryResponsibles.AsNoTracking()
            .Where(x => x.ManagementCompanyEmployeeId == row.Id).Select(x => x.ManagementCompanyRequestCategoryId).ToListAsync(ct);
        return new(row.Id, row.ManagementCompanyId, row.JobTitle, row.AccessType, row.CompanyName, categoryIds);
    }

    private static async Task<IResult> Context(ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    {
        var scope = await Scope(user, db, access, ct);
        var categories = await db.ManagementCompanyRequestCategories.AsNoTracking().Where(x => x.ManagementCompanyId == scope.ManagementCompanyId && scope.CategoryIds.Contains(x.Id)).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, Type = x.FormType == ManagementCompanyRequestFormType.UnitFine ? ManagementCompanyRequestType.Fine : x.FormType == ManagementCompanyRequestFormType.SupplierPayment ? ManagementCompanyRequestType.Payment : ManagementCompanyRequestType.GeneralQuestion }).ToListAsync(ct);
        return Results.Ok(new { managementCompanyId = scope.ManagementCompanyId, managementCompanyName = scope.CompanyName, scope.JobTitle, scope.AccessType, categories });
    }

    private static async Task<IResult> Options(ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct)
    {
        var scope = await Scope(user, db, access, ct); var ids = scope.CategoryIds;
        var categories = await db.ManagementCompanyRequestCategories.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var condominiums = await db.ManagementCompanyRequests.AsNoTracking().Where(x => x.ManagementCompanyId == scope.ManagementCompanyId && ids.Contains(x.CategoryId)).Select(x => new { x.CondominiumId, Name = db.Condominiums.Where(c => c.Id == x.CondominiumId).Select(c => c.Name).First() }).Distinct().OrderBy(x => x.Name).ToListAsync(ct);
        return Results.Ok(new { condominiums, categories });
    }

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, ManagementCompanyRequestAccessService access, CancellationToken ct, Guid? condominiumId = null, Guid? categoryId = null, ManagementCompanyRequestStatus? status = null, string? search = null, DateOnly? from = null, DateOnly? to = null, bool includeCompleted = false, bool includeCancelled = false, int page = 1, int pageSize = 20)
    {
        var scope = await Scope(user, db, access, ct); var ids = scope.CategoryIds;
        if (categoryId.HasValue && !ids.Contains(categoryId.Value)) throw new ForbiddenAppException("Você não possui acesso a esta categoria de solicitação.");
        if (from.HasValue && to.HasValue && from > to) throw new ValidationAppException("A data inicial não pode ser posterior à data final.");
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.ManagementCompanyRequests.AsNoTracking().Where(x => x.ManagementCompanyId == scope.ManagementCompanyId && ids.Contains(x.CategoryId));
        if (condominiumId.HasValue) query = query.Where(x => x.CondominiumId == condominiumId);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        else
        {
            if (!includeCompleted) query = query.Where(x => x.Status != ManagementCompanyRequestStatus.Completed);
            if (!includeCancelled) query = query.Where(x => x.Status != ManagementCompanyRequestStatus.Cancelled);
        }
        if (from.HasValue) { var start = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc); query = query.Where(x => x.CreatedAt >= start); }
        if (to.HasValue) { var end = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc); query = query.Where(x => x.CreatedAt < end); }
        if (!string.IsNullOrWhiteSpace(search)) { var q = search.Trim(); query = query.Where(r => r.FriendlyIdentifier.Contains(q) || db.ManagementCompanyFineRequests.Any(x => x.RequestId == r.Id && x.Nature.Contains(q)) || db.ManagementCompanyPaymentRequests.Any(x => x.RequestId == r.Id && x.Nature.Contains(q)) || db.ManagementCompanyGeneralQuestionRequests.Any(x => x.RequestId == r.Id && x.Theme.Contains(q))); }
        if (condominiumId.HasValue && !await query.AnyAsync(ct) && !await db.ManagementCompanyRequests.AnyAsync(x => x.CondominiumId == condominiumId && x.ManagementCompanyId == scope.ManagementCompanyId && ids.Contains(x.CategoryId), ct)) throw new ForbiddenAppException("Condomínio fora do seu escopo.");
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Status == ManagementCompanyRequestStatus.Submitted).ThenByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(r => new { r.Id, r.FriendlyIdentifier, r.CondominiumId, CondominiumName = db.Condominiums.Where(c => c.Id == r.CondominiumId).Select(c => c.Name).First(), r.ManagementCompanyId, ManagementCompanyName = scope.CompanyName, r.CategoryId, CategoryName = db.ManagementCompanyRequestCategories.Where(c => c.Id == r.CategoryId).Select(c => c.Name).First(), r.Type, r.Status, Subject = r.Type == ManagementCompanyRequestType.Fine ? db.ManagementCompanyFineRequests.Where(x => x.RequestId == r.Id).Select(x => x.Nature).First() : r.Type == ManagementCompanyRequestType.Payment ? db.ManagementCompanyPaymentRequests.Where(x => x.RequestId == r.Id).Select(x => x.Nature).First() : db.ManagementCompanyGeneralQuestionRequests.Where(x => x.RequestId == r.Id).Select(x => x.Theme).First(), Unit = r.Status == ManagementCompanyRequestStatus.Submitted ? null : db.ManagementCompanyFineRequests.Where(x => x.RequestId == r.Id).Select(x => db.Units.Where(u => u.Id == x.UnitId).Select(u => u.Identifier).FirstOrDefault()).FirstOrDefault(), Block = r.Status == ManagementCompanyRequestStatus.Submitted ? null : db.ManagementCompanyFineRequests.Where(x => x.RequestId == r.Id).Select(x => db.Units.Where(u => u.Id == x.UnitId).Select(u => u.BlockId == null ? null : db.CondominiumBlocks.Where(b => b.Id == u.BlockId).Select(b => b.Identifier).FirstOrDefault()).FirstOrDefault()).FirstOrDefault(), Value = r.Status == ManagementCompanyRequestStatus.Submitted ? null : r.Type == ManagementCompanyRequestType.Fine ? db.ManagementCompanyFineRequests.Where(x => x.RequestId == r.Id).Select(x => x.Value).FirstOrDefault() : r.Type == ManagementCompanyRequestType.Payment ? db.ManagementCompanyPaymentRequests.Where(x => x.RequestId == r.Id).Select(x => (decimal?)x.Value).FirstOrDefault() : null, BeneficiaryName = r.Status == ManagementCompanyRequestStatus.Submitted ? null : db.ManagementCompanyPaymentRequests.Where(x => x.RequestId == r.Id).Select(x => x.BeneficiaryName).FirstOrDefault(), r.CreatedAt, r.UpdatedAt }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total, hasMore = page * pageSize < total });
    }
}
