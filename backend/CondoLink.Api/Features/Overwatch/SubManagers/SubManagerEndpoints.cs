using System.Security.Cryptography;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.SubManagers;

public static class SubManagerEndpoints
{
    public static IEndpointRouteBuilder MapSubManagerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/overwatch/submanagers")
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{userId:guid}", UpdateAsync);
        group.MapPatch("/{userId:guid}/status", SetStatusAsync);
        group.MapDelete("/{userId:guid}/condominium", RemoveAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking() on membership.Id equals role.CondominiumMembershipId
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            join condominium in db.Condominiums.AsNoTracking() on membership.CondominiumId equals condominium.Id
            where role.Role == CondominiumRole.SubManager
            orderby user.FullName
            select new Response(user.Id, user.FullName, user.Email!, user.PhoneNumber,
                user.PixKeyType, user.PixKey, user.IsActive, membership.CondominiumId,
                condominium.Name, membership.IsActive && membership.EndedAt == null
                    && role.IsActive && role.RevokedAt == null, user.CreatedAt, user.UpdatedAt))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateAsync(Request request, UserManager<ApplicationUser> users,
        AppDbContext db, FirstAccessService firstAccess, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var condominium = await db.Condominiums.Where(x => x.Id == request.CondominiumId && x.IsActive)
            .Select(x => new { x.Name }).SingleOrDefaultAsync(ct);
        if (condominium is null)
            return Results.NotFound(new { message = "Condomínio não encontrado ou inativo." });
        var email = request.Email!.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(email) is not null)
            return Results.Conflict(new { message = "Já existe um usuário com este e-mail." });

        var password = GenerateTemporaryPassword();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser(request.FullName!.Trim(), email, request.PhoneNumber);
        user.SetPix(request.PixKeyType, request.PixKey);
        user.RequirePasswordChange();
        user.SetEmailDeliveryEnabled(FirstAccessEmailPolicy.IsDeliverable(email));
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return Results.BadRequest(new { errors = created.Errors.Select(x => x.Description) });
        var assignment = await AssignAsync(user, request.CondominiumId, db, ct);
        if (assignment is not null) return Results.Conflict(new { message = assignment });
        await transaction.CommitAsync(ct);
        _ = await firstAccess.SendAsync(user, condominium.Name, ct);
        return Results.Created($"/overwatch/submanagers/{user.Id}", new CreatedResponse(
            user.Id, user.FullName, user.Email!, user.PhoneNumber, user.PixKeyType,
            user.PixKey, user.IsActive, request.CondominiumId, password));
    }

    private static async Task<IResult> UpdateAsync(Guid userId, Request request, AppDbContext db, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Results.NotFound();
        user.Update(request.FullName!, request.PhoneNumber);
        user.SetPix(request.PixKeyType, request.PixKey);
        var current = await ActiveRoleAsync(userId, db, ct);
        if (current?.CondominiumId != request.CondominiumId)
        {
            if (current is not null)
            {
                current.Role.Deactivate();
                await db.SaveChangesAsync(ct);
            }
            var assignment = await AssignAsync(user, request.CondominiumId, db, ct);
            if (assignment is not null) return Results.Conflict(new { message = assignment });
        }
        await db.SaveChangesAsync(ct);
        await ManagementContextReconciler.ReconcileAsync(user, db, ct);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetStatusAsync(Guid userId, StatusRequest request, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Results.NotFound();
        user.SetActiveStatus(request.IsActive);
        if (!request.IsActive) user.ClearActiveManagementCondominium();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveAsync(Guid userId, AppDbContext db, CancellationToken ct)
    {
        var current = await ActiveRoleAsync(userId, db, ct);
        if (current is null) return Results.NotFound();
        current.Role.Deactivate();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        await ManagementContextReconciler.ReconcileAsync(user, db, ct);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<string?> AssignAsync(ApplicationUser user, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 9182));", ct);
        if (await ActiveRoleAsync(user.Id, db, ct) is not null)
            return "Este usuário já possui um vínculo ativo como subsíndico.";
        var occupied = await (from m in db.CondominiumMemberships
            join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
            where m.CondominiumId == condominiumId && m.IsActive && m.EndedAt == null
                && r.Role == CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null
            select r.Id).AnyAsync(ct);
        if (occupied) return "Este condomínio já possui um subsíndico ativo.";
        var membership = await db.CondominiumMemberships.SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.CondominiumId == condominiumId, ct);
        if (membership is null) { membership = new CondominiumMembership(user.Id, condominiumId); db.Add(membership); }
        else membership.Activate();
        var role = await db.CondominiumMembershipRoles.SingleOrDefaultAsync(
            x => x.CondominiumMembershipId == membership.Id && x.Role == CondominiumRole.SubManager, ct);
        if (role is null) db.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.SubManager));
        else role.Activate();
        await db.SaveChangesAsync(ct);
        return null;
    }

    private static Task<ActiveRole?> ActiveRoleAsync(Guid userId, AppDbContext db, CancellationToken ct) =>
        (from m in db.CondominiumMemberships
         join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
         where m.UserId == userId && m.IsActive && m.EndedAt == null
            && r.Role == CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null
         select new ActiveRole(m.CondominiumId, r)).SingleOrDefaultAsync(ct);

    private static string? Validate(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email)
            || request.CondominiumId == Guid.Empty) return "Nome, e-mail e condomínio são obrigatórios.";
        if (!Domain.PhoneNumberNormalizer.IsValidOptional(request.PhoneNumber)) return "Telefone inválido.";
        try { _ = new ApplicationUser(request.FullName, request.Email, request.PhoneNumber); }
        catch (ArgumentException ex) { return ex.Message; }
        return null;
    }
    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return "Aa1!" + new string(RandomNumberGenerator.GetBytes(12).Select(x => chars[x % chars.Length]).ToArray());
    }
    private sealed record ActiveRole(Guid CondominiumId, CondominiumMembershipRole Role);
    public sealed record Request(string? FullName, string? Email, string? PhoneNumber,
        Guid CondominiumId, PixKeyType? PixKeyType, string? PixKey);
    public sealed record StatusRequest(bool IsActive);
    public sealed record Response(Guid Id, string FullName, string Email, string? PhoneNumber,
        PixKeyType? PixKeyType, string? PixKey, bool IsActive, Guid CondominiumId,
        string CondominiumName, bool HasActiveLink, DateTime CreatedAt, DateTime UpdatedAt);
    public sealed record CreatedResponse(Guid Id, string FullName, string Email, string? PhoneNumber,
        PixKeyType? PixKeyType, string? PixKey, bool IsActive, Guid CondominiumId, string TemporaryPassword);
}
