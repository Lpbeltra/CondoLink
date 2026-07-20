using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class CreateOverwatchManager
{
    private const string ManagerRoleName = "Manager";

    public static IEndpointRouteBuilder MapCreateOverwatchManager(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/managers",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create manager")
            .WithDescription("Creates a new condominium manager.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Results.Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        if (!await roleManager.RoleExistsAsync(ManagerRoleName))
        {
            var createRoleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(ManagerRoleName));

            if (!createRoleResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    errors = createRoleResult.Errors
                        .Select(error => error.Description)
                });
            }
        }

        var user = new ApplicationUser(
            request.FullName,
            email,
            request.PhoneNumber);

        var createResult = await userManager.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                errors = createResult.Errors
                    .Select(error => error.Description)
            });
        }

        var roleResult = await userManager.AddToRoleAsync(
            user,
            ManagerRoleName);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return Results.BadRequest(new
            {
                errors = roleResult.Errors
                    .Select(error => error.Description)
            });
        }

        return Results.Created(
            $"/overwatch/managers/{user.Id}",
            new ManagerCreatedResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.IsActive));
    }

    public sealed record Request(
        string FullName,
        string Email,
        string? PhoneNumber,
        string Password);

    public sealed record ManagerCreatedResponse(
        Guid Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool IsActive);
}