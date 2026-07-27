using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Requests;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Reports;

/// <summary>
/// Management indicators over the requests the caller manages.
/// Backlog item: "Dashboard e relatórios — indicadores avançados".
/// </summary>
public static class GetRequestReport
{
    /// <summary>Default reporting window when the caller does not specify one.</summary>
    private const int DefaultWindowDays = 30;

    /// <summary>Bounded so a single call cannot scan an unbounded history.</summary>
    private const int MaximumWindowDays = 365;

    public static IEndpointRouteBuilder MapGetRequestReport(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/management/reports/requests", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithSummary("Request indicators for the managed condominiums");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        int? days,
        Guid? condominiumId,
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

        var windowDays = days ?? DefaultWindowDays;

        if (windowDays < 1 || windowDays > MaximumWindowDays)
        {
            return Results.BadRequest(new
            {
                error = $"Days must be between 1 and {MaximumWindowDays}."
            });
        }

        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-(windowDays - 1));

        // Reuses the manager-scoping helper: a caller can never see a
        // condominium they do not manage, and an explicit condominiumId can only
        // narrow that set, never widen it.
        var scoped = ListCondominiumRequests.AuthorizedRequests(dbContext, authenticatedUserId);

        if (condominiumId.HasValue)
        {
            scoped = scoped.Where(request => request.CondominiumId == condominiumId.Value);
        }

        var windowed = scoped.Where(request => request.CreatedAt >= from);

        // First reply = earliest message from anyone other than the author.
        var rows = await windowed
            .Select(request => new
            {
                request.Status,
                request.Priority,
                request.CreatedAt,
                request.ResolvedAt,
                request.CategoryId,
                CategoryName = dbContext.Categories
                    .Where(category => category.Id == request.CategoryId)
                    .Select(category => category.Name)
                    .FirstOrDefault(),
                FirstManagerReplyAt = dbContext.RequestMessages
                    .Where(message =>
                        message.RequestId == request.Id
                        && message.AuthorUserId != request.AuthorUserId)
                    .Min(message => (DateTime?)message.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var samples = rows
            .Select(row => new RequestMetrics.RequestSample(
                row.Status,
                row.Priority,
                row.CreatedAt,
                row.ResolvedAt,
                row.FirstManagerReplyAt,
                row.CategoryId,
                row.CategoryName ?? "Sem categoria"))
            .ToArray();

        var response = new Response(
            new PeriodResponse(
                DateOnly.FromDateTime(from),
                DateOnly.FromDateTime(now),
                windowDays),
            new SummaryResponse(
                samples.Length,
                RequestMetrics.CountOpen(samples),
                RequestMetrics.CountAwaitingFirstResponse(samples),
                RequestMetrics.AverageFirstResponseHours(samples),
                RequestMetrics.AverageResolutionHours(samples),
                RequestMetrics.ResolutionRatePercent(samples)),
            RequestMetrics.VolumeByCategory(samples),
            RequestMetrics.VolumeByPriority(samples),
            RequestMetrics.CreatedPerDay(
                samples,
                DateOnly.FromDateTime(from),
                DateOnly.FromDateTime(now)));

        return Results.Ok(response);
    }

    public sealed record PeriodResponse(DateOnly From, DateOnly To, int Days);

    public sealed record SummaryResponse(
        int Total,
        int Open,
        int AwaitingFirstResponse,
        double? AverageFirstResponseHours,
        double? AverageResolutionHours,
        double? ResolutionRatePercent);

    public sealed record Response(
        PeriodResponse Period,
        SummaryResponse Summary,
        IReadOnlyList<RequestMetrics.CategoryVolume> ByCategory,
        IReadOnlyList<RequestMetrics.PriorityVolume> ByPriority,
        IReadOnlyList<RequestMetrics.DailyVolume> CreatedPerDay);
}
