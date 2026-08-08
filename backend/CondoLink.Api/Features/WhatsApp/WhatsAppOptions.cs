namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";
    public bool Enabled { get; set; }
    public string ApiVersion { get; set; } = "v23.0";
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? AccessToken { get; set; }
    public string? VerifyToken { get; set; }
    public string? AppSecret { get; set; }
    public int SessionExpirationMinutes { get; set; } = 30;
    public bool OutboundWorkerEnabled { get; set; }
    public int OutboundBatchSize { get; set; } = 10;
    public int OutboundPollingSeconds { get; set; } = 10;
    public int OutboundMaxAttempts { get; set; } = 5;
    public int OutboundInitialRetrySeconds { get; set; } = 30;
    public string PortalUrl { get; set; } = "https://www.comvy.com.br";
    public WhatsAppTemplateOptions Templates { get; set; } = new();
}

public sealed class WhatsAppTemplateOptions
{
    public WhatsAppTemplateDefinition AdministrationMessage { get; set; } = new();
    public WhatsAppTemplateDefinition InformationRequested { get; set; } = new();
    public WhatsAppTemplateDefinition StatusChanged { get; set; } = new();
    public WhatsAppTemplateDefinition Resolved { get; set; } = new();
    public WhatsAppTemplateDefinition Cancelled { get; set; } = new();
    public WhatsAppTemplateDefinition Reopened { get; set; } = new();
}

public sealed class WhatsAppTemplateDefinition
{
    public string? Name { get; set; }
    public string? Language { get; set; }
    public string? BodyParameterName { get; set; }
}
