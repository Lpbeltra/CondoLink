using System.Security.Cryptography;
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
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new
            {
                message = "Full name and email are required."
            });
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (fullName.Length > 200 || email.Length > 254)
        {
            return Results.BadRequest(new
            {
                message = "Full name or email exceeds the allowed length."
            });
        }

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

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser(fullName, email, null);

        var createResult = await userManager.CreateAsync(
            user,
            temporaryPassword);

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
                user.IsActive,
                0,
                user.CreatedAt,
                user.UpdatedAt,
                temporaryPassword));
    }

    private static string GenerateTemporaryPassword()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var randomPart = new string(
            bytes.Select(value => characters[value % characters.Length])
                .ToArray());
        return $"Aa1!{randomPart}";
    }

    public sealed record Request(string? FullName, string? Email);

    public sealed record ManagerCreatedResponse(
        Guid Id,
        string FullName,
        string Email,
        bool IsActive,
        int CondominiumCount,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string TemporaryPassword);
}
