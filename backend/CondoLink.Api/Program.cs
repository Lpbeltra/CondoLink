using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Blocks;
using CondoLink.Api.Features.Categories;
using CondoLink.Api.Features.CondominiumMemberRoles;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.Condominiums;
using CondoLink.Api.Features.CondominiumSetup;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.Reports;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.UnitMemberships;
using CondoLink.Api.Features.Units;
using CondoLink.Api.Features.Users;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.OpenApi;
using CondoLink.Api;
using CondoLink.Api.Features.Overwatch;
using CondoLink.Api.Features.Overwatch.Managers;
using Microsoft.EntityFrameworkCore;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.OperationalMessages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddComvyDataProtection(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddScoped<CondominiumMembershipService>();
builder.Services.AddScoped<CondoLink.Api.Features.Categories.RequestCategoryResolver>();
builder.Services.AddScoped<ManagerOnboardingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<OperationalMessageTemplateService>();
builder.Services.AddScoped<RequestAiAnalysisRefresher>();
builder.Services.AddScoped<ResidentReplyService>();
builder.Services.AddScoped<RequestClosureService>();
builder.Services.AddSingleton<OperationalTelemetry>();
builder.Services.AddSingleton<ApiRequestMetrics>();
builder.Services.Configure<CondominiumAssistantOptions>(
    builder.Configuration.GetSection(CondominiumAssistantOptions.SectionName));
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>((services, client) =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "AssistantEmbedding"));
builder.Services.AddScoped<CondominiumDocumentProcessor>();
builder.Services.AddHttpClient<CondominiumAssistantService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "AssistantChat"));
builder.Services.AddScoped<WhatsAppNotificationDispatcher>();
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.AddScoped<WhatsAppConversationService>();
builder.Services.AddScoped<AdministrativeResidentRegistrationService>();
builder.Services.AddScoped<AdministrativeResidentLookupService>();
builder.Services.AddScoped<AdministrativeUnitResolver>();
builder.Services.AddScoped<AdministrativeResidentMembershipMutationService>();
builder.Services.Configure<RequestDraftAiOptions>(
    builder.Configuration.GetSection(RequestDraftAiOptions.SectionName));
builder.Services.AddHttpClient<IRequestDraftAiService, RequestDraftAiService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "RequestDraft"));
builder.Services.AddHttpClient<IAdministrativeResidentExtractionService,
    AdministrativeResidentExtractionService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "ResidentExtraction"));
builder.Services.AddHttpClient<IAdministrativeResidentLookupExtractionService,
    AdministrativeResidentLookupExtractionService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "ResidentLookup"));
builder.Services.AddHttpClient<IAdministrativeResidentMutationExtractionService,
    AdministrativeResidentMutationExtractionService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "ResidentMutation"));
builder.Services.AddHttpClient<IResidentReplyAiService, ResidentReplyAiService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "RequestAnalysis"));
builder.Services.Configure<RequestDraftAiAudioOptions>(
    builder.Configuration.GetSection(RequestDraftAiAudioOptions.SectionName));
builder.Services.AddHttpClient<IWhatsAppAudioTranscriptionService,
    OpenAiAudioTranscriptionService>((services, client) =>
{
    var settings = services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RequestDraftAiAudioOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
}).AddHttpMessageHandler(sp => new OpenAiTelemetryHandler(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), "AudioTranscription"));
builder.Services.AddScoped<AuthenticationSessionService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<FirstAccessOptions>(builder.Configuration.GetSection(FirstAccessOptions.SectionName));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<FirstAccessService>();
builder.Services.AddScoped<FirstAccessWhatsAppInvitationService>();
builder.Services.AddSingleton<IFirstAccessWhatsAppPayloadProtector,
    FirstAccessWhatsAppPayloadProtector>();
builder.Services.AddSingleton<IPhoneVerificationMessageProtector,
    PhoneVerificationMessageProtector>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<WhatsAppOutboundWorker>();
builder.Services.AddHostedService<WhatsAppConversationInactivityWorker>();
builder.Services.AddHostedService<RequestClosureWorker>();
builder.Services.AddHostedService<OperationalRetentionWorker>();
builder.Services.AddHttpClient<IWhatsAppClient, MetaWhatsAppClient>(client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type =>
        type.FullName?.Replace("+", ".") ?? type.Name);

    options.AddSecurityDefinition(
        "bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Informe o token JWT."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearer", document)] = []
        });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<LocalFileStorage>();
builder.Services.AddSingleton<ICondominiumDocumentStorage>(services => services.GetRequiredService<LocalFileStorage>());

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

var whatsappOptions = app.Services
    .GetRequiredService<
        Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>()
    .Value;

app.Logger.LogInformation(
    "WhatsApp configuration loaded. Enabled: {Enabled}; " +
    "VerifyTokenConfigured: {VerifyTokenConfigured}; " +
    "PhoneNumberIdConfigured: {PhoneNumberIdConfigured}; " +
    "AppSecretConfigured: {AppSecretConfigured}",
    whatsappOptions.Enabled,
    !string.IsNullOrWhiteSpace(whatsappOptions.VerifyToken),
    !string.IsNullOrWhiteSpace(whatsappOptions.PhoneNumberId),
    !string.IsNullOrWhiteSpace(whatsappOptions.AppSecret));

app.Logger.LogInformation(
    "WhatsApp template configuration loaded. " +
    "TemplateName: {TemplateName}; Language: {Language}; " +
    "NamedParameterEnabled: {NamedParameterEnabled}",
    whatsappOptions.Templates.InformationRequested.Name,
    whatsappOptions.Templates.InformationRequested.Language,
    !string.IsNullOrWhiteSpace(whatsappOptions.Templates.InformationRequested
        .BodyParameterName));

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "CondoLink API";

        options.EnableFilter();
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();

        options.DocExpansion(
            Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });

    app.MapOpenApi();
}

app.UseCors("FrontendDevelopment");
app.Use(async (context, next) =>
{
    var started=System.Diagnostics.Stopwatch.GetTimestamp();
    var queries=context.RequestServices.GetRequiredService<QueryPerformanceScope>();
    using var queryScope=queries.Begin();
    var failed=false;
    try
    {
        await next();
    }
    catch
    {
        failed=true;
        throw;
    }
    finally
    {
        var duration=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var route=(context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText??"unmatched";
        var query=queries.Snapshot();
        var status=failed&&!context.Response.HasStarted?StatusCodes.Status500InternalServerError:context.Response.StatusCode;
        context.RequestServices.GetRequiredService<ApiRequestMetrics>().Record(new(DateTime.UtcNow,context.Request.Method,route,status,duration,context.Response.ContentLength,query.QueryCount,query.SlowQueryCount,query.TotalDurationMs,query.MaximumDurationMs));
        if(duration>=1000||status>=500)
            app.Logger.LogWarning("Slow or failed request. Method: {Method}; Route: {Route}; Status: {Status}; DurationMs: {DurationMs}; ResponseBytes: {ResponseBytes}; QueryCount: {QueryCount}; SqlDurationMs: {SqlDurationMs}.",context.Request.Method,route,status,Math.Round(duration,1),context.Response.ContentLength,query.QueryCount,Math.Round(query.TotalDurationMs,1));
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
        "/health",
        async (
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var canConnect =
                await dbContext.Database.CanConnectAsync(
                    cancellationToken);

            return canConnect
                ? Results.Ok(new
                {
                    status = "healthy",
                    database = "connected"
                })
                : Results.Json(
                    new
                    {
                        status = "unhealthy",
                        database = "disconnected"
                    },
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable);
        })
    .WithTags("System")
    .WithSummary("Check API and database health")
    .WithDescription(
        "Checks whether the API can connect to the database.");

// Authentication
app.MapLogin();
app.MapChangeTemporaryPassword();
app.MapFirstAccess();

// Users
app.MapCreateUser();
app.MapGetCurrentUser();
app.MapListMyCondominiums();

// Condominiums
app.MapCreateCondominium();
app.MapGetCondominiumById();
app.MapListCondominiums();

// Condominium members
app.MapAddCondominiumMember();
app.MapAddCondominiumMemberRole();
app.MapListCondominiumMembers();
app.MapExportCondominiumMembersPdf();
app.MapManageResidentLifecycle();
app.MapOnboardCondominiumMember();
app.MapResetMemberTemporaryPassword();
app.MapUpdateCondominiumMember();
app.MapCondominiumSetup();
app.MapCondominiumAssistant();

// Management
app.MapManagementContext();

// Blocks
app.MapCondominiumBlocks();

// Units
app.MapCreateUnit();
app.MapGetUnitById();
app.MapListCondominiumUnits();
app.MapListMyRequestUnits();
app.MapManageUnit();

// Unit memberships
app.MapCreateUnitMembership();
app.MapListUnitMemberships();
app.MapManageUnitMembership();

// Categories
app.MapCreateCategory();
app.MapListCondominiumCategories();
app.MapManageCategory();

// Requests
app.MapCreateRequest();
app.MapGetRequestById();
app.MapListMyRequests();
app.MapListCondominiumRequests();
app.MapUpdateRequestStatus();
app.MapSuggestRequestStatusMessage();
app.MapUpdateRequestPriority();
app.MapCreateResidentReply();
app.MapManageResidentClosure();

// Reports
app.MapGetRequestReport();

// Notifications
app.MapNotifications();

// Request messages and attachments
app.MapCreateRequestMessage();
app.MapListRequestMessages();
app.MapRequestAttachments();

// External integrations
app.MapWhatsAppWebhook();
app.MapWhatsAppAdministration();

// Overwatch
app.MapOverwatchEndpoints();

await app.InitializePlatformAdminAsync();

app.Run();
