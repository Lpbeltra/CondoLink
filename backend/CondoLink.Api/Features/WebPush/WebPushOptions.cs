namespace CondoLink.Api.Features.WebPush;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";
    public bool Enabled { get; set; }
    public string VapidPublicKey { get; set; } = "";
    public string VapidPrivateKey { get; set; } = "";
    public string Subject { get; set; } = "";

    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(VapidPublicKey)
        && !string.IsNullOrWhiteSpace(VapidPrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
