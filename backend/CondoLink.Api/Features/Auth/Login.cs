using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace CondoLink.Api.Features.Auth;

public static class Login
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { error = "Email is required." });
        }

        var email = request.Email.Trim();

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(new { error = "Email is invalid." });
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return Results.BadRequest(new { error = "Password is required." });
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
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

        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var key = configuration["Jwt:Key"]!;
        var expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes");
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(expirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now).ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        var response = new Response(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked(expirationMinutes * 60),
            new UserResponse(user.Id, user.FullName, user.Email!, user.IsActive));

        return Results.Ok(response);
    }

    public sealed record Request(string? Email, string? Password);

    public sealed record Response(
        string AccessToken,
        string TokenType,
        int ExpiresIn,
        UserResponse User);

    public sealed record UserResponse(
        Guid Id,
        string FullName,
        string Email,
        bool IsActive);
}
