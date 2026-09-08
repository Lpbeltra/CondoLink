using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class PrepareProviderContactMessage
{
    public static IEndpointRouteBuilder MapPrepareProviderContactMessage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:guid}/provider-contact-message", HandleAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> HandleAsync(Guid requestId, ClaimsPrincipal principal, AppDbContext db, IProviderContactAiService ai, CancellationToken ct)
    {
        var userValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userValue, out var userId)) return Results.Unauthorized();
        var user = await db.Set<ApplicationUser>().AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.IsActive, x.FullName }).SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive) return Results.Forbid();
        var row = await (from request in db.Requests.AsNoTracking()
                         join category in db.Categories.AsNoTracking() on request.CategoryId equals category.Id
                         join condo in db.Condominiums.AsNoTracking() on request.CondominiumId equals condo.Id
                         join provider in db.ServiceProviders.AsNoTracking() on request.ServiceProviderId equals provider.Id
                         where request.Id == requestId
                         select new { request, Category = category.Name, Condominium = condo.Name, Provider = provider }).SingleOrDefaultAsync(ct);
        if (row is null) return Results.NotFound(new { error = "Atendimento ou prestador não encontrado." });
        var membership = await (from m in db.CondominiumMemberships.AsNoTracking()
                                join role in db.CondominiumMembershipRoles.AsNoTracking() on m.Id equals role.CondominiumMembershipId
                                where m.UserId == userId && m.CondominiumId == row.request.CondominiumId && m.IsActive && m.EndedAt == null && role.IsActive && role.RevokedAt == null && (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                                select new { role.Role, MembershipId = m.Id }).FirstOrDefaultAsync(ct);
        if (membership is null) return Results.Forbid();
        if (membership.Role == CondominiumRole.SubManager && !await SubManagerAccess.HasAsync(db, userId, row.request.CondominiumId, SubManagerModule.Attendance, ct)) return Results.Forbid();
        var linked = await db.ServiceProviderUserLinks.AnyAsync(x => x.ServiceProviderId == row.Provider.Id && x.UserId == userId, ct) || await db.ServiceProviderCondominiumLinks.AnyAsync(x => x.ServiceProviderId == row.Provider.Id && x.CondominiumId == row.request.CondominiumId, ct);
        if (!linked) return Results.Forbid();
        var messages = await db.RequestMessages.AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.CreatedAt).Select(x => x.Content).ToArrayAsync(ct);
        var context = $"Condomínio: {row.Condominium}\nAtendimento: {row.request.Title}\nDescrição: {row.request.Description}\nCategoria: {row.Category}\nStatus: {row.request.Status}\nPrioridade: {row.request.Priority}\nPrestador: {row.Provider.Name}\nEspecialidade: {row.Provider.Specialty}" + (messages.Length == 0 ? "" : $"\nRelatos relevantes:\n{string.Join("\n", messages.TakeLast(5))}");
        var result = await ai.PrepareAsync(context, ct);
        return result.Succeeded ? Results.Ok(new { message = result.Message }) : Results.Problem(result.Error, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
