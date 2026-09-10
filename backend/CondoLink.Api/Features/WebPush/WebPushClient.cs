using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WebPush;

public interface IWebPushClient
{
    Task SendAsync(string endpoint, string p256dh, string auth, string payload,
        CancellationToken cancellationToken);
}

public sealed class StandardWebPushClient(
    PushServiceClient client,
    IOptions<WebPushOptions> options) : IWebPushClient
{
    public Task SendAsync(string endpoint, string p256dh, string auth,
        string payload, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var subscription = new PushSubscription { Endpoint = endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, p256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, auth);
        var authentication = new VapidAuthentication(
            settings.VapidPublicKey, settings.VapidPrivateKey)
        { Subject = settings.Subject };
        var message = new PushMessage(payload)
        {
            TimeToLive = 86400,
            Urgency = PushMessageUrgency.Normal
        };
        return client.RequestPushMessageDeliveryAsync(
            subscription, message, authentication, cancellationToken);
    }
}
