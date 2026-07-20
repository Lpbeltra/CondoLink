using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Blocks;
using CondoLink.Api.Features.Categories;
using CondoLink.Api.Features.CondominiumMemberRoles;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.Condominiums;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.UnitMemberships;
using CondoLink.Api.Features.Units;
using CondoLink.Api.Features.Users;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.OpenApi;
using CondoLink.Api;
using CondoLink.Api.Features.Overwatch;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
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
app.MapOnboardCondominiumMember();

// Management
app.MapManagementContext();

// Blocks
app.MapCondominiumBlocks();

// Units
app.MapCreateUnit();
app.MapGetUnitById();
app.MapListCondominiumUnits();
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
app.MapUpdateRequestPriority();

// Request messages and attachments
app.MapCreateRequestMessage();
app.MapListRequestMessages();
app.MapRequestAttachments();

await app.InitializePlatformAdminAsync();

app.MapOverwatchEndpoints();

app.Run();