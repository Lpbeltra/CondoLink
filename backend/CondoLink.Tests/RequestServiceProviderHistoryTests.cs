using CondoLink.Domain.Entities;

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
}
