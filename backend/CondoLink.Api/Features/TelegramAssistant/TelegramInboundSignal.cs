namespace CondoLink.Api.Features.TelegramAssistant;

/// <summary>Best-effort local wake-up; durable polling remains recovery path across instances.</summary>
public sealed class TelegramInboundSignal
{
    private readonly SemaphoreSlim signal = new(0, 1);
    public void Wake()
    {
        try { signal.Release(); }
        catch (SemaphoreFullException) { }
    }
    public Task WaitAsync(TimeSpan fallback, CancellationToken ct) => signal.WaitAsync(fallback, ct);
}
