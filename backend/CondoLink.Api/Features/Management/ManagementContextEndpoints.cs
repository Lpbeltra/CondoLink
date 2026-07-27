using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Management;

public static class ManagementContextEndpoints
{
    public static IEndpointRouteBuilder MapManagementContext(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/management/context", HandleGetAsync)
            .RequireAuthorization();
        endpoints.MapPut("/management/context", HandlePutAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleGetAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(principal, dbContext, cancellationToken);
        if (user is null)
        {
            return AuthenticationFailed();
        }

        var context = await ManagementContextReconciler.ReconcileAsync(
            user, dbContext, cancellationToken);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(context);
    }

    private static async Task<IResult> HandlePutAsync(
        ManagementContextRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(principal, dbContext, cancellationToken);
        if (user is null)
        {
            return AuthenticationFailed();
        }

        var available = await ManagementContextReconciler
            .GetAvailableCondominiumsAsync(
                user.Id, dbContext, cancellationToken);
        if (request.CondominiumId is Guid requestedId
            && requestedId != Guid.Empty
            && !available.Any(item => item.Id == requestedId))
        {
            return Results.Forbid();
        }

        var context = await ManagementContextReconciler.SelectAsync(
            user, request.CondominiumId, dbContext, cancellationToken);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(context);
    }

    private static async Task<ApplicationUser?> GetActiveUserAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            return null;
        }

        return await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.IsActive,
            cancellationToken);
    }

    private static IResult AuthenticationFailed() =>
        Results.Json(
            new { error = "Authenticated user was not found or is inactive." },
            statusCode: StatusCodes.Status401Unauthorized);

    public sealed record ManagementContextRequest(Guid? CondominiumId);
}
