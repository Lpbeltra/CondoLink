using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class RequestServiceProviderHistoryTests
{
    [Fact]
    public void Keeps_link_change_and_removal_snapshots()
    {
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var linked = new RequestServiceProviderHistory(requestId, "Linked", null, null, "César", "Hidráulica", actorId, DateTime.UtcNow);
        var changed = new RequestServiceProviderHistory(requestId, "Changed", "César", "Hidráulica", "Ana", "Elétrica", actorId, DateTime.UtcNow);
        var removed = new RequestServiceProviderHistory(requestId, "Removed", "Ana", "Elétrica", null, null, actorId, DateTime.UtcNow);

        Assert.Equal("Linked", linked.EventType);
        Assert.Equal("César", linked.ProviderName);
        Assert.Equal("César", changed.PreviousName);
        Assert.Equal("Ana", removed.PreviousName);
        Assert.Null(removed.ProviderName);
    }

    [Fact]
    public void Unlink_preserves_waiting_for_third_party_status_and_history_sequence()
    {
        var now = DateTime.UtcNow;
        var request = new Request(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "Vazamento", "Descrição");
        request.ChangeStatus(RequestStatus.WaitingForThirdParty, now);
        var providerId = Guid.NewGuid();
        request.SetServiceProvider(providerId, now.AddMinutes(1));
        var linked = new RequestServiceProviderHistory(request.Id, "Linked", null, null, "César", "Hidráulica", Guid.NewGuid(), now.AddMinutes(1));
        request.SetServiceProvider(null, now.AddMinutes(2));
        var removed = new RequestServiceProviderHistory(request.Id, "Removed", "César", "Hidráulica", null, null, Guid.NewGuid(), now.AddMinutes(2));
        request.SetServiceProvider(Guid.NewGuid(), now.AddMinutes(3));
        var relinked = new RequestServiceProviderHistory(request.Id, "Linked", null, null, "João", "Elétrica", Guid.NewGuid(), now.AddMinutes(3));

        Assert.Equal(RequestStatus.WaitingForThirdParty, request.Status);
        Assert.NotNull(request.ServiceProviderId);
        Assert.Collection([linked, removed, relinked],
            item => Assert.Equal("Linked", item.EventType),
            item => Assert.Equal("Removed", item.EventType),
            item => Assert.Equal("Linked", item.EventType));
        Assert.Equal("César", removed.PreviousName);
    }
}
