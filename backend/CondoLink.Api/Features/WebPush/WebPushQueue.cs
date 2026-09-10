using System.Threading.Channels;

namespace CondoLink.Api.Features.WebPush;

public interface IWebPushQueue
{
    bool TryEnqueue(Guid notificationId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class WebPushQueue : IWebPushQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public bool TryEnqueue(Guid notificationId) => _channel.Writer.TryWrite(notificationId);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
