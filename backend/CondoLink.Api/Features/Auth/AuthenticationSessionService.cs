using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace CondoLink.Api.Features.Auth;

public sealed class AuthenticationSessionService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    public async Task<Login.Response?> IssueAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var key = configuration["Jwt:Key"]!;
        var expirationMinutes =
            configuration.GetValue<int>("Jwt:ExpirationMinutes");
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(expirationMinutes);

        user.MarkSuccessfulLogin(now);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return null;

        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now).ToString(),
                ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Select(
            role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new Login.Response(
            false,
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked(expirationMinutes * 60),
            new Login.UserResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.IsActive,
                roles.ToList()));
    }
}
