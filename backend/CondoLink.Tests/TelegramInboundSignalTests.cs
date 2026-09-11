using CondoLink.Api.Features.TelegramAssistant;

namespace CondoLink.Tests;

public sealed class TelegramInboundSignalTests
{
    [Fact]
    public async Task Wake_releases_waiter_without_waiting_for_poll_interval()
    {
        var signal = new TelegramInboundSignal();
        var wait = signal.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        signal.Wake();
        await wait.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Multiple_wakes_are_coalesced()
    {
        var signal = new TelegramInboundSignal();
        signal.Wake(); signal.Wake();
        await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
    }
}
