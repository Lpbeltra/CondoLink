using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Categories;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Condominiums;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.CondominiumMemberRoles;
using CondoLink.Api.Features.Units;
using CondoLink.Api.Features.UnitMemberships;
using CondoLink.Api.Features.Users;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendDevelopment");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Json(
            new { status = "unhealthy", database = "disconnected" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapCreateCondominium();
app.MapGetCondominiumById();
app.MapListCondominiums();
app.MapCreateUnit();
app.MapGetUnitById();
app.MapListCondominiumUnits();
app.MapCreateUser();
app.MapLogin();
app.MapGetCurrentUser();
app.MapListMyCondominiums();
app.MapAddCondominiumMember();
app.MapAddCondominiumMemberRole();
app.MapListCondominiumMembers();
app.MapCreateUnitMembership();
app.MapListUnitMemberships();
app.MapCreateCategory();
app.MapListCondominiumCategories();
app.MapCreateRequest();
app.MapGetRequestById();
app.MapListMyRequests();
app.MapCreateRequestMessage();
app.MapListRequestMessages();
app.MapUpdateRequestStatus();
app.MapUpdateRequestPriority();
app.MapListCondominiumRequests();

app.Run();
