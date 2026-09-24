using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.CommercialDemo;

public static class CommercialDemoEndpoints
{
    public static IEndpointRouteBuilder MapCommercialDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/overwatch/commercial-demo")
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        group.MapGet("", GetStatus);
        group.MapGet("/revert-preview", GetStatus);
        group.MapPost("/create", Create);
        group.MapPost("/revert", Revert);
        return endpoints;
    }

    private static async Task<IResult> GetStatus(CommercialDemoReverter reverter, CancellationToken ct)
    {
        var state = await reverter.InspectAsync(ct);
        return Results.Ok(new { key = CommercialDemoManifest.DatasetKey, state.Exists,
            state.Counts, state.Conflicts, state.ExternalRecordsAffected,
            canRevert = state.Exists && state.Conflicts.Count == 0 });
    }

    private static async Task<IResult> Create(CreateRequest request, ClaimsPrincipal caller,
        AppDbContext db, CommercialDemoCreator creator, CancellationToken ct)
    {
        if (request.Confirmation != $"CREATE {CommercialDemoManifest.DatasetKey}")
            return Results.BadRequest(new { message = "Explicit dataset confirmation is required." });
        if (!StrongPassword(request.ManagerPassword) || !StrongPassword(request.ResidentPassword)
            || !StrongPassword(request.EmployeePassword))
            return Results.BadRequest(new { message = "Provide three distinct passwords of at least 16 characters with upper, lower, digit and symbol." });
        if (new[] { request.ManagerPassword, request.ResidentPassword, request.EmployeePassword }.Distinct().Count() != 3)
            return Results.BadRequest(new { message = "Demo login passwords must be distinct." });
        if (await db.CommercialDemoDatasets.AsNoTracking().AnyAsync(x =>
                x.Key == CommercialDemoManifest.DatasetKey, ct))
            return Results.Ok(new { key = CommercialDemoManifest.DatasetKey, alreadyExists = true });
        if (await db.Condominiums.AnyAsync(x => x.Name == "Residencial Aurora", ct) ||
            await db.ManagementCompanies.AnyAsync(x => x.Name == "Administradora Horizonte", ct))
            return Results.Conflict(new { message = "A similarly named external record exists; no demo data was created." });
        var operatorId = Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
        CommercialDemoManifest manifest;
        try
        {
            manifest = await creator.CreateAsync(operatorId,
                new DemoCredentials(request.ManagerPassword, request.ResidentPassword, request.EmployeePassword), ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.CommercialDemoDatasets.AsNoTracking().AnyAsync(
                x => x.Key == CommercialDemoManifest.DatasetKey, ct))
                return Results.Ok(new { key = CommercialDemoManifest.DatasetKey, alreadyExists = true });
            throw;
        }
        return Results.Created("/overwatch/commercial-demo", new {
            key = CommercialDemoManifest.DatasetKey, alreadyExists = false,
            counts = manifest.Entities.ToDictionary(x => x.Key.Split('.').Last(), x => x.Value.Count) });
    }

    private static async Task<IResult> Revert(RevertRequest request, ClaimsPrincipal caller,
        CommercialDemoReverter reverter, CancellationToken ct)
    {
        if (request.Confirmation != $"REVERT {CommercialDemoManifest.DatasetKey}")
            return Results.BadRequest(new { message = "Explicit dataset confirmation is required." });
        var operatorId = Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var state = await reverter.RevertAsync(operatorId, ct);
        if (state.Conflicts.Count != 0)
            return Results.Conflict(new { message = "Revert refused: external or ambiguous references exist.",
                state.Conflicts, state.Counts, state.ExternalRecordsAffected });
        return Results.Ok(new { key = CommercialDemoManifest.DatasetKey,
            alreadyAbsent = !state.Exists, removed = state.Counts });
    }

    private static bool StrongPassword(string? password) => password is { Length: >= 16 }
        && password.Any(char.IsUpper) && password.Any(char.IsLower)
        && password.Any(char.IsDigit) && password.Any(x => !char.IsLetterOrDigit(x));

    public sealed record CreateRequest(string Confirmation, string ManagerPassword,
        string ResidentPassword, string EmployeePassword);
    public sealed record RevertRequest(string Confirmation);
}
