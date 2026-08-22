using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Common;

public static class PagedResult
{
    public static (int Page, int PageSize) Normalize(
        int? page, int? pageSize, int defaultPageSize, int maximumPageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? defaultPageSize, 1, maximumPageSize));
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public static async Task<PagedResult<T>> CreateAsync(
        IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}
