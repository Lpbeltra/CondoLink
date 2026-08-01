using CondoLink.Domain.Enums;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

public sealed class RequestStatusTransitionTests
{
    private static DomainRequest NewRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "Titulo", "Descricao");

    [Fact]
    public void New_requests_start_in_progress() =>
        Assert.Equal(RequestStatus.InProgress, NewRequest().Status);

    [Fact]
    public void Cancelling_a_request_after_it_was_reopened_from_resolved_keeps_history_consistent()
    {
        var request = NewRequest();
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        request.ChangeStatus(RequestStatus.Resolved, t0);
        Assert.Equal(t0, request.ResolvedAt);

        // Reopen -> ResolvedAt must be cleared.
        request.ChangeStatus(RequestStatus.Open, t0.AddHours(1));
        Assert.Null(request.ResolvedAt);
    }

    [Fact]
    public void Cancelled_request_can_be_reopened_but_priority_change_while_cancelled_is_blocked()
    {
        var request = NewRequest();
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        request.ChangeStatus(RequestStatus.Cancelled, t0);

        Assert.Throws<InvalidOperationException>(
            () => request.ChangePriority(RequestPriority.High, t0.AddMinutes(1)));

        request.ChangeStatus(RequestStatus.Open, t0.AddMinutes(2));
        request.ChangePriority(RequestPriority.High, t0.AddMinutes(3));
        Assert.Equal(RequestPriority.High, request.Priority);
    }

    [Fact]
    public void Terminal_transition_matrix_matches_documented_workflow()
    {
        // Resolved should be reachable from every active state.
        foreach (var from in new[]
                 {
                     RequestStatus.Open,
                     RequestStatus.InProgress,
                     RequestStatus.WaitingForResident,
                     RequestStatus.WaitingForThirdParty
                 })
        {
            var request = NewRequest();
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            if (from == RequestStatus.Open)
            {
                request.ChangeStatus(RequestStatus.Resolved, t0);
                request.ChangeStatus(RequestStatus.Open, t0.AddMinutes(1));
            }
            else if (from != RequestStatus.InProgress)
            {
                request.ChangeStatus(from, t0.AddMinutes(1));
            }

            request.ChangeStatus(RequestStatus.Resolved, t0.AddMinutes(3));
            Assert.Equal(RequestStatus.Resolved, request.Status);
        }
    }
}
