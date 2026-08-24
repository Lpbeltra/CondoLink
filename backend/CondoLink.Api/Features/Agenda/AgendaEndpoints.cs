using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Agenda;

public static class AgendaEndpoints
{
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/management/condominiums/{condominiumId:guid}/agenda")
            .RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapGet("/options", OptionsAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{reminderId:guid}", UpdateAsync);
        group.MapDelete("/{reminderId:guid}", DeleteAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(Guid condominiumId,
        ClaimsPrincipal principal, AppDbContext db, string? search,
        string? view, CancellationToken ct)
    {
        var access = await Authorize(principal, condominiumId, db, ct);
        if (!access.Allowed) return access.Result!;
        var query = db.AgendaReminders.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId);
        if (view == "upcoming") query = query.Where(x => x.IsActive);
        else if (view == "recurring") query = query.Where(x => x.IsActive
            && x.RecurrenceType != AgendaRecurrenceType.None);
        else if (view == "past") query = query.Where(x => !x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term)
                || (x.RelatedThirdParty != null
                    && x.RelatedThirdParty.ToLower().Contains(term)));
        }
        var rows = await query.OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.NextOccurrenceAtUtc).ThenBy(x => x.Title)
            .Select(x => new ReminderResponse(x.Id, x.Title, x.Description, x.UnitId,
                db.Units.Where(u => u.Id == x.UnitId).Select(u => u.Identifier).FirstOrDefault(),
                (from u in db.Units join b in db.CondominiumBlocks on u.BlockId equals b.Id
                    where u.Id == x.UnitId select b.Identifier).FirstOrDefault(),
                x.RelatedThirdParty, x.StartsAtUtc, x.NextOccurrenceAtUtc,
                x.TimeZoneId, x.RecurrenceType.ToString(), x.NotifyByWhatsApp,
                x.NotifyByEmail, x.IsActive, x.CompletedAt, x.CreatedAt,
                db.AgendaReminderRequests.Count(l => l.ReminderId == x.Id),
                db.AgendaReminderRequests.Where(l => l.ReminderId == x.Id)
                    .Select(l => l.RequestId).ToArray())).ToArrayAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> OptionsAsync(Guid condominiumId,
        ClaimsPrincipal principal, AppDbContext db, Guid? reminderId,
        CancellationToken ct)
    {
        var access = await Authorize(principal, condominiumId, db, ct);
        if (!access.Allowed) return access.Result!;
        var units = await db.Units.AsNoTracking().Where(x => x.CondominiumId == condominiumId && x.IsActive)
            .OrderBy(x => x.Identifier).Select(x => new { x.Id, x.Identifier,
                Block = db.CondominiumBlocks.Where(b => b.Id == x.BlockId)
                    .Select(b => b.Identifier).FirstOrDefault() }).ToArrayAsync(ct);
        var requests = await db.Requests.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId
                && x.Status != RequestStatus.Resolved && x.Status != RequestStatus.Cancelled)
            .Select(x => new RequestOption(x.Id, x.Id.ToString(), x.Title,
                db.Set<ApplicationUser>().Where(u => u.Id == x.AuthorUserId)
                    .Select(u => u.FullName).FirstOrDefault()!,
                db.Units.Where(u => u.Id == x.TargetUnitId)
                    .Select(u => u.Identifier).FirstOrDefault(), x.Status.ToString(),
                db.AgendaReminderRequests.Where(l => l.RequestId == x.Id)
                    .Select(l => (Guid?)l.ReminderId).FirstOrDefault()))
            .Where(x => x.LinkedReminderId == null || x.LinkedReminderId == reminderId)
            .OrderByDescending(x => x.Protocol).ToArrayAsync(ct);
        return Results.Ok(new { units, requests });
    }

    private static Task<IResult> CreateAsync(Guid condominiumId, ReminderInput input,
        ClaimsPrincipal principal, AppDbContext db, IOptions<AgendaOptions> options,
        CancellationToken ct) => SaveAsync(condominiumId, null, input, principal,
            db, options, ct);
    private static Task<IResult> UpdateAsync(Guid condominiumId, Guid reminderId,
        ReminderInput input, ClaimsPrincipal principal, AppDbContext db,
        IOptions<AgendaOptions> options, CancellationToken ct) =>
        SaveAsync(condominiumId, reminderId, input, principal, db, options, ct);

    private static async Task<IResult> SaveAsync(Guid condominiumId, Guid? reminderId,
        ReminderInput input, ClaimsPrincipal principal, AppDbContext db,
        IOptions<AgendaOptions> options, CancellationToken ct)
    {
        var access = await Authorize(principal, condominiumId, db, ct);
        if (!access.Allowed) return access.Result!;
        if (!Enum.TryParse<AgendaRecurrenceType>(input.RecurrenceType, true,
                out var recurrence) || !Enum.IsDefined(recurrence))
            return Results.BadRequest(new { error = "Recorrência inválida." });
        var starts = input.StartsAtUtc.Kind == DateTimeKind.Utc
            ? input.StartsAtUtc : input.StartsAtUtc.ToUniversalTime();
        var timezone = options.Value.OperationalTimeZone;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch { return Results.Problem("O fuso operacional da Agenda é inválido."); }
        if (input.UnitId.HasValue && !await db.Units.AnyAsync(x => x.Id == input.UnitId
            && x.CondominiumId == condominiumId && x.IsActive, ct))
            return Results.BadRequest(new { error = "A unidade não pertence ao condomínio." });
        var requestIds = input.RequestIds.Distinct().ToArray();
        var eligible = await db.Requests.CountAsync(x => requestIds.Contains(x.Id)
            && x.CondominiumId == condominiumId
            && x.Status != RequestStatus.Resolved && x.Status != RequestStatus.Cancelled, ct);
        if (eligible != requestIds.Length)
            return Results.BadRequest(new { error = "Um atendimento é inválido ou já foi finalizado." });
        var conflicting = await db.AgendaReminderRequests.AnyAsync(x =>
            requestIds.Contains(x.RequestId) && x.ReminderId != reminderId, ct);
        if (conflicting) return Results.Conflict(new
            { error = "Um atendimento já está vinculado a outro lembrete." });
        var now = DateTime.UtcNow;
        AgendaReminder reminder;
        if (reminderId.HasValue)
        {
            reminder = await db.AgendaReminders.SingleOrDefaultAsync(x =>
                x.Id == reminderId && x.CondominiumId == condominiumId, ct)
                ?? throw new KeyNotFoundException("Lembrete não encontrado.");
            reminder.Update(input.Title, input.Description, input.UnitId,
                input.RelatedThirdParty, starts, timezone, recurrence,
                input.NotifyByWhatsApp, input.NotifyByEmail, now);
            await db.AgendaReminderRequests.Where(x => x.ReminderId == reminder.Id)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            reminder = new AgendaReminder(condominiumId, access.UserId, input.Title,
                input.Description, input.UnitId, input.RelatedThirdParty, starts,
                timezone, recurrence, input.NotifyByWhatsApp, input.NotifyByEmail, now);
            db.Add(reminder);
        }
        db.AddRange(requestIds.Select(id => new AgendaReminderRequest(reminder.Id,
            id, access.UserId, now)));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new
            { error = "Um atendimento foi vinculado por outra operação." }); }
        return Results.Ok(new { reminder.Id });
    }

    private static async Task<IResult> DeleteAsync(Guid condominiumId, Guid reminderId,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var access = await Authorize(principal, condominiumId, db, ct);
        if (!access.Allowed) return access.Result!;
        var reminder = await db.AgendaReminders.SingleOrDefaultAsync(x =>
            x.Id == reminderId && x.CondominiumId == condominiumId, ct);
        if (reminder is null) return Results.NotFound();
        db.Remove(reminder); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<Access> Authorize(ClaimsPrincipal principal,
        Guid condominiumId, AppDbContext db, CancellationToken ct)
    {
        var raw = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(raw, out var userId)) return new(false, Guid.Empty,
            Results.Unauthorized());
        var active = await db.Set<ApplicationUser>().AnyAsync(x => x.Id == userId
            && x.IsActive, ct);
        if (!active) return new(false, userId, Results.Forbid());
        var platformAdmin = principal.IsInRole(DependencyInjection.PlatformAdminRole);
        var manager = await (from m in db.CondominiumMemberships
            join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
            where m.UserId == userId && m.CondominiumId == condominiumId
                && m.IsActive && m.EndedAt == null && r.Role == CondominiumRole.Manager
                && r.IsActive && r.RevokedAt == null select m.Id).AnyAsync(ct);
        return manager || platformAdmin ? new(true, userId, null)
            : new(false, userId, Results.Forbid());
    }

    public sealed record ReminderInput(string Title, string? Description, Guid? UnitId,
        string? RelatedThirdParty, DateTime StartsAtUtc, string RecurrenceType,
        bool NotifyByWhatsApp, bool NotifyByEmail, Guid[] RequestIds);
    public sealed record ReminderResponse(Guid Id, string Title, string? Description,
        Guid? UnitId, string? UnitIdentifier, string? Block, string? RelatedThirdParty,
        DateTime StartsAtUtc, DateTime? NextOccurrenceAtUtc, string TimeZoneId,
        string RecurrenceType, bool NotifyByWhatsApp, bool NotifyByEmail,
        bool IsActive, DateTime? CompletedAt, DateTime CreatedAt,
        int RequestCount, Guid[] RequestIds);
    public sealed record RequestOption(Guid Id, string Protocol, string Title,
        string ResidentName, string? UnitIdentifier, string Status,
        Guid? LinkedReminderId);
    private sealed record Access(bool Allowed, Guid UserId, IResult? Result);
}
