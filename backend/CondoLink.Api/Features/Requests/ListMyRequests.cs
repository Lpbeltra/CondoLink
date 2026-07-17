using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class ListMyRequests
{
    public static IEndpointRouteBuilder MapListMyRequests(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/requests/mine", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(authenticatedUserIdValue, out var authenticatedUserId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == authenticatedUserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var rows = await (
                from request in dbContext.Requests.AsNoTracking()
                join category in dbContext.Categories.AsNoTracking()
                    on request.CategoryId equals category.Id
                join unit in dbContext.Units.AsNoTracking()
                    on request.TargetUnitId equals unit.Id into targetUnits
                from unit in targetUnits.DefaultIfEmpty()
                where request.AuthorUserId == authenticatedUserId
                orderby request.CreatedAt descending, request.Id descending
                select new
                {
                    request.Id,
                    request.CondominiumId,
                    request.CategoryId,
                    CategoryName = category.Name,
                    TargetUnitId = unit == null ? (Guid?)null : unit.Id,
                    TargetUnitIdentifier = unit == null ? null : unit.Identifier,
                    TargetUnitBlock = unit == null ? null : dbContext.CondominiumBlocks.Where(block => block.Id == unit.BlockId).Select(block => block.Identifier).FirstOrDefault(),
                    request.Title,
                    request.Status,
                    request.Priority,
                    request.CreatedAt,
                    request.UpdatedAt,
                    request.ResolvedAt
                })
            .ToListAsync(cancellationToken);

        var response = rows
            .Select(request => new Response(
                request.Id,
                request.CondominiumId,
                new CategoryResponse(request.CategoryId, request.CategoryName),
                request.TargetUnitId.HasValue
                    ? new TargetUnitResponse(
                        request.TargetUnitId.Value,
                        request.TargetUnitIdentifier!,
                        request.TargetUnitBlock)
                    : null,
                request.Title,
                request.Status.ToString(),
                request.Priority.ToString(),
                request.CreatedAt,
                request.UpdatedAt,
                request.ResolvedAt))
            .ToArray();

        return Results.Ok(response);
    }

    public sealed record CategoryResponse(Guid Id, string Name);
    public sealed record TargetUnitResponse(Guid Id, string Identifier, string? Block);

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        CategoryResponse Category,
        TargetUnitResponse? TargetUnit,
        string Title,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? ResolvedAt);
}
