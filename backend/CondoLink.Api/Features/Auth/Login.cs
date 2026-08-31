using System.ComponentModel.DataAnnotations;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.Auth;

public static class Login
{
    public static IEndpointRouteBuilder MapLogin(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", HandleAsync)
            .WithTags("Authentication")
            .WithSummary("Authenticate user");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager,
        [FromServices] AuthenticationSessionService sessions,
        HttpResponse httpResponse,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(
                new { error = "Email is required." });
        }

        var email = request.Email.Trim();

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(
                new { error = "Email is invalid." });
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return Results.BadRequest(
                new { error = "Password is required." });
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null
            || !await userManager.CheckPasswordAsync(
                user,
                request.Password))
        {
            return Results.Json(
                new { error = "Invalid email or password." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!user.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (user.MustChangePassword)
        {
            return Results.Ok(new PasswordChangeRequiredResponse(
                true,
                user.Email!));
        }

        var response = await sessions.IssueAsync(user, httpResponse, cancellationToken);
        return response is null
            ? Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError)
            : Results.Ok(response);
    }

    public sealed record Request(
        string? Email,
        string? Password);

    public sealed record Response(
        bool RequiresPasswordChange,
        string AccessToken,
        string TokenType,
        int ExpiresIn,
        UserResponse User);

    public sealed record UserResponse(
        Guid Id,
        string FullName,
        string Email,
        bool IsActive,
        IReadOnlyList<string> Roles);

    public sealed record PasswordChangeRequiredResponse(
        bool RequiresPasswordChange,
        string Email);
}
