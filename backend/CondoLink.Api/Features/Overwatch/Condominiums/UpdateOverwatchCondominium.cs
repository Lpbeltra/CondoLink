using CondoLink.Domain;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class UpdateOverwatchCondominium
{
    public static IEndpointRouteBuilder MapUpdateOverwatchCondominium(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/overwatch/condominiums/{id:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Update condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id, CondominiumRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.Condominiums.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return Results.NotFound(new { message = "Condominium not found." });
        var error = CondominiumValidation.Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var name = request.Name!.Trim();
        var cnpj = RegistrationData.Digits(request.Cnpj)!;
        if (await db.Condominiums.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken))
            return Results.Conflict(new { message = "A condominium with this name already exists." });
        if (await db.Condominiums.AnyAsync(x => x.Id != id && x.Cnpj == cnpj, cancellationToken))
            return Results.Conflict(new { message = "A condominium with this CNPJ already exists." });
        item.Update(name, request.Email, cnpj, request.Address, request.City,
            request.State, request.HasDoorman, request.IsRemoteDoorman,
            request.DoormanContact);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { item.Id });
    }
}
