using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Reports;

/// <summary>
/// Pure metric calculations over request data.
///
/// Kept free of EF Core and HTTP so the indicator rules are directly testable:
/// the endpoint projects rows, this decides what the numbers mean.
/// </summary>
public static class RequestMetrics
{
    /// <summary>A request row reduced to the fields the indicators need.</summary>
    public readonly record struct RequestSample(
        RequestStatus Status,
        RequestPriority Priority,
        DateTime CreatedAt,
        DateTime? ResolvedAt,
        DateTime? FirstManagerReplyAt,
        Guid CategoryId,
        string CategoryName);

    public static readonly RequestStatus[] OpenStatuses =
    [
        RequestStatus.Open,
        RequestStatus.InProgress,
        RequestStatus.WaitingForResident,
        RequestStatus.WaitingForThirdParty
    ];

    /// <summary>
    /// Average hours from creation to resolution, over resolved requests only.
    /// Returns null when nothing has been resolved yet — reporting 0 would read
    /// as "instant resolution" rather than "no data".
    /// </summary>
    public static double? AverageResolutionHours(IEnumerable<RequestSample> samples)
    {
        var durations = samples
            .Where(sample => sample.ResolvedAt.HasValue)
            .Select(sample => (sample.ResolvedAt!.Value - sample.CreatedAt).TotalHours)
            // Guard against clock skew / bad data producing negative durations.
            .Where(hours => hours >= 0)
            .ToArray();

        return durations.Length == 0 ? null : Round(durations.Average());
    }

    /// <summary>
    /// Average hours from creation to the first message written by someone other
    /// than the author. Null when no request has been answered yet.
    /// </summary>
    public static double? AverageFirstResponseHours(IEnumerable<RequestSample> samples)
    {
        var durations = samples
            .Where(sample => sample.FirstManagerReplyAt.HasValue)
            .Select(sample => (sample.FirstManagerReplyAt!.Value - sample.CreatedAt).TotalHours)
            .Where(hours => hours >= 0)
            .ToArray();

        return durations.Length == 0 ? null : Round(durations.Average());
    }

    /// <summary>Requests still needing attention (not resolved, not cancelled).</summary>
    public static int CountOpen(IEnumerable<RequestSample> samples) =>
        samples.Count(sample => OpenStatuses.Contains(sample.Status));

    /// <summary>
    /// Open requests that have never received a reply — the queue that is
    /// actually at risk of being forgotten.
    /// </summary>
    public static int CountAwaitingFirstResponse(IEnumerable<RequestSample> samples) =>
        samples.Count(sample =>
            OpenStatuses.Contains(sample.Status)
            && !sample.FirstManagerReplyAt.HasValue);

    /// <summary>
    /// Share of non-cancelled requests that reached Resolved, as a percentage.
    /// Cancelled requests are excluded from the denominator: they were withdrawn,
    /// not failed. Null when there is nothing to measure.
    /// </summary>
    public static double? ResolutionRatePercent(IEnumerable<RequestSample> samples)
    {
        var considered = samples
            .Where(sample => sample.Status != RequestStatus.Cancelled)
            .ToArray();

        if (considered.Length == 0) return null;

        var resolved = considered.Count(sample => sample.Status == RequestStatus.Resolved);
        return Round(resolved * 100.0 / considered.Length);
    }

    /// <summary>
    /// Volume per category, busiest first, ties broken alphabetically so the
    /// output is stable across calls.
    /// </summary>
    public static IReadOnlyList<CategoryVolume> VolumeByCategory(
        IEnumerable<RequestSample> samples)
        => samples
            .GroupBy(sample => (sample.CategoryId, sample.CategoryName))
            .Select(group => new CategoryVolume(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Count(),
                group.Count(sample => OpenStatuses.Contains(sample.Status)),
                AverageResolutionHours(group)))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Counts per priority, for the whole set.</summary>
    public static IReadOnlyList<PriorityVolume> VolumeByPriority(
        IEnumerable<RequestSample> samples)
    {
        var materialized = samples.ToArray();

        return Enum.GetValues<RequestPriority>()
            .Select(priority => new PriorityVolume(
                priority.ToString(),
                materialized.Count(sample => sample.Priority == priority),
                materialized.Count(sample =>
                    sample.Priority == priority && OpenStatuses.Contains(sample.Status))))
            .ToArray();
    }

    /// <summary>
    /// Requests created per day across the window, including days with zero so
    /// a chart does not silently collapse gaps.
    /// </summary>
    public static IReadOnlyList<DailyVolume> CreatedPerDay(
        IEnumerable<RequestSample> samples,
        DateOnly from,
        DateOnly to)
    {
        if (to < from) return [];

        var grouped = samples
            .GroupBy(sample => DateOnly.FromDateTime(sample.CreatedAt))
            .ToDictionary(group => group.Key, group => group.Count());

        var days = new List<DailyVolume>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            days.Add(new DailyVolume(day, grouped.GetValueOrDefault(day)));
        }

        return days;
    }

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public sealed record CategoryVolume(
        Guid CategoryId,
        string Name,
        int Total,
        int Open,
        double? AverageResolutionHours);

    public sealed record PriorityVolume(string Priority, int Total, int Open);

    public sealed record DailyVolume(DateOnly Day, int Created);
}
