using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Agenda;

public static class RequestAgendaLinkService
{
    public static Task<int> UnlinkIfTerminalAsync(AppDbContext db, Guid requestId,
        RequestStatus status, CancellationToken ct) =>
        status is RequestStatus.Resolved or RequestStatus.Cancelled
            ? db.AgendaReminderRequests.Where(x => x.RequestId == requestId)
                .ExecuteDeleteAsync(ct)
            : Task.FromResult(0);
}
