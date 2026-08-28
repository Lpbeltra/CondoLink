using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class ManagementPixEndpoint
{
    public static IEndpointRouteBuilder MapManagementPixEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/overwatch/management-users/{userId:guid}/pix", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(Guid userId, Request request,
        AppDbContext db, CancellationToken ct)
    {
        var isManagementUser = await db.CondominiumMemberships.AnyAsync(m => m.UserId == userId
            && db.CondominiumMembershipRoles.Any(r => r.CondominiumMembershipId == m.Id
                && (r.Role == CondominiumRole.Manager || r.Role == CondominiumRole.SubManager)), ct);
        if (!isManagementUser) return Results.NotFound();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        try { user.SetPix(request.Type, request.Key); }
        catch (ArgumentException exception) { return Results.BadRequest(new { message = exception.Message }); }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { user.PixKeyType, user.PixKey });
    }
    public sealed record Request(PixKeyType? Type, string? Key);
}
