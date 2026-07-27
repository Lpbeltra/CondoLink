using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class UpdateOverwatchManager
{
    public static IEndpointRouteBuilder MapUpdateOverwatchManager(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/overwatch/managers/{managerId:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Update manager");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId, CreateOverwatchManager.Request request,
        AppDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { message = "Full name and email are required." });
        var user = await (from current in db.Users
            join userRole in db.UserRoles on current.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where current.Id == managerId && role.Name == "Manager"
            select current).SingleOrDefaultAsync(cancellationToken);
        if (user is null) return Results.NotFound(new { message = "Manager not found." });
        var error = ManagerValidation.Validate(request);
        if (error is not null) return Results.BadRequest(new { message = error });
        var cpf = Domain.RegistrationData.Digits(request.Cpf);
        var cnpj = Domain.RegistrationData.Digits(request.Cnpj);
        var conflict = await ManagerValidation.FindConflictAsync(
            db, cpf, cnpj, managerId, cancellationToken);
        if (conflict is not null) return Results.Conflict(new { message = conflict });
        user.UpdateManagerProfile(request.FullName, request.PhoneNumber, cpf, cnpj,
            request.Address, request.City, request.State);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
