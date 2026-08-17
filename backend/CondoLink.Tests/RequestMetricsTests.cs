using CondoLink.Api.Features.Reports;
using CondoLink.Domain.Enums;
using static CondoLink.Api.Features.Reports.RequestMetrics;

namespace CondoLink.Tests;

public sealed class RequestMetricsTests
{
    private static readonly DateTime Base = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    private static RequestSample Sample(
        RequestStatus status = RequestStatus.Open,
        RequestPriority priority = RequestPriority.Normal,
        double createdOffsetHours = 0,
        double? resolvedAfterHours = null,
        double? firstReplyAfterHours = null,
        string categoryName = "Manutenção",
        Guid? categoryId = null)
    {
        var createdAt = Base.AddHours(createdOffsetHours);
        return new RequestSample(
            status,
            priority,
            createdAt,
            resolvedAfterHours.HasValue ? createdAt.AddHours(resolvedAfterHours.Value) : null,
            firstReplyAfterHours.HasValue ? createdAt.AddHours(firstReplyAfterHours.Value) : null,
            categoryId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            categoryName);
    }

    // ---- resolution time ----

    [Fact]
    public void Average_resolution_is_null_when_nothing_was_resolved()
    {
        // Null, not zero: zero would read as "resolved instantly".
        Assert.Null(AverageResolutionHours([Sample(), Sample(RequestStatus.InProgress)]));
    }

    [Fact]
    public void Average_resolution_considers_only_resolved_requests()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Resolved, resolvedAfterHours: 2),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 4),
            Sample(RequestStatus.Open),
        };

        Assert.Equal(3, AverageResolutionHours(samples));
    }

    [Fact]
    public void Average_resolution_ignores_negative_durations_from_bad_data()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Resolved, resolvedAfterHours: -5),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 6),
        };

        Assert.Equal(6, AverageResolutionHours(samples));
    }

    [Fact]
    public void Average_resolution_rounds_to_one_decimal()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Resolved, resolvedAfterHours: 1),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 2),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 2),
        };

        Assert.Equal(1.7, AverageResolutionHours(samples));
    }

    // ---- first response ----

    [Fact]
    public void Average_first_response_is_null_before_any_reply()
    {
        Assert.Null(AverageFirstResponseHours([Sample(), Sample()]));
    }

    [Fact]
    public void Average_first_response_uses_the_first_reply_timestamp()
    {
        var samples = new[]
        {
            Sample(firstReplyAfterHours: 1),
            Sample(firstReplyAfterHours: 3),
            Sample(),
        };

        Assert.Equal(2, AverageFirstResponseHours(samples));
    }

    // ---- open / awaiting ----

    [Fact]
    public void Open_count_excludes_resolved_and_cancelled()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Open),
            Sample(RequestStatus.InProgress),
            Sample(RequestStatus.WaitingForResident),
            Sample(RequestStatus.WaitingForThirdParty),
            Sample(RequestStatus.WaitingForResidentClosure),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 1),
            Sample(RequestStatus.Cancelled),
        };

        Assert.Equal(5, CountOpen(samples));
    }

    [Fact]
    public void Awaiting_first_response_counts_only_unanswered_open_requests()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Open),                              // counts
            Sample(RequestStatus.InProgress),                        // counts
            Sample(RequestStatus.WaitingForResidentClosure),         // counts
            Sample(RequestStatus.Open, firstReplyAfterHours: 2),     // answered
            Sample(RequestStatus.Resolved, resolvedAfterHours: 3),   // not open
            Sample(RequestStatus.Cancelled),                         // not open
        };

        Assert.Equal(3, CountAwaitingFirstResponse(samples));
    }

    // ---- resolution rate ----

    [Fact]
    public void Resolution_rate_excludes_cancelled_from_the_denominator()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Resolved, resolvedAfterHours: 1),
            Sample(RequestStatus.Open),
            Sample(RequestStatus.Cancelled),
        };

        // 1 resolved of 2 considered => 50%, cancelled ignored.
        Assert.Equal(50, ResolutionRatePercent(samples));
    }

    [Fact]
    public void Resolution_rate_is_null_when_every_request_was_cancelled()
    {
        Assert.Null(ResolutionRatePercent([Sample(RequestStatus.Cancelled)]));
    }

    [Fact]
    public void Resolution_rate_is_null_for_an_empty_set()
    {
        Assert.Null(ResolutionRatePercent([]));
    }

    [Fact]
    public void Resolution_rate_reaches_one_hundred_percent()
    {
        var samples = new[]
        {
            Sample(RequestStatus.Resolved, resolvedAfterHours: 1),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 2),
        };

        Assert.Equal(100, ResolutionRatePercent(samples));
    }

    // ---- category breakdown ----

    [Fact]
    public void Category_volume_is_ordered_by_total_descending()
    {
        var busy = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var quiet = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var samples = new[]
        {
            Sample(categoryId: busy, categoryName: "Portaria"),
            Sample(categoryId: busy, categoryName: "Portaria"),
            Sample(categoryId: quiet, categoryName: "Jardim"),
        };

        var result = VolumeByCategory(samples);

        Assert.Equal(2, result.Count);
        Assert.Equal("Portaria", result[0].Name);
        Assert.Equal(2, result[0].Total);
        Assert.Equal("Jardim", result[1].Name);
    }

    [Fact]
    public void Category_volume_breaks_ties_alphabetically_for_stable_output()
    {
        var a = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var b = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var samples = new[]
        {
            Sample(categoryId: b, categoryName: "Zeladoria"),
            Sample(categoryId: a, categoryName: "Академия"),
        };

        var result = VolumeByCategory(samples);

        Assert.Equal(2, result.Count);
        Assert.True(
            string.Compare(result[0].Name, result[1].Name, StringComparison.OrdinalIgnoreCase) < 0,
            "Equal totals must be ordered by name so the report is deterministic.");
    }

    [Fact]
    public void Category_volume_reports_open_count_and_average_per_category()
    {
        var id = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var samples = new[]
        {
            Sample(RequestStatus.Open, categoryId: id, categoryName: "Elevador"),
            Sample(RequestStatus.Resolved, resolvedAfterHours: 10, categoryId: id, categoryName: "Elevador"),
        };

        var result = VolumeByCategory(samples).Single();

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Open);
        Assert.Equal(10, result.AverageResolutionHours);
    }

    [Fact]
    public void Category_volume_is_empty_for_no_data()
    {
        Assert.Empty(VolumeByCategory([]));
    }

    // ---- priority breakdown ----

    [Fact]
    public void Priority_volume_lists_every_priority_even_at_zero()
    {
        var result = VolumeByPriority([Sample(priority: RequestPriority.High)]);

        Assert.Equal(Enum.GetValues<RequestPriority>().Length, result.Count);
        Assert.Equal(1, result.Single(item => item.Priority == "High").Total);
        Assert.All(
            result.Where(item => item.Priority != "High"),
            item => Assert.Equal(0, item.Total));
    }

    // ---- daily series ----

    [Fact]
    public void Created_per_day_fills_days_with_no_activity()
    {
        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 3);
        var samples = new[] { Sample(createdOffsetHours: 0), Sample(createdOffsetHours: 48) };

        var result = CreatedPerDay(samples, from, to);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Created);
        Assert.Equal(0, result[1].Created); // the gap must be present, not skipped
        Assert.Equal(1, result[2].Created);
    }

    [Fact]
    public void Created_per_day_returns_a_single_day_window()
    {
        var day = new DateOnly(2026, 3, 1);
        var result = CreatedPerDay([Sample()], day, day);

        Assert.Single(result);
        Assert.Equal(1, result[0].Created);
    }

    [Fact]
    public void Created_per_day_rejects_an_inverted_window()
    {
        var result = CreatedPerDay([Sample()], new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 1));

        Assert.Empty(result);
    }
}
