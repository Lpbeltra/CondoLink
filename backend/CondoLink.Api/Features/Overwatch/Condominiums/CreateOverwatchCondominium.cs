using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class CreateOverwatchCondominium
{
    public static IEndpointRouteBuilder MapCreateOverwatchCondominium(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/overwatch/condominiums", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Create condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        CondominiumRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var error = CondominiumValidation.Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var name = request.Name!.Trim();
        var cnpj = RegistrationData.Digits(request.Cnpj)!;
        if (await db.Condominiums.AnyAsync(item => item.Name == name, cancellationToken))
            return Results.Conflict(new { message = "A condominium with this name already exists." });
        if (await db.Condominiums.AnyAsync(item => item.Cnpj == cnpj, cancellationToken))
            return Results.Conflict(new { message = "A condominium with this CNPJ already exists." });
        var item = new Condominium(name, request.Email, cnpj, request.Address,
            request.City, request.State, request.HasDoorman,
            request.IsRemoteDoorman, request.DoormanContact);
        item.ConfigureWhatsAppUpdates(request.WhatsAppUpdatesEnabled ?? true, null);
        db.Condominiums.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/overwatch/condominiums/{item.Id}", new { item.Id });
    }
}
