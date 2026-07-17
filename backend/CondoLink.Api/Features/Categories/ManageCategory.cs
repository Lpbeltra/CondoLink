using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.Categories;

public static class ManageCategory
{
    public static IEndpointRouteBuilder MapManageCategory(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/condominiums/{condominiumId:guid}/categories/{categoryId:guid}", UpdateAsync).RequireAuthorization();
        endpoints.MapDelete("/condominiums/{condominiumId:guid}/categories/{categoryId:guid}", DeleteAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(Guid condominiumId, Guid categoryId, Request request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId && item.CondominiumId == condominiumId, ct);
        if (category is null) return Results.NotFound(new { error = "Category not found." });
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return Results.BadRequest(new { error = "Name is required." });
        if (name.Length > 100) return Results.BadRequest(new { error = "Name must not exceed 100 characters." });
        var normalized = name.ToUpperInvariant();
        if (await db.Categories.AnyAsync(item => item.Id != categoryId && item.CondominiumId == condominiumId && item.NormalizedName == normalized, ct)) return Duplicate();
        category.Rename(name);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (IsDuplicate(exception)) { return Duplicate(); }
        return Results.Ok(new Response(category.Id, category.CondominiumId, category.Name, category.Description, await db.Requests.CountAsync(item => item.CategoryId == categoryId, ct)));
    }

    private static async Task<IResult> DeleteAsync(Guid condominiumId, Guid categoryId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await IsManager(principal, condominiumId, db, ct)) return Results.Forbid();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId && item.CondominiumId == condominiumId, ct);
        if (category is null) return Results.NotFound(new { error = "Category not found." });
        if (await db.Requests.AnyAsync(item => item.CategoryId == categoryId, ct)) return Results.Conflict(new { error = "Não é possível excluir esta categoria porque ela já foi utilizada em solicitações." });
        db.Categories.Remove(category); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<bool> IsManager(ClaimsPrincipal principal, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && await db.CondominiumMemberships.AsNoTracking().Where(item => item.UserId == userId && item.CondominiumId == condominiumId && item.IsActive && item.EndedAt == null).Join(db.CondominiumMembershipRoles.AsNoTracking().Where(role => role.Role == CondominiumRole.Manager && role.IsActive && role.RevokedAt == null), item => item.Id, role => role.CondominiumMembershipId, (_, _) => true).AnyAsync(ct);
    }
    private static bool IsDuplicate(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CategoryConfiguration.UniqueCondominiumNormalizedNameIndex };
    private static IResult Duplicate() => Results.Conflict(new { error = "A category with this name already exists in the condominium." });
    public sealed record Request(string? Name);
    public sealed record Response(Guid Id, Guid CondominiumId, string Name, string? Description, int RequestCount);
}
