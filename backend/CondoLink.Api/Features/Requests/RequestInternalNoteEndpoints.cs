using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class RequestInternalNoteEndpoints
{
    public static IEndpointRouteBuilder MapRequestInternalNotes(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/management/requests/{requestId:guid}/internal-notes")
            .RequireAuthorization().AddEndpointFilter(AuthorizeAsync);
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{noteId:guid}", EditAsync);
        group.MapDelete("/{noteId:guid}", DeleteAsync);
        return endpoints;
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.TryParse(
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    private static async ValueTask<object?> AuthorizeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var ct = http.RequestAborted;
        var userId = UserId(http.User);
        if (userId == Guid.Empty) return Results.Unauthorized();
        if (!await db.Set<ApplicationUser>().AnyAsync(x => x.Id == userId && x.IsActive, ct))
            return Results.Forbid();
        var requestId = Guid.Parse(http.Request.RouteValues["requestId"]!.ToString()!);
        var condominiumId = await db.Requests.Where(x => x.Id == requestId)
            .Select(x => (Guid?)x.CondominiumId).SingleOrDefaultAsync(ct);
        if (condominiumId is null) return Results.NotFound();
        if (!await SubManagerAccess.HasAsync(db, userId, condominiumId.Value, SubManagerModule.Attendance, ct))
            return Results.Forbid();
        return await next(context);
    }

    private static IQueryable<Response> Responses(AppDbContext db, Guid requestId) =>
        from note in db.RequestInternalNotes.AsNoTracking()
        join author in db.Set<ApplicationUser>().AsNoTracking() on note.AuthorUserId equals author.Id
        where note.RequestId == requestId
        orderby note.CreatedAt descending, note.Id descending
        select new Response(note.Id, note.Content, new AuthorResponse(author.Id, author.FullName),
            note.CreatedAt, note.UpdatedAt);

    private static Task<Response> ResponseAsync(AppDbContext db, Guid requestId,
        Guid noteId, CancellationToken ct) =>
        (from note in db.RequestInternalNotes.AsNoTracking()
         join author in db.Set<ApplicationUser>().AsNoTracking() on note.AuthorUserId equals author.Id
         where note.RequestId == requestId && note.Id == noteId
         select new Response(note.Id, note.Content,
             new AuthorResponse(author.Id, author.FullName), note.CreatedAt, note.UpdatedAt))
        .SingleAsync(ct);

    private static async Task<IResult> ListAsync(Guid requestId, AppDbContext db, CancellationToken ct) =>
        Results.Ok(await Responses(db, requestId).ToArrayAsync(ct));

    private static IResult? Validate(Input input) =>
        string.IsNullOrWhiteSpace(input.Content) || input.Content.Trim().Length > RequestInternalNote.MaximumContentLength
            ? Results.BadRequest(new { error = "A nota deve conter entre 1 e 3000 caracteres." }) : null;

    private static async Task<IResult> CreateAsync(Guid requestId, Input input,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (Validate(input) is { } error) return error;
        var note = new RequestInternalNote(requestId, UserId(principal), input.Content!);
        db.RequestInternalNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/management/requests/{requestId}/internal-notes/{note.Id}",
            await ResponseAsync(db, requestId, note.Id, ct));
    }

    private static async Task<IResult> EditAsync(Guid requestId, Guid noteId, Input input,
        AppDbContext db, CancellationToken ct)
    {
        if (Validate(input) is { } error) return error;
        var note = await db.RequestInternalNotes.SingleOrDefaultAsync(x => x.RequestId == requestId && x.Id == noteId, ct);
        if (note is null) return Results.NotFound();
        note.Edit(input.Content!);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ResponseAsync(db, requestId, noteId, ct));
    }

    private static async Task<IResult> DeleteAsync(Guid requestId, Guid noteId, AppDbContext db, CancellationToken ct)
    {
        var note = await db.RequestInternalNotes.SingleOrDefaultAsync(x => x.RequestId == requestId && x.Id == noteId, ct);
        if (note is null) return Results.NotFound();
        db.RequestInternalNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public sealed record Input(string? Content);
    public sealed record AuthorResponse(Guid Id, string FullName);
    public sealed record Response(Guid Id, string Content, AuthorResponse Author, DateTime CreatedAt, DateTime? UpdatedAt);
}
