namespace CondoLink.Api.Features.TelegramAssistant;

public sealed class TelegramAssistantOptions
{
    public const string SectionName = "TelegramAssistant";
    public bool Enabled { get; set; }
    public string? BotToken { get; set; }
    public string? WebhookSecret { get; set; }
    public string? PublicBaseUrl { get; set; }
    public string? BotUsername { get; set; }
    public int PollingSeconds { get; set; } = 2;
    public int MaximumAttempts { get; set; } = 4;
    public long MaximumAudioBytes { get; set; } = 15 * 1024 * 1024;
    public int MaximumAudioDurationSeconds { get; set; } = 600;
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BotToken)
        && !string.IsNullOrWhiteSpace(WebhookSecret);
}
