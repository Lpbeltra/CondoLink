using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Users;

public static class GetCurrentUser
{
    public static IEndpointRouteBuilder MapGetCurrentUser(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new Response(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!user.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(user);
    }

    public sealed record Response(
        Guid Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
