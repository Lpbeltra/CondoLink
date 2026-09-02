using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        group.MapGet("/search", SearchAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{userId:guid}", UpdateAsync);
        group.MapPatch("/{userId:guid}/status", SetStatusAsync);
        group.MapGet("/{userId:guid}/permissions", ListPermissionsAsync);
        group.MapPut("/{userId:guid}/permissions", UpdatePermissionsAsync);
        group.MapDelete("/{userId:guid}/condominium", RemoveAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(string? query, string? condominiumId, AppDbContext db, CancellationToken ct)
    {
        var term = query?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Results.Ok(Array.Empty<ExistingUserResponse>());
        var preferredCondominiumId = Guid.TryParse(condominiumId, out var parsedCondominiumId)
            ? parsedCondominiumId
            : (Guid?)null;

        var users = await db.Users.AsNoTracking()
            .Where(user => user.IsActive
                && !db.CondominiumMembershipRoles.Any(role => role.Role == CondominiumRole.SubManager
                    && role.IsActive && role.RevokedAt == null
                    && db.CondominiumMemberships.Any(membership => membership.Id == role.CondominiumMembershipId
                        && membership.UserId == user.Id && membership.IsActive && membership.EndedAt == null)))
            .Where(user => user.FullName.ToLower().Contains(term)
                || (user.Email != null && user.Email.ToLower().Contains(term))
                || (user.PhoneNumber != null && user.PhoneNumber.Contains(term)))
            .OrderBy(user => user.FullName)
            .Take(25)
            .Select(user => new { user.Id, user.FullName, user.Email, user.PhoneNumber, user.PixKeyType, user.PixKey })
            .ToListAsync(ct);

        var ids = users.Select(user => user.Id).ToArray();
        var units = await (from link in db.UnitMemberships.AsNoTracking()
            join unit in db.Units.AsNoTracking() on link.UnitId equals unit.Id
            join condominium in db.Condominiums.AsNoTracking() on unit.CondominiumId equals condominium.Id
            where ids.Contains(link.UserId) && link.IsActive && link.EndedAt == null
            select new { link.UserId, unit.CondominiumId, CondominiumName = condominium.Name, Unit = unit.Identifier })
            .ToListAsync(ct);

        var rows = users.Select(user =>
        {
            var links = units.Where(link => link.UserId == user.Id).ToArray();
            var preferred = preferredCondominiumId.HasValue
                ? links.FirstOrDefault(link => link.CondominiumId == preferredCondominiumId.Value) ?? links.FirstOrDefault()
                : links.FirstOrDefault();
            return new ExistingUserResponse(user.Id, user.FullName, user.Email!, user.PhoneNumber,
                user.PixKeyType, user.PixKey, links.Select(link => new ExistingUserLink(
                    link.CondominiumId, link.CondominiumName, link.Unit)).ToArray(),
                preferred?.CondominiumId, preferred?.CondominiumName, preferred?.Unit);
        }).OrderByDescending(row => preferredCondominiumId.HasValue && row.CondominiumId == preferredCondominiumId)
            .ThenBy(row => row.FullName);
        return Results.Ok(rows);
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
        if (request.ExistingUserId.HasValue)
            return await PromoteExistingAsync(request, db, ct);

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
        ApplicationUser user;
        try
        {
            user = new ApplicationUser(request.FullName!.Trim(), email, request.PhoneNumber);
            user.SetPix(request.PixKeyType, request.PixKey);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        user.RequirePasswordChange();
        user.SetEmailDeliveryEnabled(FirstAccessEmailPolicy.IsDeliverable(email));
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return Results.BadRequest(new { message = IdentityErrorMessage(created.Errors), errors = created.Errors.Select(x => x.Description) });
        string? assignment;
        try { assignment = await AssignAsync(user, request.CondominiumId, db, ct); }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Não foi possível criar o vínculo de subsíndico. Verifique se o usuário ou condomínio já possui vínculo ativo." });
        }
        if (assignment is not null) return Results.Conflict(new { message = assignment });
        await transaction.CommitAsync(ct);
        _ = await firstAccess.SendAsync(user, condominium.Name, ct);
        return Results.Created($"/overwatch/submanagers/{user.Id}", new CreatedResponse(
            user.Id, user.FullName, user.Email!, user.PhoneNumber, user.PixKeyType,
            user.PixKey, user.IsActive, request.CondominiumId, password));
    }

    private static async Task<IResult> PromoteExistingAsync(Request request, AppDbContext db, CancellationToken ct)
    {
        if (request.ExistingUserId is not Guid existingUserId)
            return Results.BadRequest(new { message = "Usuário existente é obrigatório." });
        if (request.CondominiumId == Guid.Empty)
            return Results.BadRequest(new { message = "Condomínio é obrigatório." });
        var condominium = await db.Condominiums.AsNoTracking()
            .Where(x => x.Id == request.CondominiumId && x.IsActive)
            .Select(x => x.Name).SingleOrDefaultAsync(ct);
        if (condominium is null)
            return Results.NotFound(new { message = "Condomínio não encontrado ou inativo." });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == existingUserId, ct);
        if (user is null) return Results.NotFound(new { message = "Usuário não encontrado." });
        if (!user.IsActive) return Results.Conflict(new { message = "Este usuário está inativo." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        string? assignment;
        try { assignment = await AssignAsync(user, request.CondominiumId, db, ct); }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Não foi possível criar o vínculo de subsíndico. Verifique se o usuário já possui esse vínculo ativo." });
        }
        if (assignment is not null) return Results.Conflict(new { message = assignment });
        await transaction.CommitAsync(ct);
        return Results.Created($"/overwatch/submanagers/{user.Id}", new CreatedResponse(
            user.Id, user.FullName, user.Email!, user.PhoneNumber, user.PixKeyType,
            user.PixKey, user.IsActive, request.CondominiumId, null));
    }

    private static async Task<IResult> ListPermissionsAsync(Guid userId, AppDbContext db, CancellationToken ct)
    {
        var membership = await ActiveRoleAsync(userId, db, ct);
        if (membership is null) return Results.NotFound();
        await SubManagerAccess.EnsureDefaultsAsync(db, membership.Role.CondominiumMembershipId, userId, ct);
        await db.SaveChangesAsync(ct);
        var rows = await db.SubManagerModulePermissions.AsNoTracking()
            .Where(x => x.CondominiumMembershipId == membership.Role.CondominiumMembershipId)
            .OrderBy(x => x.Module)
            .Select(x => new { module = x.Module.ToString(), allowed = x.IsAllowed })
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> UpdatePermissionsAsync(Guid userId, PermissionRequest request, ClaimsPrincipal principal, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var membership = await ActiveRoleAsync(userId, db, ct);
        if (membership is null) return Results.NotFound();
        if (request.Permissions is null || request.Permissions.Count != Enum.GetValues<SubManagerModule>().Length
            || request.Permissions.Any(x => !Enum.TryParse<SubManagerModule>(x.Module, true, out _)))
            return Results.BadRequest(new { message = "Informe exatamente uma permissão por módulo." });
        foreach (var item in request.Permissions)
        {
            var module = Enum.Parse<SubManagerModule>(item.Module, true);
            var permission = await db.SubManagerModulePermissions.SingleOrDefaultAsync(
                x => x.CondominiumMembershipId == membership.Role.CondominiumMembershipId && x.Module == module, ct);
            if (permission is null)
            {
                permission = new SubManagerModulePermission(membership.Role.CondominiumMembershipId, module, userId);
                db.Add(permission);
            }
            var actorValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(actorValue, out var actorId)) return Results.Unauthorized();
            permission.SetAllowed(item.Allowed, actorId);
        }
        await db.SaveChangesAsync(ct);
        loggerFactory.CreateLogger(typeof(SubManagerEndpoints)).LogInformation("SubManager module permissions updated. TargetUserId: {TargetUserId}; Modules: {Modules}", userId, string.Join(',', request.Permissions.Select(x => x.Module)));
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateAsync(Guid userId, Request request, UserManager<ApplicationUser> users, AppDbContext db, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Results.NotFound();
        var email = request.Email!.Trim().ToLowerInvariant();
        var emailOwner = await users.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != userId)
            return Results.Conflict(new { message = "Já existe um usuário com este e-mail." });
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await users.SetEmailAsync(user, email);
            if (!emailResult.Succeeded) return Results.BadRequest(new { errors = emailResult.Errors.Select(x => x.Description) });
            var nameResult = await users.SetUserNameAsync(user, email);
            if (!nameResult.Succeeded) return Results.BadRequest(new { errors = nameResult.Errors.Select(x => x.Description) });
        }
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

    /// <summary>Internal (not private) so Postgres-backed concurrency tests can call the real assignment path directly.</summary>
    internal static async Task<string?> AssignAsync(ApplicationUser user, Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 9182));", ct);
        if (await ActiveRoleAsync(user.Id, db, ct) is not null)
            return "Este usuário já possui um vínculo ativo como subsíndico.";
        var membership = await db.CondominiumMemberships.SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.CondominiumId == condominiumId, ct);
        if (membership is null)
        {
            membership = new CondominiumMembership(user.Id, condominiumId);
            db.Add(membership);
        }
        else membership.Activate();
        var role = await db.CondominiumMembershipRoles.SingleOrDefaultAsync(
            x => x.CondominiumMembershipId == membership.Id && x.Role == CondominiumRole.SubManager, ct);
        if (role is null) db.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.SubManager));
        else role.Activate();
        await db.SaveChangesAsync(ct);
        await SubManagerAccess.EnsureDefaultsAsync(db, membership.Id, user.Id, ct);
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
        try
        {
            var user = new ApplicationUser(request.FullName, request.Email, request.PhoneNumber);
            user.SetPix(request.PixKeyType, request.PixKey);
        }
        catch (ArgumentException ex) { return ex.Message; }
        return null;
    }
    private static string IdentityErrorMessage(IEnumerable<IdentityError> errors) =>
        errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
            ? "Já existe um usuário com este e-mail."
            : "Os dados informados não puderam ser validados.";
    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return "Aa1!" + new string(RandomNumberGenerator.GetBytes(12).Select(x => chars[x % chars.Length]).ToArray());
    }
    private sealed record ActiveRole(Guid CondominiumId, CondominiumMembershipRole Role);
    public sealed record Request(string? FullName, string? Email, string? PhoneNumber,
        Guid CondominiumId, PixKeyType? PixKeyType, string? PixKey, Guid? ExistingUserId = null);
    public sealed record StatusRequest(bool IsActive);
    public sealed record PermissionRequest(IReadOnlyList<PermissionItem>? Permissions);
    public sealed record PermissionItem(string Module, bool Allowed);
    public sealed record Response(Guid Id, string FullName, string Email, string? PhoneNumber,
        PixKeyType? PixKeyType, string? PixKey, bool IsActive, Guid CondominiumId,
        string CondominiumName, bool HasActiveLink, DateTime CreatedAt, DateTime UpdatedAt);
    public sealed record CreatedResponse(Guid Id, string FullName, string Email, string? PhoneNumber,
        PixKeyType? PixKeyType, string? PixKey, bool IsActive, Guid CondominiumId, string? TemporaryPassword);
    public sealed record ExistingUserResponse(Guid UserId, string FullName, string Email, string? PhoneNumber,
        PixKeyType? PixKeyType, string? PixKey, IReadOnlyList<ExistingUserLink> Links,
        Guid? CondominiumId, string? CondominiumName, string? Unit);
    public sealed record ExistingUserLink(Guid CondominiumId, string CondominiumName, string Unit);
}
