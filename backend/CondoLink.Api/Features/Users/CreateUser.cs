using System.ComponentModel.DataAnnotations;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.Users;

public static class CreateUser
{
    public static IEndpointRouteBuilder MapCreateUser(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/users", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { error = "Full name is required." });
        }

        if (request.FullName.Trim().Length > 200)
        {
            return Results.BadRequest(new { error = "Full name must not exceed 200 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { error = "Email is required." });
        }

        var email = request.Email.Trim();

        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(new { error = "Email is invalid." });
        }

        if (request.PhoneNumber?.Trim().Length > 30)
        {
            return Results.BadRequest(new { error = "PhoneNumber must not exceed 30 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Password is required." });
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return DuplicateEmailConflict();
        }

        var user = new ApplicationUser(request.FullName, email, request.PhoneNumber);
        IdentityResult result;

        try
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmailViolation(exception))
        {
            return DuplicateEmailConflict();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return DuplicateEmailConflict();
            }

            return Results.BadRequest(new
            {
                errors = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        var response = new Response(
            user.Id,
            user.FullName,
            user.Email!,
            user.PhoneNumber,
            user.IsActive,
            user.CreatedAt);

        return Results.Created($"/users/{user.Id}", response);
    }

    private static bool IsDuplicateEmailViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ApplicationUserConfiguration.UniqueNormalizedEmailIndex
                or "UserNameIndex"
        };
    }

    private static IResult DuplicateEmailConflict()
    {
        return Results.Conflict(new { error = "A user with this email already exists." });
    }

    public sealed record Request(
        string? FullName,
        string? Email,
        string? PhoneNumber,
        string? Password);

    public sealed record Response(
        Guid Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt);
}
